using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;

namespace FreeSims.Tests
{
    public static class SavedLotObjectTests
    {
        public const int Count = 10;
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
        private static IffFile House(GamePaths paths, int number)
        {
            return new IffFile(new NeighborhoodStore(paths, 0).GetReadPath("Houses/House" + number.ToString("00") + ".iff"));
        }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            bool oldWorld = VM.UseWorld; VM.UseWorld = false;
            var results = new List<string>();
            TS1LotObjectSession first = null, second = null;
            Action<string, Action> check = (name, action) => {
                try { action(); results.Add("PASS " + name); }
                catch (Exception ex) { results.Add("FAIL " + name); log(ex.ToString()); }
                log(results[results.Count - 1]);
            };
            try {
                check("CONTENT OVERRIDES AND CACHE", () => ContentOverrides(paths));
                var content = new TS1ObjectProvider(paths);
                check("EXPANSION AND DOWNLOAD CONTENT", () => {
                    var baseContent = new TS1ObjectProvider(paths, false);
                    Require(content.DefinitionCount > baseContent.DefinitionCount, "Expansion content missing.");
                    Require(content.SourceFiles.Any(x => x.StartsWith("Downloads/")), "Download content missing.");
                    // Native reflection must retain all primitive operands.
                    var context = new VMContext(null);
                    var operands = context.Primitives.Where(p => p != null && p.OperandModel != null).Select(p => p.OperandModel).Distinct().ToArray();
                    foreach (var type in operands) Require(Activator.CreateInstance(type) is VMPrimitiveOperand, type.FullName);
                    log("CONTENT GUIDS=" + content.DefinitionCount + " SOURCES=" + content.SourceFiles.Count + " OVERRIDES=" + content.OverrideCount + " OPERANDS=" + operands.Length);
                });
                check("HOUSE 2 SAVED OBJECT GRAPH", () => {
                    var iff = House(paths, 2); first = TS1LotObjectSession.Load(iff, content);
                    VerifyGraph(first, iff, 388, 247);
                    log("HOUSE=2 ENTITIES=" + first.SavedObjectCount + " GROUPS=" + first.GroupCount);
                });
                check("HOUSE 28 SAVED OBJECT GRAPH", () => {
                    var iff = House(paths, 28); second = TS1LotObjectSession.Load(iff, content);
                    VerifyGraph(second, iff, 755, 540);
                    log("HOUSE=28 ENTITIES=" + second.SavedObjectCount + " GROUPS=" + second.GroupCount);
                    foreach (var source in iff.Get<OBJM>(1).ObjectData.Values.Select(x => content.GetSourceFile(x.GUID)).Distinct().OrderBy(x => x)) log("HOUSE28 SOURCE " + source);
                });
                check("SAVED CONTAINER SLOT LINKS", () => {
                    var iff = House(paths, 28); var map = iff.Get<OBJM>(1);
                    Require(first.ContainedCount == 0 && second.ContainedCount == 11, "Unexpected containment totals.");
                    foreach (var saved in map.ObjectData.Values.Where(x => x.ContainerID != 0)) {
                        var entity = second.VM.GetObjectById((short)saved.ObjectID);
                        Require(entity.Container.ObjectID == saved.ContainerID && entity.ContainerSlot == saved.ContainerSlot, "Wrong container/slot.");
                        Require(ReferenceEquals(entity.Container.Contained[saved.ContainerSlot], entity), "Missing reverse slot link.");
                    }
                });
                check("FAILED LOAD PRESERVES ACTIVE LOT", () => {
                    var original = second.VM.Entities.ToArray();
                    var missing = House(paths, 28);
                    missing.Get<OBJT>(0).Entries.First(x => x.GUID != 0).GUID = 0xFFFFFFFF;
                    Reject<FileNotFoundException>(() => { using (TS1LotObjectSession.Load(missing, content)) {} });
                    var cycle = House(paths, 28); var record = cycle.Get<OBJM>(1).ObjectData.Values.First();
                    record.ContainerID = record.ObjectID;
                    Reject<InvalidDataException>(() => { using (TS1LotObjectSession.Load(cycle, content)) {} });
                    var bounds = House(paths, 28); bounds.Get<OBJM>(1).ObjectData.Values.First().SavedX = 32767;
                    Reject<InvalidDataException>(() => { using (TS1LotObjectSession.Load(bounds, content)) {} });
                    var slot = House(paths, 28);
                    slot.Get<OBJM>(1).ObjectData.Values.First(x => x.ContainerID != 0).ContainerSlot = 32767;
                    Reject<InvalidDataException>(() => { using (TS1LotObjectSession.Load(slot, content)) {} });
                    Require(original.SequenceEqual(second.VM.Entities), "Failed import changed active entities.");
                    foreach (var entity in original) Require(ReferenceEquals(second.VM.GetObjectById(entity.ObjectID), entity), "Failed import changed IDs.");
                });
                check("PAUSED SAVED BEHAVIORS STAY HELD", () => {
                    var snapshots = first.VM.Entities.Select(x => (short[])x.Thread.TempRegisters.Clone()).ToArray();
                    for (int i = 0; i < 30; i++) first.Tick();
                    Require(first.VM.Entities.Count == 388, "Paused objects spawned/deleted entities.");
                    for (int i = 0; i < snapshots.Length; i++)
                        Require(first.VM.Entities[i].Thread.ThreadBreak == VMThreadBreakMode.Pause &&
                            snapshots[i].SequenceEqual(first.VM.Entities[i].Thread.TempRegisters), "Held thread executed.");
                });
                check("SAVED CHAIRS RESTART AND TICK", () => {
                    var errors = new List<string>(); second.VM.OnScriptError += e => errors.Add(e.ToString());
                    // Explicit allowlist: saved execution stacks are not resumed. Other behaviors remain held.
                    var chairs = second.VM.Entities.Where(e => e.Object.Resource.Name == "ChairsHD.iff" ||
                        e.Object.OBJ.ChunkLabel == "Chair - Outdoor - Moderate").ToArray();
                    Require(chairs.Length > 0, "No supported saved chairs.");
                    foreach (var entity in chairs) second.RestartMain(entity.ObjectID);
                    long before = second.VM.Context.Clock.Ticks;
                    for (int i = 0; i < 150; i++) second.Tick();
                    Require(errors.Count == 0, string.Join("\n", errors));
                    Require(second.VM.Context.Clock.Ticks == before + 150 && second.VM.Entities.Count == 755, "Unexpected tick/entity totals.");
                    Require(chairs.All(x => x.Thread.ThreadBreak != VMThreadBreakMode.Pause && x.Thread.Stack.Count > 0), "Chair main did not run.");
                    log("SAVED MAIN STARTED=" + chairs.Length + " TICKS=150 SCRIPT_ERRORS=" + errors.Count);
                });
                check("SAVED IDS AND NEW OBJECT ALLOCATION", () => {
                    var original = second.VM.Entities.ToArray();
                    var created = second.VM.Context.CreateObjectInstance(0x26BFBB29, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    Require(created != null && created.Objects.Count == 1, "Fresh chair creation failed.");
                    Require(!original.Any(x => x.ObjectID == created.Objects[0].ObjectID), "Allocated ID collides with saved ID.");
                    foreach (var entity in original) Require(ReferenceEquals(entity, second.VM.GetObjectById(entity.ObjectID)), "Saved ID overwritten.");
                    created.Objects[0].Delete(true, second.VM.Context);
                    Require(original.SequenceEqual(second.VM.Entities), "Create/delete damaged saved graph.");
                });
                check("DISPOSED SESSION REJECTS WORK", () => {
                    first.Dispose(); second.Dispose();
                    Reject<ObjectDisposedException>(() => first.Tick());
                    Reject<ObjectDisposedException>(() => second.RestartMain(1));
                });
            } catch (Exception ex) { results.Add("FAIL SAVED CONTENT"); log(ex.ToString()); }
            finally { if (first != null) first.Dispose(); if (second != null) second.Dispose(); VM.UseWorld = oldWorld; }
            return results;
        }
        private static void VerifyGraph(TS1LotObjectSession session, IffFile iff, int count, int groups)
        {
            var map = iff.Get<OBJM>(1);
            Require(session.SavedObjectCount == count && session.VM.Entities.Count == count && session.GroupCount == groups, "Incomplete object graph.");
            Require(session.VM.Context.Clock.Hours == iff.Get<SIMI>(1).GlobalData[0], "Saved clock lost.");
            foreach (var saved in map.ObjectData.Values) {
                var entity = session.VM.GetObjectById((short)saved.ObjectID);
                Require(entity != null && entity.Object.OBJ.GUID == saved.GUID && entity.PersistID == saved.ObjectID, "ID/GUID mismatch.");
                Require(entity.MultitileGroup.Objects.Contains(entity), "Missing group membership.");
                Require(entity.Direction == (Direction)(1 << saved.Direction), "Direction changed.");
                if (saved.ContainerID == 0 && saved.SavedX != -16)
                    Require(entity.Position.x == saved.SavedX && entity.Position.y == saved.SavedY && entity.Position.Level == saved.SavedLevel, "Saved position changed.");
                for (int i = 0; i < saved.Attributes.Length; i++) Require(entity.GetAttribute(i) == saved.Attributes[i], "Saved attribute lost.");
                Require(entity.Thread.TempRegisters.Take(8).SequenceEqual(saved.TempRegisters) && entity.Thread.TempRegisters.Skip(8).All(x => x == 0), "Saved temporary registers lost.");
                Require(entity.Thread.ThreadBreak == VMThreadBreakMode.Pause, "Saved thread was implicitly resumed.");
            }
        }
        private static void ContentOverrides(GamePaths paths)
        {
            string root = paths.GetUserDataPath("ContentOverrideFixture");
            var fixture = new GamePaths(Path.Combine(root, "Content"), Path.Combine(root, "Game"), Path.Combine(root, "User"));
            WriteFar(fixture.GetGameDataPath("GameData/Global/Global.far"), "global.iff", IffBytes());
            WriteFar(fixture.GetGameDataPath("GameData/Objects/Objects.far"), "base.iff", IffBytes(1001, 1002));
            WriteFar(fixture.GetGameDataPath("ExpansionPack/objects.far"), "expansion.iff", IffBytes(1001));
            string loose = fixture.GetGameDataPath("Downloads/override.iff");
            Directory.CreateDirectory(Path.GetDirectoryName(loose)); File.WriteAllBytes(loose, IffBytes(1001));
            var content = new TS1ObjectProvider(fixture);
            Require(content.DefinitionCount == 2 && content.OverrideCount == 2, "Override count wrong.");
            Require(content.GetSourceFile(1001) == "Downloads/override.iff", "Loose override lost.");
            var winner = content.GetObject(1001, true);
            Require(content.GetObject(1002, true) != null && ReferenceEquals(winner, content.GetObject(1001, true)), "Sibling load undid override.");
            Require(ReferenceEquals(content.GetGlobal("GLOBAL", true), content.GetGlobal("global.iff", true)), "Global cache unstable.");
        }
        private static byte[] IffBytes(params uint[] guids)
        {
            var iff = new IffFile(); ushort id = 1;
            foreach (uint guid in guids) iff.AddChunk(new OBJD { ChunkID = id++, ChunkLabel = "Synthetic", ChunkProcessed = true, GUID = guid, ObjectType = OBJDType.Normal });
            using (var stream = new MemoryStream()) { iff.Write(stream); return stream.ToArray(); }
        }
        private static void WriteFar(string path, string name, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream)) {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("FAR!byAZ")); writer.Write(1); writer.Write(16 + bytes.Length);
                writer.Write(bytes); writer.Write(1); writer.Write(bytes.Length); writer.Write(bytes.Length); writer.Write(16);
                var filename = System.Text.Encoding.ASCII.GetBytes(name); writer.Write(filename.Length); writer.Write(filename);
            }
        }
    }
}