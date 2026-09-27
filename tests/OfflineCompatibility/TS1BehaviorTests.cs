using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;

namespace FreeSims.Tests
{
    public static class TS1BehaviorTests
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
        private sealed class TracedContent : IVMContentProvider, IVMTS1LotInfo
        {
            public TS1ObjectProvider Source;
            public int ZoningQueries;
            public GameObject GetObject(uint guid, bool ts1) { return Source.GetObject(guid, ts1); }
            public GameGlobal GetGlobal(string name, bool ts1) { return Source.GetGlobal(name, ts1); }
            public short GetLotZoning(short lot) { ZoningQueries++; return Source.GetLotZoning(lot); }
        }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            bool oldWorld = VM.UseWorld; VM.UseWorld = false;
            Action<string, Action> check = (name, action) => {
                try { action(); results.Add("PASS " + name); }
                catch (Exception error) { results.Add("FAIL " + name); log(error.ToString()); }
                log(results[results.Count - 1]);
            };
            try {
                var content = new TS1ObjectProvider(paths);
                check("LOT ZONING VALUES AND CACHE", () => {
                    Require(content.GetLotZoning(2) == 0 && content.GetLotZoning(58) == 1, "Editable zoning mismatch.");
                    Require(content.GetLotZoning(28) == 1 && content.GetLotZoning(81) == 2 && content.GetLotZoning(89) == 2, "Destination defaults mismatch.");
                    Require(content.GetLotZoning(2) == 0, "Unstable zoning cache.");
                    Reject<ArgumentOutOfRangeException>(() => content.GetLotZoning(0));
                });
                check("ZONING OVERRIDES AND INVALID DATA", () => ZoningFixtures(paths));
                check("TS1 DISPATCH AND TSO ISOLATION", () => {
                    using (var session = TS1LotObjectSession.Load(House(paths, 2), content)) {
                        var entity = session.VM.Entities[0];
                        var frame = new VMStackFrame { Caller = entity, Callee = entity, Thread = entity.Thread, CodeOwner = entity.Object };
                        var handler = new VMGenericTSOCall();
                        entity.Thread.TempRegisters[0] = 2;
                        Require(handler.Execute(frame, new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)28 }) == VMPrimitiveExitCode.GOTO_TRUE &&
                            entity.Thread.TempRegisters[0] == 0, "TS1 call 28 did not return residential zoning.");
                        entity.Thread.TempRegisters[0] = 7;
                        handler.Execute(frame, new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)14 });
                        Require(session.VM.GetGlobalValue(31) == 7, "TS1 radio station not assigned.");
                        foreach (byte call in new byte[] { 17, 25, 255 })
                            Reject<NotSupportedException>(() => handler.Execute(frame, new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)call }));
                        Require(entity.Thread.TempRegisters[0] == 7 && session.VM.Entities.Count == 388, "Unsupported call changed state.");
                        session.VM.TS1 = false;
                        handler.Execute(frame, new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)17 });
                        Require(entity.Thread.TempRegisters[0] == 2, "TSO dispatch changed.");
                        session.VM.TS1 = true;
                        var types = session.VM.Context.Primitives.Where(p => p != null && p.OperandModel != null).Select(p => p.OperandModel).Distinct().ToArray();
                        foreach (var type in types) Require(Activator.CreateInstance(type) is VMPrimitiveOperand, "Missing operand constructor.");
                        log("BEHAVIOR OPERAND CONSTRUCTORS=" + types.Length);
                    }
                });
                check("SAVED CLOCK SECONDS RESTORED", () => {
                    foreach (short seconds in new short[] { 0, 1, 42, 51, 52, 59 }) {
                        var iff = House(paths, 2); var saved = iff.Get<SIMI>(1).GlobalData;
                        saved[6] = seconds; // Includes odd seconds, which fall between clock ticks at 150 ticks/minute.
                        using (var session = TS1LotObjectSession.Load(iff, content)) {
                            Require(session.VM.Context.Clock.Seconds == seconds, "Saved seconds were treated as minutes or rounded down.");
                            for (int i = 0; i < 150; i++) session.Tick();
                            Require(session.VM.Context.Clock.Minutes == (saved[5] + 1) % 60 && session.VM.Context.Clock.Seconds == seconds, "Incorrect minute cadence.");
                        }
                    }
                });
                check("SAVED FLOWERS AND SHRUBS RUN", () => {
                    using (var session = TS1LotObjectSession.Load(House(paths, 2), content)) {
                        var trace = new TracedContent { Source = content }; session.VM.Context.ContentProvider = trace;
                        var selected = Select(session, "FlowersOutdoor.iff", "Shrubs.iff");
                        Require(selected.Length == 134, "Expected 93 flowers and 41 shrubs.");
                        foreach (var entity in selected) session.RestartMain(entity.ObjectID);
                        session.Tick();
                        var waits = selected.Select(x => x.Thread.Stack.Last()).ToArray();
                        var delay = waits.Select(x => x.Args[0]).ToArray();
                        for (int i = 0; i < 599; i++) session.Tick();
                        Require(trace.ZoningQueries >= 93, "Flowers did not query TS1 zoning.");
                        Require(waits.Where((f, i) => f.Args[0] < delay[i]).Count() >= 93, "Flower main loops did not advance.");
                        VerifyRunning(session, selected, 388);
                        log("FLOWERS=93 SHRUBS=41 TICKS=600 ZONING_QUERIES=" + trace.ZoningQueries + " SCRIPT_ERRORS=0");
                    }
                });
                check("CEILING LIGHT ZONING RESPONSE", () => Lights(paths, content, "lampceiling.iff", 14, log));
                check("WALL LIGHT ZONING RESPONSE", () => Lights(paths, content, "WallLite.iff", 27, log));
                check("MIXED SAVED OBJECTS LONG RUN", () => {
                    using (var session = TS1LotObjectSession.Load(House(paths, 28), content)) {
                        session.VM.Context.RandomSeed = 12345;
                        var selected = Select(session, "ChairsHD.iff", "lampceiling.iff", "WallLite.iff", "TablesHD.iff", "Sinks.iff", "PlantsHangingHD.iff", "FishTankBig.iff");
                        Require(selected.Length == 80, "Unexpected mixed object fixture.");
                        foreach (var entity in selected) session.RestartMain(entity.ObjectID);
                        for (int i = 0; i < 6000; i++) {
                            if (i == 3000) session.VM.Context.Clock.Hours = 19;
                            session.Tick();
                        }
                        VerifyRunning(session, selected, 755);
                        Require(session.ContainedCount == 11, "Containment fixture changed.");
                        var held = session.VM.Entities.Except(selected).ToArray();
                        Require(held.All(x => x.Thread.ThreadBreak == VMThreadBreakMode.Pause), "Unselected behavior started.");
                        log("MIXED ACTIVE=80 HELD=" + held.Length + " TICKS=6000 SCRIPT_ERRORS=0");
                    }
                });
                check("SCRIPT FAULT PRESERVES LOT", () => FaultContainment(paths, content, false, log));
                check("CHECK TREE FAULT STOPS LOT", () => FaultContainment(paths, content, true, log));
            } catch (Exception error) { results.Add("FAIL BEHAVIOR CONTENT"); log(error.ToString()); }
            finally { VM.UseWorld = oldWorld; }
            return results;
        }
        private static VMEntity[] Select(TS1LotObjectSession session, params string[] files)
        {
            return session.VM.Entities.Where(x => files.Contains(x.Object.Resource.Name)).ToArray();
        }
        private static void VerifyRunning(TS1LotObjectSession session, VMEntity[] selected, int count)
        {
            Require(session.VM.ScriptFault == null && session.VM.Entities.Count == count, "Script fault or unexpected entity mutation.");
            Require(selected.All(x => !x.Dead && x.Thread.ThreadBreak != VMThreadBreakMode.Pause && x.Thread.Stack.Count > 0), "Selected main stopped.");
        }
        private static void Lights(GamePaths paths, TS1ObjectProvider content, string file, int count, Action<string> log)
        {
            using (var session = TS1LotObjectSession.Load(House(paths, 28), content)) {
                session.VM.Context.RandomSeed = 12345;
                session.VM.Context.Clock.Hours = 12;
                var trace = new TracedContent { Source = content }; session.VM.Context.ContentProvider = trace;
                var selected = Select(session, file);
                Require(selected.Length == count, "Missing light fixture.");
                foreach (var entity in selected) session.RestartMain(entity.ObjectID);
                var observedOn = new HashSet<short>();
                for (int i = 0; i < 1800; i++) {
                    session.Tick();
                    foreach (var entity in selected.Where(x => x.GetAttribute(0) == 1)) observedOn.Add(entity.ObjectID);
                }
                var outside = selected.Where(x => session.VM.Context.RoomInfo[x.GetValue(VMStackObjectVariable.Room)].Room.IsOutside).ToArray();
                var inside = selected.Except(outside).ToArray();
                Require(observedOn.SetEquals(inside.Select(x => x.ObjectID)), "Community daytime lights did not match indoor rooms.");
                // Controlled scenario: use real residential lot 2 zoning while retaining the imported geometry.
                session.VM.GlobalState[10] = 2;
                for (int i = 0; i < 1800; i++) session.Tick();
                Require(selected.All(x => x.GetAttribute(0) == 0), "Empty residential lot lights did not switch off.");
                session.VM.Context.Clock.Hours = 19;
                for (int i = 0; i < 1800; i++) session.Tick();
                Require(outside.All(x => x.GetAttribute(0) == 1) && inside.All(x => x.GetAttribute(0) == 0), "Residential nighttime light state mismatch.");
                session.VM.Context.Clock.Hours = 12;
                for (int i = 0; i < 1800; i++) session.Tick();
                Require(selected.All(x => x.GetAttribute(0) == 0), "Lights did not switch off on return to daytime.");
                VerifyRunning(session, selected, 755);
                Require(trace.ZoningQueries > 0, "Light behavior bypassed zoning.");
                log("LIGHT " + file + " COUNT=" + count + " COMMUNITY_INDOOR_ON=" + observedOn.Count +
                    " RESIDENTIAL_DAY_OFF=" + count + " NIGHT_OUTDOOR_ON=" + outside.Length + " TICKS=7200 SCRIPT_ERRORS=0");
            }
        }
        private sealed class FaultedLotCommand : FSO.SimAntics.NetPlay.Model.VMNetCommandBodyAbstract
        {
            public bool Verified, Executed;
            public Action<VM> OnVerify;
            public override bool Verify(VM vm, VMAvatar caller) { Verified = true; if (OnVerify != null) OnVerify(vm); return true; }
            public override bool Execute(VM vm) { Executed = true; return true; }
        }
        private static VMStackFrame FaultFrame(VMEntity entity)
        {
            return new VMStackFrame {
                Caller = entity, Callee = entity, CodeOwner = entity.Object, Args = new short[4],
                Routine = new VMRoutine { ID = 65000, Rti = new VMFunctionRTI { Name = "Original unsupported-call fixture" },
                    Instructions = new[] { new VMInstruction { Opcode = 1, Operand = new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)255 } } } }
            };
        }
        private static void FaultContainment(GamePaths paths, TS1ObjectProvider content, bool checkTree, Action<string> log)
        {
            using (var session = TS1LotObjectSession.Load(House(paths, 2), content)) {
                int reports = 0, dialogs = 0;
                session.VM.OnScriptError += e => reports++;
                session.VM.OnDialog += e => dialogs++;
                var original = session.VM.Entities.ToArray();
                var entity = original[0]; entity.SetAttribute(0, 1234);
                entity.SetValue(VMStackObjectVariable.LockoutCount, 5);
                var observer = original[1];
                observer.Thread = new VMThread(session.VM.Context, observer, 5);
                var observerFrame = new VMStackFrame { Caller = observer, Callee = observer, CodeOwner = observer.Object, Args = new short[] { 9, 0, 0, 0 },
                    Routine = new VMRoutine { ID = 65001, Instructions = new[] { new VMInstruction { Opcode = 0, Operand = new VMSleepOperand { StackVarToDec = 0 } } } } };
                observer.Thread.Push(observerFrame);
                if (checkTree) {
                    Require(VMThread.EvaluateCheck(session.VM.Context, entity, FaultFrame(entity)) == VMPrimitiveExitCode.ERROR, "Check fault did not return ERROR.");
                } else {
                    session.RestartMain(entity.ObjectID);
                    entity.Thread.Push(FaultFrame(entity));
                    Reject<InvalidOperationException>(() => session.Tick());
                }
                Require(session.VM.ScriptExecutionStopped && reports == 1 && dialogs == 0, "Fault was hidden or legacy recovery ran.");
                Require(session.VM.ScriptFault.ToString().Contains("Unsupported TS1 generic call 255"), "Missing actionable fault context.");
                Require(entity.GetAttribute(0) == 1234 && entity.GetValue(VMStackObjectVariable.LockoutCount) == 5 &&
                    !entity.Dead && original.SequenceEqual(session.VM.Entities), "Fault reset/deleted or mutated the graph.");
                Require(observerFrame.Args[0] == 9, "Another entity ran after the fault.");
                long stoppedAt = session.VM.Context.Clock.Ticks;
                Reject<InvalidOperationException>(() => session.RestartMain(entity.ObjectID));
                for (int i = 0; i < 3; i++) Reject<InvalidOperationException>(() => session.Tick());
                session.VM.InternalTick();
                var driver = new FSO.SimAntics.NetPlay.Drivers.VMOfflineDriver();
                var command = new FaultedLotCommand();
                driver.SendCommand(command);
                Require(!driver.Tick(session.VM) && !command.Verified && !command.Executed, "Faulted VM accepted queued commands.");
                driver.CloseNet();
                Require(session.VM.Context.Clock.Ticks == stoppedAt && reports == 1, "Faulted VM kept ticking/retrying.");
                using (var fresh = TS1LotObjectSession.Load(House(paths, 2), content)) {
                    fresh.Tick(); Require(fresh.VM.ScriptFault == null && fresh.VM.Entities.Count == 388, "Fault leaked into replacement session.");
                    var commandDriver = new FSO.SimAntics.NetPlay.Drivers.VMOfflineDriver();
                    var first = new FaultedLotCommand { OnVerify = vm => VMThread.EvaluateCheck(vm.Context, vm.Entities[0], FaultFrame(vm.Entities[0])) };
                    var later = new FaultedLotCommand();
                    commandDriver.SendCommand(first); commandDriver.SendCommand(later);
                    Require(!commandDriver.Tick(fresh.VM) && first.Verified && !first.Executed && !later.Verified && !later.Executed,
                        "Command verification continued after a script fault.");
                    commandDriver.CloseNet();
                }
                log("EXPECTED FAULT CONTAINED check=" + checkTree + " REPORTS=1 RESET=0 DELETED=0");
            }
        }
        private static void ZoningFixtures(GamePaths paths)
        {
            string root = paths.GetUserDataPath("BehaviorZoningFixture");
            var isolated = new GamePaths(Path.Combine(root, "Content"), paths.GameDataRoot, Path.Combine(root, "User"));
            var store = new NeighborhoodStore(isolated, 0);
            Action<string[]> write = values => {
                var iff = new IffFile();
                var str = new STR { ChunkID = 1, ChunkLabel = "Original zoning fixture", ChunkProcessed = true };
                str.LanguageSets[0].Strings = values.Select(x => new STRItem { LanguageCode = 1, Value = x, Comment = "" }).ToArray();
                iff.AddChunk(str); store.Write("LotZoning.iff", stream => iff.Write(stream));
            };
            write(new[] { "2, community", "58, residential" });
            var content = new TS1ObjectProvider(isolated, false);
            Require(content.GetLotZoning(2) == 1 && content.GetLotZoning(58) == 0, "User overlay ignored.");
            write(new[] { "2, residential" });
            Require(content.GetLotZoning(2) == 1 && new TS1ObjectProvider(isolated, false).GetLotZoning(2) == 0, "Zoning snapshot is unstable.");
            foreach (var rows in new[] { new[] { "2, residential", "2, community" }, new[] { "2, unsupported" }, new[] { "bad row" }, new[] { "-1, residential" } }) {
                write(rows);
                Reject<InvalidDataException>(() => new TS1ObjectProvider(isolated, false).GetLotZoning(2));
            }
        }
    }
}