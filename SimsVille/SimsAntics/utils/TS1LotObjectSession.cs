using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;

namespace FSO.SimAntics
{
    /// <summary>
    /// An isolated headless VM built from saved object placement/state prefixes.
    /// All threads start paused: saved stacks/queues/person state are not decoded.
    /// RestartMain explicitly begins a NEW main routine, never resumes a saved stack.
    /// Dispose before replacing the active lot. Failed loads never mutate another VM.
    /// </summary>
    public sealed class TS1LotObjectSession : IDisposable
    {
        private readonly VMOfflineDriver driver;
        private bool disposed;
        public VM VM { get; private set; }
        public int SavedObjectCount { get; private set; }
        public int GroupCount { get; private set; }
        public int ContainedCount { get; private set; }
        private TS1LotObjectSession(VM vm, VMOfflineDriver driver) { VM = vm; this.driver = driver; }

        public static TS1LotObjectSession Load(IffFile iff, TS1ObjectProvider content, bool visualPersons = false)
        {
            if (VM.UseWorld) throw new InvalidOperationException("Saved object import currently requires a headless VM.");
            if (iff == null || content == null) throw new ArgumentNullException();
            var map = iff.Get<OBJM>(1);
            var simi = iff.Get<SIMI>(1);
            if (map == null || simi == null || simi.GlobalData == null || simi.GlobalData.Length < 33)
                throw new InvalidDataException("Missing lot object/global data.");
            map.ResolveTypes(iff.Get<OBJT>(0));
            int size = simi.GlobalData[23];
            if (size < 1 || size > 64) throw new InvalidDataException("Invalid lot size.");
            var defs = new Dictionary<int, GameObject>();
            foreach (var pair in map.ObjectData) {
                var saved = pair.Value;
                if (pair.Key != saved.ObjectID || saved.ObjectID <= 0 || saved.ObjectID > short.MaxValue || saved.Data == null || saved.Data.Length != 73 || saved.Attributes == null || saved.TempRegisters == null || saved.TempRegisters.Length != 8 || saved.Direction < 0 || saved.Direction > 7)
                    throw new InvalidDataException("Invalid saved object record: " + pair.Key);
                bool outside = saved.SavedX == -16 && saved.SavedY == -16;
                if (saved.SavedLevel < 1 || saved.SavedLevel > 2 || (!outside && (saved.SavedX < 0 || saved.SavedY < 0 || saved.SavedX >= size * 16 || saved.SavedY >= size * 16)))
                    throw new InvalidDataException("Saved position outside lot: " + pair.Key);
                if ((saved.ParentID != 0 && !map.ObjectData.ContainsKey(saved.ParentID)) || (saved.ContainerID != 0 && !map.ObjectData.ContainsKey(saved.ContainerID)))
                    throw new InvalidDataException("Dangling saved object link: " + pair.Key);
                var def = content.GetObject(saved.GUID, true);
                if (def == null) throw new FileNotFoundException("Missing TS1 GUID " + saved.GUID.ToString("X8") + " for saved object " + pair.Key);
                if (def.OBJ.ObjectType == OBJDType.Person) {
                    if(!visualPersons)throw new NotSupportedException("Saved Sim/person state is not supported: " + pair.Key);
                    // Visual people are detached snapshots, not runnable VM avatars.
                    if(saved.Type!=OBJDType.Person||saved.ContainerID!=0||saved.ParentID!=0||saved.SavedX<0)
                        throw new NotSupportedException("Visual Sim requires a saved free-standing placement: "+pair.Key);
                    continue;
                }
                if((saved.ContainerID!=0&&map.ObjectData[saved.ContainerID].Type==OBJDType.Person)||
                    (saved.ParentID!=0&&map.ObjectData[saved.ParentID].Type==OBJDType.Person))
                    throw new NotSupportedException("Objects carried by saved Sims require full person state import.");
                defs.Add(pair.Key, def);
                var seen = new HashSet<int>();
                var current = saved;
                while (current.ContainerID != 0) {
                    if (!seen.Add(current.ObjectID) || !map.ObjectData.TryGetValue(current.ContainerID, out current))
                        throw new InvalidDataException("Invalid containment chain: " + pair.Key);
                }
            }
            var driver = new VMOfflineDriver();
            var vm = new VM(new VMContext(null) { ContentProvider = content }, null) { TS1 = true, StopOnScriptError = true };
            var session = new TS1LotObjectSession(vm, driver);
            try {
                vm.VM_SetDriver(driver); vm.Init();
                vm.GlobalState = (short[])simi.GlobalData.Clone();
                vm.Context.Clock.Hours = vm.GlobalState[0];
                vm.Context.Clock.DayOfMonth = vm.GlobalState[1];
                vm.Context.Clock.Minutes = vm.GlobalState[5];
                // Global 6 stores seconds; VMClock runs 150 ticks per simulated minute.
                vm.Context.Clock.TicksPerMinute = 150;
                vm.Context.Clock.MinuteFractions = (vm.GlobalState[6] * 150 + 59) / 60;
                vm.Context.Clock.Month = vm.GlobalState[7];
                vm.Context.Clock.Year = vm.GlobalState[8];
                vm.Context.Architecture = new VMArchitecture(size, size, null, vm.Context);
                LoadArchitecture(iff, vm, size);
                vm.Context.Architecture.Tick();
                var groups = new Dictionary<string, VMMultitileGroup>();
                foreach (var saved in map.ObjectData.Values.OrderBy(x => x.ObjectID)) {
                    if(visualPersons&&saved.Type==OBJDType.Person)continue;
                    var def = defs[saved.ObjectID];
                    var entity = new VMGameObject(def, null);
                    Array.Copy(saved.Data, entity.ObjectData, saved.Data.Length);
                    for (int i = 0; i < saved.Attributes.Length; i++) entity.SetAttribute(i, saved.Attributes[i]);
                    if (entity.Slots != null && entity.Slots.Slots.ContainsKey(0)) entity.Contained = new VMEntity[entity.Slots.Slots[0].Count];
                    entity.Direction = (Direction)(1 << saved.Direction);
                    entity.Position = saved.SavedX == -16 ? LotTilePos.OUT_OF_WORLD : new LotTilePos((short)saved.SavedX, (short)saved.SavedY, (sbyte)saved.SavedLevel);
                    entity.PersistID = (uint)saved.ObjectID;
                    entity.GenerateTreeByName(vm.Context);
                    entity.Thread = new VMThread(vm.Context, entity, def.OBJ.StackSize) { ThreadBreak = VMThreadBreakMode.Pause };
                    Array.Copy(saved.TempRegisters, entity.Thread.TempRegisters, 8);
                    vm.AddRestoredEntity(entity, (short)saved.ObjectID);
                    string key = content.GetSourceFile(saved.GUID) + "!" + GroupKey(saved, def);
                    VMMultitileGroup group;
                    if (!groups.TryGetValue(key, out group)) {
                        group = new VMMultitileGroup { MultiTile = def.OBJ.MasterID != 0 };
                        groups.Add(key, group);
                    }
                    if (group.MultiTile) {
                        var masters = def.Resource.List<OBJD>().Where(x => x.MasterID == def.OBJ.MasterID && x.SubIndex == -1).ToArray();
                        if (masters.Length != 1) throw new InvalidDataException("Missing/ambiguous multitile master: " + saved.ObjectID);
                        entity.MasterDefinition = masters[0];
                        entity.UseTreeTableOf(content.GetObject(masters[0].GUID, true), true);
                        if (group.Objects.Any(x => x.Object.OBJ.SubIndex == def.OBJ.SubIndex && x.Object.OBJ.LevelOffset == def.OBJ.LevelOffset))
                            throw new InvalidDataException("Ambiguous overlapping multitile group: " + saved.ObjectID);
                    }
                    group.AddObject(entity); entity.MultitileGroup = group;
                }
                foreach (var group in groups.Values.Where(x => x.MultiTile)) {
                    var first = group.Objects[0];
                    var expected = first.Object.Resource.List<OBJD>().Where(x => x.MasterID == first.MasterDefinition.MasterID && x.SubIndex >= 0 && x.GUID != 0).Select(x => x.GUID).OrderBy(x => x);
                    var actual = group.Objects.Select(x => x.Object.OBJ.GUID).OrderBy(x => x);
                    if (!expected.SequenceEqual(actual)) throw new InvalidDataException("Incomplete saved multitile group at object " + first.ObjectID);
                }
                foreach (var saved in map.ObjectData.Values.Where(x => x.ContainerID != 0).OrderBy(x => ContainerDepth(x, map))) {
                    var child = vm.GetObjectById((short)saved.ObjectID);
                    var parent = vm.GetObjectById((short)saved.ContainerID);
                    if (!parent.PlaceInSlot(child, saved.ContainerSlot, false, vm.Context))
                        throw new InvalidDataException("Invalid/occupied container slot for object " + saved.ObjectID);
                    session.ContainedCount++;
                }
                foreach (var entity in vm.Entities) {
                    entity.Footprint = entity.Position == LotTilePos.OUT_OF_WORLD ? null : entity.GetObstacle(entity.Position, entity.Direction);
                    vm.Context.RegisterObjectPos(entity);
                    vm.Context.SetToNextCache.RegisterCategory(entity, entity.GetValue(VMStackObjectVariable.Category));
                }
                vm.Context.SetToNextCache.VerifyPositions();
                session.SavedObjectCount = map.ObjectData.Count;
                session.GroupCount = groups.Count;
                return session;
            } catch { session.Dispose(); throw; }
        }
        /// <summary>Use the live game's one-sim-minute-per-real-second cadence.
        /// Raw imports retain their original clock representation until explicitly started.</summary>
        public void ConfigureLiveClock()
        {
            if (disposed) throw new ObjectDisposedException("TS1LotObjectSession");
            VM.Context.Clock.SetTicksPerMinute(VMTimeController.TS1TicksPerMinute);
            VM.GlobalState[6] = (short)VM.Context.Clock.Seconds;
        }

