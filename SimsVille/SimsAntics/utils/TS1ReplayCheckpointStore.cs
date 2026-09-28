using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.Formats.IFF;

namespace FSO.SimAntics
{
    /// <summary>Checkpoints ONLY the bounded, deterministic controlled probe by verified replay.
    /// Never deserializes the legacy TSO VM format, writes game files, or resumes automatically.
    /// Methods run between ticks on the simulation thread. Not a general gameplay save.</summary>
    public sealed class TS1ReplayCheckpointStore
    {
        public const int RecordLength = 152;
        private readonly GamePaths paths;
        private readonly TS1ObjectProvider content;
        private readonly string revision;
        private readonly byte[] contentHash;
        public string FilePath { get; private set; }
        public bool Exists { get { return File.Exists(FilePath); } }

        public TS1ReplayCheckpointStore(GamePaths paths, string revision)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            if (revision == null || revision.Length != 40 || revision.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("An exact source revision is required.", "revision");
            this.paths = paths; this.revision = revision.ToLowerInvariant();
            FilePath = paths.GetUserDataPath("Checkpoints/controlled.bin");
            content = new TS1ObjectProvider(paths);
            // Bind all indexed sources plus the two original/overlay lots and zoning.
            // Calculated once for this content session, not on every checkpoint write.
            using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory)) {
                foreach (var relative in content.SourceFiles.OrderBy(x => x, StringComparer.Ordinal)) {
                    writer.Write(relative); writer.Write(HashFile(paths.GetGameDataPath(relative)));
                }
                var neighborhood = new NeighborhoodStore(paths, 0);
                foreach (var relative in new[] { "Houses/House02.iff", "Houses/House28.iff", "LotZoning.iff" }) {
                    writer.Write(relative); writer.Write(HashFile(neighborhood.GetReadPath(relative)));
                }
                writer.Flush(); contentHash = Hash(memory.ToArray());
            }
        }
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static byte[] HashFile(string path)
        { using (var file = File.OpenRead(path)) using (var sha = SHA256.Create()) return sha.ComputeHash(file); }

        public TS1SimulationController Fresh(int house)
        {
            if (house != 2 && house != 28) throw new ArgumentOutOfRangeException("house");
            var result = new TS1SimulationController();
            try {
                var path = new NeighborhoodStore(paths, 0).GetReadPath("Houses/House" + house.ToString("00") + ".iff");
                result.Load(new IffFile(path), content, house); return result;
            } catch { result.Dispose(); throw; }
        }
        public static byte[] StateHash(TS1SimulationController simulation)
        {
            if (simulation == null || simulation.VM == null || simulation.Fault != null || simulation.VM.ScriptExecutionStopped)
                throw new InvalidOperationException("Cannot checkpoint an absent or faulted session.");
            simulation.CheckpointReady();
            using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory)) {
                writer.Write(simulation.House); writer.Write(simulation.CompletedTicks);
                // Serialize only for a canonical comparison digest; NEVER load this as a TS1 save.
                var snapshot = simulation.VM.Save(); snapshot.Compressed = false; snapshot.SerializeInto(writer);
                var clock = simulation.VM.Context.Clock;
                writer.Write(clock.DayOfMonth); writer.Write(clock.Month); writer.Write(clock.Year);
                foreach (var entity in simulation.VM.Entities) {
                    writer.Write((int)entity.Thread.ThreadBreak);
                    writer.Write(entity.Thread.QueueDirty); writer.Write(entity.Thread.RoutineDirty);
                    writer.Write(entity.Thread.TicksThisFrame);
                }
                writer.Flush(); return Hash(memory.ToArray());
            }
        }
        public byte[] Encode(TS1SimulationController simulation)
        {
            if (simulation == null || (simulation.House != 2 && simulation.House != 28) ||
                simulation.CompletedTicks < 0 || simulation.CompletedTicks > TS1SimulationController.TickLimit)
                throw new InvalidOperationException("Unsupported controlled session.");
            using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory)) {
                writer.Write(Encoding.ASCII.GetBytes("FSRP")); writer.Write(1);
                writer.Write(Encoding.ASCII.GetBytes(revision));
                writer.Write(simulation.House); writer.Write(simulation.CompletedTicks);
                writer.Write(contentHash); writer.Write(StateHash(simulation)); writer.Flush();
                writer.Write(Hash(memory.ToArray())); writer.Flush(); return memory.ToArray();
            }
        }
        public TS1SimulationController Restore(byte[] bytes)
        {
            if (bytes == null || bytes.Length != RecordLength) throw new InvalidDataException("Invalid checkpoint length.");
            if (!Hash(bytes.Take(120).ToArray()).SequenceEqual(bytes.Skip(120))) throw new InvalidDataException("Checkpoint checksum mismatch.");
            using (var reader = new BinaryReader(new MemoryStream(bytes, false))) {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "FSRP" || reader.ReadInt32() != 1)
                    throw new InvalidDataException("Unsupported checkpoint format.");
                if (Encoding.ASCII.GetString(reader.ReadBytes(40)) != revision) throw new InvalidDataException("Checkpoint belongs to another engine revision.");
                int house = reader.ReadInt32(), ticks = reader.ReadInt32();
                if ((house != 2 && house != 28) || ticks < 0 || ticks > TS1SimulationController.TickLimit)
                    throw new InvalidDataException("Checkpoint replay range rejected.");
                if (!reader.ReadBytes(32).SequenceEqual(contentHash)) throw new InvalidDataException("Checkpoint game content has changed.");
                var expected = reader.ReadBytes(32);
                var candidate = Fresh(house);
                try {
                    for (int i = 0; i < ticks; i++) candidate.Step();
                    if (!StateHash(candidate).SequenceEqual(expected)) throw new InvalidDataException("Checkpoint replay did not reproduce the saved state.");
                    return candidate; // Paused; callers replace the active session only on success.
                } catch { candidate.Dispose(); throw; }
            }
        }
        public TS1SimulationController Read()
        {
            using (var file = File.OpenRead(FilePath)) {
                if (file.Length != RecordLength) throw new InvalidDataException("Invalid checkpoint file size.");
                var bytes = new byte[RecordLength]; int offset = 0;
                while (offset < bytes.Length) { int count = file.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new EndOfStreamException(); offset += count; }
                return Restore(bytes);
            }
        }
        public void Save(TS1SimulationController simulation)
        {
            var bytes = Encode(simulation);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    file.Write(bytes, 0, bytes.Length); file.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
