using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;
using FSO.SimAntics;
using FSO.SimAntics.Engine;
using FSO.SimAntics.Model;
using FSO.SimAntics.Primitives;

namespace FreeSims.Tests
{
    public static class ControlledSimulationTests
    {
        public const int Count = 9;
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        public static IffFile House(GamePaths paths, int number)
        { return new IffFile(new NeighborhoodStore(paths, 0).GetReadPath("Houses/House" + number.ToString("00") + ".iff")); }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            bool oldWorld = VM.UseWorld; VM.UseWorld = false;
            try {
                var content = new TS1ObjectProvider(paths);
                Action<string, Action<TS1SimulationController>> check = (name, test) => {
                    using (var sim = new TS1SimulationController()) {
                        try { sim.Load(House(paths, 28), content, 28); test(sim); results.Add("PASS " + name); }
                        catch (Exception ex) { results.Add("FAIL " + name); log(ex.ToString()); }
                        log(results.Last());
                    }
                };
                check("SUPPORTED OBJECTS START PAUSED", sim => {
                    Require(!sim.Running && sim.CompletedTicks == 0 && sim.ActiveObjects == 80 && sim.HeldObjects == 675, "Incorrect initial state.");
                    using(var raw=TS1LotObjectSession.Load(House(paths,28),content)) {
                        var clock=sim.VM.Context.Clock;var saved=raw.VM.Context.Clock;
                        Require(saved.TicksPerMinute==150&&clock.TicksPerMinute==30&&clock.Hours==saved.Hours&&clock.Minutes==saved.Minutes&&
                            clock.MinuteFractions==saved.MinuteFractions/5&&sim.VM.GlobalState[6]==clock.Seconds,
                            "Live cadence conversion changed raw import or saved clock time.");
                    }
                    Require(sim.VM.Entities.Count(e => e.Thread.ThreadBreak == VMThreadBreakMode.Pause) == 675, "Unselected threads activated.");
                    sim.Load(House(paths, 2), content, 2);
                    Require(sim.ActiveObjects == 134 && sim.HeldObjects == 254 && !sim.Running, "Incorrect flower profile.");
                });
                check("PAUSE AND SINGLE STEP", sim => {
                    long before = sim.VM.Context.Clock.Ticks;
                    sim.Advance(TimeSpan.FromHours(1), true);
                    Require(sim.CompletedTicks == 0 && sim.VM.Context.Clock.Ticks == before, "Paused clock advanced.");
                    sim.Step();
                    Require(sim.CompletedTicks == 1 && sim.VM.Context.Clock.Ticks == before + 1 && !sim.Running, "Step was not exactly one tick.");
                    sim.Run(); Reject<InvalidOperationException>(() => sim.Step()); sim.Pause();
                    Reject<ArgumentOutOfRangeException>(() => sim.Advance(TimeSpan.FromTicks(-1), true));
                });
                check("FRAME RATE INDEPENDENT CADENCE", sim => {
                    sim.Run();
                    for (int i = 0; i < 100; i++) sim.Advance(TimeSpan.FromMilliseconds(10), true);
                    Require(sim.CompletedTicks == 30, "100 Hz cadence mismatch.");
                    sim.Load(House(paths, 28), content, 28); sim.Run();
                    for (int i = 0; i < 20; i++) sim.Advance(TimeSpan.FromMilliseconds(50), true);
                    Require(sim.CompletedTicks == 30, "20 Hz cadence mismatch.");
                });
                check("FOCUS LOSS PAUSES WITHOUT CATCHUP", sim => {
                    sim.Run(); sim.Advance(TimeSpan.FromMilliseconds(20), true);
                    sim.Advance(TimeSpan.FromHours(1), false);
                    sim.Advance(TimeSpan.FromHours(1), true);
                    Require(!sim.Running && sim.CompletedTicks == 0, "Background or return advanced simulation.");
                    sim.Run(); sim.Advance(TimeSpan.FromMilliseconds(20), true);
                    Require(sim.CompletedTicks == 0, "Stale fractional time survived pause.");
                    sim.Advance(TimeSpan.FromMilliseconds(20), true);
                    Require(sim.CompletedTicks == 1, "Explicit resume failed.");
                });
                check("STALL WORK IS BOUNDED", sim => {
                    sim.Run(); sim.Advance(TimeSpan.MaxValue, true);
                    Require(sim.CompletedTicks > 0 && sim.CompletedTicks <= 5, "Stall caused unbounded catchup.");
                    int count = sim.CompletedTicks; sim.Advance(TimeSpan.Zero, true);
                    Require(sim.CompletedTicks == count, "Discarded time remained queued.");
                });
                check("LOT REPLACEMENT IS TRANSACTIONAL", sim => {
                    sim.Step(); sim.Run(); var old = sim.VM;
                    Reject<System.IO.InvalidDataException>(() => sim.Load(new IffFile(), content, 2));
                    Reject<ArgumentOutOfRangeException>(() => sim.Load(House(paths, 2), content, 99));
                    Require(sim.VM == old && sim.Running && sim.CompletedTicks == 1 && sim.House == 28, "Failed load changed active state.");
                    sim.Load(House(paths, 2), content, 2);
                    Require(sim.VM != old && !sim.Running && sim.CompletedTicks == 0 && sim.House == 2, "Replacement did not reset safely.");
                });
                check("BOTH LOTS RESPECT EXECUTION LIMIT", sim => {
                    foreach (int house in new[] { 2, 28 }) {
                        sim.Load(House(paths, house), content, house);
                        var held = sim.VM.Entities.Where(e => e.Thread.ThreadBreak == VMThreadBreakMode.Pause).ToArray();
                        sim.Run();
                        while (sim.Running) sim.Advance(TimeSpan.FromMilliseconds(100), true);
                        Require(sim.CompletedTicks == TS1SimulationController.TickLimit && sim.LimitReached && sim.Fault == null, "Limit or script failure.");
                        Require(held.All(e => e.Thread.ThreadBreak == VMThreadBreakMode.Pause) && sim.VM.Entities.Count == sim.ActiveObjects + sim.HeldObjects, "Unselected threads started or graph changed.");
                        Reject<InvalidOperationException>(() => sim.Run()); Reject<InvalidOperationException>(() => sim.Step());
                        log("CONTROLLED HOUSE=" + house + " ACTIVE=" + sim.ActiveObjects + " HELD=" + sim.HeldObjects + " TICKS=" + sim.CompletedTicks + " SCRIPT_ERRORS=0");
                    }
                });
                check("SCRIPT FAULT STOPS CONTROLLER", sim => {
                    var entity = sim.VM.Entities.First(e => e.Thread.ThreadBreak != VMThreadBreakMode.Pause);
                    entity.Thread.Push(new VMStackFrame { Caller = entity, Callee = entity, CodeOwner = entity.Object, Args = new short[4],
                        Routine = new VMRoutine { ID = 65000, Instructions = new[] { new VMInstruction { Opcode = 1, Operand = new VMGenericTSOCallOperand { Call = (VMGenericTSOCallMode)255 } } } } });
                    sim.Run(); Reject<InvalidOperationException>(() => sim.Advance(TimeSpan.FromMilliseconds(100), true));
                    Require(sim.Fault != null && sim.VM.ScriptExecutionStopped && !sim.Running && sim.CompletedTicks == 0, "Fault was hidden.");
                    Require(sim.VM.ScriptFault.ToString().Contains("Unsupported TS1 generic call 255"), "Unexpected fault source.");
                    long stopped = sim.VM.Context.Clock.Ticks;
                    sim.Advance(TimeSpan.FromSeconds(1), true);
                    Reject<InvalidOperationException>(() => sim.Run()); Reject<InvalidOperationException>(() => sim.Step());
                    Require(sim.VM.Context.Clock.Ticks == stopped, "Faulted VM advanced.");
                    sim.Load(House(paths, 28), content, 28); sim.Step();
                    Require(sim.Fault == null && sim.CompletedTicks == 1, "Fresh load did not recover.");
                });
                check("DISPOSED CONTROLLER REJECTS WORK", sim => {
                    sim.Dispose();
                    Reject<ObjectDisposedException>(() => sim.Run()); Reject<ObjectDisposedException>(() => sim.Step());
                    Reject<ObjectDisposedException>(() => sim.Advance(TimeSpan.Zero, false));
                    Reject<ObjectDisposedException>(() => sim.Load(House(paths, 2), content, 2));
                });
            } catch (Exception ex) { results.Add("FAIL CONTROLLER SETUP"); log(ex.ToString()); }
            finally { VM.UseWorld = oldWorld; }
            return results;
        }
    }
}
