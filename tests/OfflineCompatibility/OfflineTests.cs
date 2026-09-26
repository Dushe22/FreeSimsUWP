using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;

namespace FreeSims.Tests
{
    public static class OfflineTests
    {
        public const int Count = 8;
        private sealed class Command : VMNetCommandBodyAbstract
        {
            public Func<VM,bool> VerifyAction = vm => true;
            public Action<VM> Action = vm => {};
            public override bool Verify(VM vm, VMAvatar caller) { return VerifyAction(vm); }
            public override bool Execute(VM vm, VMAvatar caller) { Action(vm); return true; }
        }
        private static VM Create(VMOfflineDriver driver)
        {
            var vm = new VM(new VMContext(null), null) { GlobalState = new short[33], TS1 = true };
            vm.VM_SetDriver(driver);
            return vm;
        }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            Check(results, log, "OFFLINE AUTHORITY AND CONTRACT", () => {
                var driver = new VMOfflineDriver();
                var vm = Create(driver);
                Require(vm.IsServer && driver.GlobalLink != null && driver.GetUserIP(1) == "local", "No local authority.");
                Require(typeof(VMNetDriver).GetMethods().All(x => x.Name != "OnPacket"), "Base contract exposes packets.");
                driver.CloseNet();
            });
            Check(results, log, "COMMAND ORDER AND VERIFICATION", () => {
                var driver = new VMOfflineDriver(); var vm = Create(driver); string seen = "";
                driver.SendCommand(new Command { Action = v => seen += "1" });
                driver.SendCommand(new Command { VerifyAction = v => false, Action = v => seen += "X" });
                driver.SendCommand(new Command { Action = v => seen += "2" });
                Require(driver.Tick(vm) && seen == "12", "Order/verification mismatch.");
                driver.Tick(vm); Require(seen == "12", "Command executed twice."); driver.CloseNet();
            });
            Check(results, log, "REENTRANT COMMANDS DEFERRED", () => {
                var driver = new VMOfflineDriver(); var vm = Create(driver); string seen = "";
                driver.SendCommand(new Command {
                    VerifyAction = v => { driver.SendCommand(new Command { Action = w => seen += "V" }); return true; },
                    Action = v => { seen += "A"; driver.SendCommand(new Command { Action = w => seen += "E" }); }
                });
                driver.Tick(vm); Require(seen == "A", "Reentrant command ran early.");
                driver.Tick(vm); Require(seen == "AVE", "Deferred command order changed."); driver.CloseNet();
            });
            Check(results, log, "NETWORK REJECTION AND SHUTDOWN", () => {
                var driver = new VMOfflineDriver(); var vm = Create(driver); bool rejected = false; bool ran = false;
                try { driver.SendCommand(new Command { FromNet = true }); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Network-origin command accepted.");
                driver.SendCommand(new Command { Action = v => ran = true });
                driver.CloseNet(); Require(!driver.Tick(vm) && !ran, "Closed driver ran queued work.");
                rejected = false;
                try { driver.SendCommand(new Command()); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "Closed driver accepted work.");
            });
            Check(results, log, "HEADLESS VM CLOCK AND ARCHITECTURE", () => {
                bool oldWorld = VM.UseWorld; VM.UseWorld = false;
                try {
                    var driver = new VMOfflineDriver(); var vm = Create(driver);
                    vm.Context.Architecture = new VMArchitecture(8,8,null,vm.Context);
                    for (int i=0; i<150; i++) driver.Tick(vm);
                    Require(vm.Context.Clock.Ticks == 150 && vm.Context.Clock.Minutes == 1 && vm.GlobalState[5] == 1,
                        "VM clock/global state did not advance.");
                    Require(vm.Context.RoomInfo != null, "Architecture did not build rooms.");
                    driver.CloseNet(); log("HEADLESS TICKS 150 - EMPTY 8X8 LOT");
                } finally { VM.UseWorld = oldWorld; }
            });
            Check(results, log, "NEIGHBORHOOD PROVIDER AND HOUSE", () => {
                var provider = new TS1NeighborhoodProvider(paths,0);
                Require(provider.Neighbors.Entries.Count > 0 && provider.GetHouse(1).List<HOUS>().Count > 0, "Missing neighborhood/house.");
                log("PROVIDER NEIGHBORS " + provider.Neighbors.Entries.Count);
            });
            Check(results, log, "SAVED OVERLAY AND FAILED SELECTION", () => {
                var scratch = new GamePaths(paths.ContentRoot,paths.GameDataRoot,paths.GetUserDataPath("OfflineProbe/" + Guid.NewGuid().ToString("N")));
                var store = new NeighborhoodStore(scratch,0);
                store.Write("Houses/House01.iff", output => { using (var input = File.OpenRead(paths.GetGameDataPath("UserData/Houses/House01.iff"))) input.CopyTo(output); });
                var provider = new TS1NeighborhoodProvider(scratch,0);
                Require(provider.GetHousePath(1).StartsWith(scratch.UserDataRoot,StringComparison.OrdinalIgnoreCase), "Saved house ignored.");
                Require(provider.GetHouse(1).List<HOUS>().Count > 0, "Overlay read failed.");
                var original = provider.MainResource; bool rejected = false;
                try { provider.InitSpecific(8); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && object.ReferenceEquals(original,provider.MainResource), "Failed selection changed active neighborhood.");
            });
            Check(results, log, "PREVIEW BLOCKS INCOMPLETE SAVES", () => {
                var provider = new TS1NeighborhoodProvider(paths,0); int rejected=0;
                try { provider.SaveNeighbourhood(true); } catch (NotSupportedException) { rejected++; }
                try { provider.SaveHouse(1,provider.GetHouse(1)); } catch (NotSupportedException) { rejected++; }
                Require(provider.IsReadOnly && rejected == 2, "Preview allowed incomplete serialization.");
            });
            log("RESULT " + results.Count(x => x.StartsWith("PASS ")) + "/" + Count + " PASS");
            return results;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Check(List<string> results, Action<string> log, string name, Action test)
        {
            try { test(); results.Add("PASS "+name); log("PASS "+name); }
            catch (Exception ex) { results.Add("FAIL "+name); log("FAIL "+name+" "+ex); }
        }
    }
}
