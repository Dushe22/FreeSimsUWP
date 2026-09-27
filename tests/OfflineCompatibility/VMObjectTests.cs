using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Engine.Primitives;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;

namespace FreeSims.Tests
{
    public static class VMObjectTests
    {
        public const int Count = 8;
        // Real base-game definitions, discovered from the user's Objects.far OBJD index.
        private const uint Chair = 0x26BFBB29;
        private const uint Sofa = 0x0FBB8BF8;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            var errors = new List<string>();
            bool oldWorld = VM.UseWorld;
            VM.UseWorld = false;
            var driver = new VMOfflineDriver();
            try {
                var provider = new TS1ObjectProvider(paths);
                var vm = new VM(new VMContext(null) { ContentProvider = provider }, null) { TS1 = true };
                vm.OnScriptError += error => errors.Add(error.ToString());
                vm.VM_SetDriver(driver); vm.Init();
                vm.Context.Architecture = new VMArchitecture(16, 16, null, vm.Context);
                driver.Tick(vm);
                Action<string, Action> check = (name, action) => {
                    try {
                        action();
                        Require(errors.Count == 0, "SimAntics failure: " + string.Join("\n", errors));
                        results.Add("PASS " + name);
                    } catch (Exception ex) { results.Add("FAIL " + name); log(ex.ToString()); }
                    log(results[results.Count - 1]);
                };
                VMMultitileGroup chair = null, sofa = null;
                check("TS1 ARCHIVE AND GLOBAL CACHE", () => {
                    var operands = vm.Context.Primitives.Where(p => p != null && p.OperandModel != null).Select(p => p.OperandModel).Distinct().ToArray();
                    foreach (var type in operands)
                        Require(Activator.CreateInstance(type) is VMPrimitiveOperand, "Cannot activate VM operand: " + type.FullName);
                    log("VM OPERAND CONSTRUCTORS=" + operands.Length);
                    var def = provider.GetObject(Chair, true);
                    Require(provider.DefinitionCount > 0 && def != null, "Missing real chair definition.");
                    Require(ReferenceEquals(def, provider.GetObject(Chair, true)), "Unstable object cache.");
                    Require(ReferenceEquals(vm.Context.Globals, provider.GetGlobal("GLOBAL.iff", true)), "Unstable global cache.");
                    Require(new VMContext((FSO.LotView.World)null, vm.Context).ContentProvider == provider, "Lost provider in replacement context.");
                    log("TS1 DEFINITIONS=" + provider.DefinitionCount + " CHAIR=" + Chair.ToString("X8") + " SOFA=" + Sofa.ToString("X8"));
                });
                check("MISSING CONTENT AND MODE GUARDS", () => {
                    int before = vm.Entities.Count;
                    Require(vm.Context.CreateObjectInstance(0, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true) == null, "Unknown GUID accepted.");
                    Require(vm.Entities.Count == before, "Unknown GUID changed VM.");
                    Reject<InvalidOperationException>(() => provider.GetObject(Chair, false));
                    Reject<ArgumentException>(() => provider.GetGlobal("../global", true));
                    Reject<FileNotFoundException>(() => provider.GetGlobal("missing-vm-probe-global", true));
                });
                check("SINGLE TILE INIT AND MAIN", () => {
                    chair = vm.Context.CreateObjectInstance(Chair, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    Require(chair != null && chair.Objects.Count == 1 && !chair.MultiTile, "Wrong chair group.");
                    var entity = chair.Objects[0];
                    Require(entity.GetBHAVWithOwner(entity.EntryPoints[0].ActionFunction, vm.Context) != null, "Missing real init BHAV.");
                    Require(entity.GetValue(VMStackObjectVariable.PlacementFlags) == 3, "Real chair init BHAV did not set placement flags.");
                    Require(entity.Thread != null && entity.Thread.Stack.Count > 0 && entity.WorldUI == null, "Main BHAV not queued/headless entity expected.");
                    Require(vm.GetObjectById(entity.ObjectID) == entity && entity.ObjectID != 0, "Entity was not registered.");
                    log("CHAIR INIT flags=" + entity.GetValue(VMStackObjectVariable.Flags) + " placement=" + entity.GetValue(VMStackObjectVariable.PlacementFlags));
                });
                check("MULTITILE INIT AND SHARED RESOURCE", () => {
                    sofa = vm.Context.CreateObjectInstance(Sofa, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    Require(sofa != null && sofa.MultiTile && sofa.Objects.Count == 3, "Expected three sofa tiles.");
                    Require(sofa.Objects.Select(o => o.ObjectID).Distinct().Count() == 3, "Duplicate entity IDs.");
                    foreach (var entity in sofa.Objects) {
                        Require(entity.MasterDefinition.GUID == Sofa && entity.MultitileGroup == sofa, "Lost master/group.");
                        Require(entity.Thread != null && entity.Thread.Stack.Count > 0, "Tile main BHAV not queued.");
                        Require(ReferenceEquals(entity.Object.Resource, provider.GetObject(Sofa, true).Resource), "Tiles do not share IFF resource.");
                        Require(vm.Context.SetToNextCache.GetObjectsByGUID(entity.Object.OBJ.GUID).Contains(entity), "GUID lookup missing tile.");
                    }
                });
                check("REAL OBJECT PLACEMENT AND ROTATION", () => {
                    Require(chair.ChangePosition(new LotTilePos(80, 80, 1), Direction.NORTH, vm.Context).Status == VMPlacementError.Success, "Chair placement failed.");
                    Require(sofa.ChangePosition(new LotTilePos(144, 144, 1), Direction.EAST, vm.Context).Status == VMPlacementError.Success, "Sofa placement failed.");
                    foreach (var entity in vm.Entities) {
                        Require(entity.Position != LotTilePos.OUT_OF_WORLD && vm.Context.SetToNextCache.GetObjectsAt(entity.Position).Contains(entity), "Tile position not registered.");
                    }
                    Require(sofa.Objects.Select(o => o.Position).Distinct().Count() == 3, "Overlapping sofa tiles.");
                    vm.Context.SetToNextCache.VerifyPositions();
                });
                check("SCRIPT CREATE OBJECT USES TS1", () => {
                    var caller = chair.Objects[0];
                    var frame = new VMStackFrame { Thread = caller.Thread, Caller = caller, Callee = caller, CodeOwner = caller.Object, StackObject = caller };
                    var primitive = new VMCreateObjectInstance();
                    int before = vm.Entities.Count;
                    Require(primitive.Execute(frame, new VMCreateObjectInstanceOperand { GUID = 0, Position = VMCreateObjectPosition.OutOfWorld }) == VMPrimitiveExitCode.GOTO_FALSE && vm.Entities.Count == before, "Missing script object changed VM.");
                    Require(primitive.Execute(frame, new VMCreateObjectInstanceOperand { GUID = Chair, Position = VMCreateObjectPosition.OutOfWorld }) == VMPrimitiveExitCode.GOTO_TRUE, "Create primitive failed.");
                    Require(frame.StackObject != caller && frame.StackObject.Object.OBJ.GUID == Chair && vm.Entities.Count == before + 1, "Primitive did not create a new entity.");
                    frame.StackObject.MultitileGroup.Delete(vm.Context);
                    Require(vm.Entities.Count == before, "Script-created object leaked.");
                });
                check("LIVE OBJECTS TICK WITHOUT SCRIPT ERRORS", () => {
                    long before = vm.Context.Clock.Ticks;
                    foreach (var entity in vm.Entities) entity.SetValue(VMStackObjectVariable.LockoutCount, 100);
                    for (int i = 0; i < 150; i++) Require(driver.Tick(vm), "Offline driver stopped.");
                    Require(vm.Context.Clock.Ticks == before + 150 && vm.Entities.Count == 4, "Clock/entities changed unexpectedly.");
                    Require(vm.Entities.All(o => !o.Dead && o.Thread != null && o.GetValue(VMStackObjectVariable.LockoutCount) == 0), "Entity ticks did not run.");
                    log("LIVE VM TICKS=150 ENTITIES=" + vm.Entities.Count + " SCRIPT ERRORS=" + errors.Count);
                });
                check("DELETE AND RECREATE CLEANLY", () => {
                    var oldEntities = vm.Entities.ToArray();
                    chair.Delete(vm.Context); sofa.Delete(vm.Context);
                    Require(vm.Entities.Count == 0 && oldEntities.All(o => vm.GetObjectById(o.ObjectID) == null), "Entities survived deletion.");
                    foreach (var old in oldEntities) {
                        var matches = vm.Context.SetToNextCache.GetObjectsByGUID(old.Object.OBJ.GUID);
                        Require(matches == null || !matches.Contains(old), "Deleted entity in GUID cache.");
                        matches = vm.Context.SetToNextCache.GetObjectsAt(old.Position);
                        Require(matches == null || !matches.Contains(old), "Deleted entity in position cache.");
                    }
                    var again = vm.Context.CreateObjectInstance(Chair, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                    Require(again.Objects.Count == 1 && !oldEntities.Contains(again.Objects[0]), "Recreate reused dead object.");
                    again.Delete(vm.Context);
                    Require(vm.Entities.Count == 0, "Recreated object leaked.");
                });
            } catch (Exception ex) { results.Add("FAIL TS1 VM CONTENT"); log(ex.ToString()); }
            finally { driver.CloseNet(); VM.UseWorld = oldWorld; }
            return results;
        }
    }
}