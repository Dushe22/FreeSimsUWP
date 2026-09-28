using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FSO.Common.Platform;
using FSO.SimAntics;

namespace FreeSims.Tests
{
    public static class ReplayCheckpointTests
    {
        public const int Count = 8;
        private const string Revision = "1111111111111111111111111111111111111111";
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Reject<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
        private static void Ticks(TS1SimulationController sim, int count) { for (int i = 0; i < count; i++) sim.Step(); }
        private static void Same(TS1SimulationController a, TS1SimulationController b)
        { Require(TS1ReplayCheckpointStore.StateHash(a).SequenceEqual(TS1ReplayCheckpointStore.StateHash(b)), "Replay state mismatch."); }
        private static byte[] Changed(byte[] original, int index, byte value)
        {
            var result = (byte[])original.Clone(); result[index] = value;
            using (var sha = SHA256.Create()) Array.Copy(sha.ComputeHash(result, 0, 120), 0, result, 120, 32);
            return result;
        }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>(); bool oldWorld = VM.UseWorld; VM.UseWorld = false;
            var isolated = new GamePaths(paths.ContentRoot, paths.GameDataRoot, paths.GetUserDataPath("ReplayTests/" + Guid.NewGuid().ToString("N")));
            Action<string, Action> check = (name, action) => {
                try { action(); results.Add("PASS " + name); }
                catch (Exception ex) { results.Add("FAIL " + name); log(ex.ToString()); }
                log(results.Last());
            };
            try {
                var store = new TS1ReplayCheckpointStore(isolated, Revision);
                check("BOTH LOTS CHECKPOINT ROUNDTRIP", () => {
                    foreach (int house in new[] { 2, 28 }) using (var original = store.Fresh(house)) {
                        foreach (int ticks in new[] { 0, 76 }) {
                            Ticks(original, ticks);
                            var bytes = store.Encode(original); Require(bytes.Length == TS1ReplayCheckpointStore.RecordLength, "Unexpected record length.");
                            using (var restored = store.Restore(bytes)) {
                                Require(!restored.Running && restored.CompletedTicks == ticks && restored.House == house, "Restored controls incorrect.");
                                Same(original, restored);
                            }
                        }
                        log("REPLAY HOUSE=" + house + " TICKS=76 MATCHED OBJECTS STACKS CLOCK RNG");
                    }
                });
                check("RESTORED BEHAVIORS CONTINUE IDENTICALLY", () => {
                    foreach (int house in new[] { 2, 28 }) using (var original = store.Fresh(house)) {
                        Ticks(original, 1561); store.Save(original);
                        using (var restored = store.Read()) {
                            Same(original, restored);
                            Ticks(original, 300); Ticks(restored, 300); Same(original, restored);
                            log("CONTINUATION HOUSE=" + house + " SAVED=1561 FINAL=1861 MATCHED");
                        }
                    }
                });
                check("EXECUTION LIMIT SURVIVES RESTORE", () => {
                    using (var original = store.Fresh(28)) {
                        Ticks(original, 6000); store.Save(original);
                        using (var restored = store.Read()) {
                            Same(original, restored);
                            Require(restored.LimitReached && !restored.Running, "Limit lost.");
                            Reject<InvalidOperationException>(() => restored.Run());
                            Reject<InvalidOperationException>(() => restored.Step());
                        }
                    }
                });
                check("CORRUPT AND UNBOUNDED RECORDS REJECTED", () => {
                    using (var sim = store.Fresh(28)) {
                        var bytes = store.Encode(sim);
                        foreach (var bad in new[] { new byte[0], new byte[151], new byte[153], new byte[152], Changed(bytes, 4, 255), Changed(bytes, 48, 99), Changed(bytes, 55, 127) })
                            Reject<InvalidDataException>(() => store.Restore(bad));
                        var damaged = (byte[])bytes.Clone(); damaged[119] ^= 1;
                        Reject<InvalidDataException>(() => store.Restore(damaged));
                        Require(sim.CompletedTicks == 0 && sim.Fault == null, "Failed restore touched active session.");
                        sim.Dispose(); Reject<ObjectDisposedException>(() => store.Encode(sim));
                    }
                });
                check("REVISION AND CONTENT IDENTITY REQUIRED", () => {
                    using (var sim = store.Fresh(28)) {
                        var bytes = store.Encode(sim);
                        Reject<InvalidDataException>(() => store.Restore(Changed(bytes, 8, (byte)'2')));
                        Reject<InvalidDataException>(() => store.Restore(Changed(bytes, 56, (byte)(bytes[56] ^ 1))));
                    }
                });
                check("DIVERGENT STATE NEVER REPLACES SESSION", () => {
                    using (var sim = store.Fresh(2)) {
                        Ticks(sim, 76); var bytes = store.Encode(sim);
                        Reject<InvalidDataException>(() => store.Restore(Changed(bytes, 88, (byte)(bytes[88] ^ 1))));
                        Require(sim.CompletedTicks == 76 && sim.Fault == null, "Active session changed.");
                        sim.VM.GlobalState[0]++;
                        Reject<InvalidDataException>(() => store.Restore(store.Encode(sim)));
                    }
                });
                check("FAILED WRITE PRESERVES CHECKPOINT", () => {
                    using (var sim = store.Fresh(28)) {
                        store.Save(sim); var original = File.ReadAllBytes(store.FilePath); sim.Step();
                        using (var locked = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                            Reject<IOException>(() => store.Save(sim));
                        Require(original.SequenceEqual(File.ReadAllBytes(store.FilePath)), "Previous checkpoint damaged.");
                        Require(Directory.GetFiles(Path.GetDirectoryName(store.FilePath), "*.tmp").Length == 0, "Temporary file leaked.");
                        store.Save(sim);
                        using (var restored = store.Read()) Require(restored.CompletedTicks == 1, "Atomic replacement failed.");
                    }
                });
                check("CHANGED SOURCE OVERLAY REJECTED", () => {
                    using (var sim = store.Fresh(28)) {
                        var bytes = store.Encode(sim);
                        var overlay = new NeighborhoodStore(isolated, 0);
                        var source = File.ReadAllBytes(overlay.GetReadPath("Houses/House28.iff"));
                        overlay.Write("Houses/House28.iff", stream => { stream.Write(source, 0, source.Length); stream.WriteByte(0); });
                        var changed = new TS1ReplayCheckpointStore(isolated, Revision);
                        Reject<InvalidDataException>(() => changed.Restore(bytes));
                        Require(store.FilePath.StartsWith(isolated.UserDataRoot, StringComparison.OrdinalIgnoreCase), "Checkpoint escaped user root.");
                    }
                });
            } catch (Exception ex) { results.Add("FAIL CHECKPOINT SETUP"); log(ex.ToString()); }
            finally { VM.UseWorld = oldWorld; }
            return results;
        }
    }
}