        private static int ContainerDepth(OBJM.MappedObject saved, OBJM map)
        {
            int depth = 0;
            while (saved.ContainerID != 0) { saved = map.ObjectData[saved.ContainerID]; depth++; }
            return depth;
        }
        private static string GroupKey(OBJM.MappedObject saved, GameObject def)
        {
            if (def.OBJ.MasterID == 0) return "single:" + saved.ObjectID;
            if ((saved.Direction & 1) != 0 || def.OBJ.SubIndex < 0 || saved.SavedX == -16)
                throw new NotSupportedException("Unsupported multitile placement: " + saved.ObjectID);
            int x = (sbyte)(def.OBJ.SubIndex >> 8) * 16, y = (sbyte)def.OBJ.SubIndex * 16;
            for (int i = 0; i < saved.Direction / 2; i++) { int oldX = x; x = -y; y = oldX; }
            return def.Resource.Name + ":" + def.OBJ.MasterID + ":" + saved.Direction + ":" + (saved.SavedX - x) + ":" + (saved.SavedY - y) + ":" + (saved.SavedLevel - def.OBJ.LevelOffset);
        }
        private static byte[] ArrayData(IffFile iff, ushort id, int stride)
        {
            var array = iff.Get<ARRY>(id);
            if (array == null || array.Width != 64 || array.Height != 64 || array.ByteSize() != stride || array.Data.Length != 64 * 64 * stride)
                throw new InvalidDataException("Invalid/missing architecture ARRY " + id);
            return array.TransposeData;
        }
        private static T[] Resize<T>(T[] data, int size)
        {
            var result = new T[size * size];
            for (int y = 0; y < size; y++) Array.Copy(data, y * 64, result, y * size, size);
            return result;
        }
        private static void LoadArchitecture(IffFile iff, VM vm, int size)
        {
            // Logical floor/wall IDs only. Rendering still needs TS1 texture-name mapping.
            var decoder = new VMWorldActivator(vm, null);
            bool advanced = iff.Get<ARRY>(11) != null;
            for (int floor = 0; floor < 2; floor++) {
                vm.Context.Architecture.Floors[floor] = Resize(advanced ? decoder.DecodeAdvFloors(ArrayData(iff, (ushort)(11 + 100 * floor), 2)) : decoder.DecodeFloors(ArrayData(iff, (ushort)(1 + 100 * floor), 1)), size);
                vm.Context.Architecture.Walls[floor] = Resize(advanced ? decoder.DecodeAdvWalls(ArrayData(iff, (ushort)(12 + 100 * floor), 14)) : decoder.DecodeWalls(ArrayData(iff, (ushort)(2 + 100 * floor), 8)), size);
            }
            var pools = Resize(ArrayData(iff, 9, 1), size);
            var water = Resize(ArrayData(iff, 10, 1), size);
            for (int i = 0; i < pools.Length; i++) {
                if (pools[i] != 0 && pools[i] != 255) vm.Context.Architecture.Floors[0][i].Pattern = 65535;
                if (water[i] != 0 && water[i] != 255) vm.Context.Architecture.Floors[0][i].Pattern = 65534;
            }
            vm.Context.Architecture.RegenWallsAt();
            vm.Context.Architecture.RegenRoomMap();
        }
        public void RestartMain(short savedID)
        {
            if (disposed) throw new ObjectDisposedException("TS1LotObjectSession");
            if (VM.ScriptExecutionStopped) throw new InvalidOperationException("Discard the faulted lot before restarting behavior.", VM.ScriptFault);
            var entity = VM.GetObjectById(savedID);
            if (entity == null) throw new ArgumentException("Unknown saved object ID.", "savedID");
            entity.Thread = new VMThread(VM.Context, entity, entity.Object.OBJ.StackSize);
        }
        public void Tick()
        {
            if (disposed) throw new ObjectDisposedException("TS1LotObjectSession");
            if (VM.UseWorld) throw new InvalidOperationException("Imported lot requires headless mode.");
            if (!VM.ScriptExecutionStopped) driver.Tick(VM);
            if (VM.ScriptExecutionStopped) throw new InvalidOperationException("Saved lot simulation stopped after a script fault.", VM.ScriptFault);
        }
        public void Dispose() { if (!disposed) { driver.CloseNet(); VM.ReleaseRuntimeReferences(); disposed = true; } }
    }
}
