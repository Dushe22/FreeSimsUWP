using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    // Original field-encoded fixtures. No Sims data is embedded in these tests.
    public static class OBJMPlacementTests
    {
        public static List<string> Run(Action<string> log)
        {
            var results = new List<string>();
            Check(results, log, "OBJM RECORD BOUNDARIES", () => {
                var map = Read(Fixture());
                Require(map.IDToOBJT.SequenceEqual(new ushort[] { 61, 42, 59, 7 }));
                Require(map.ObjectData.Count == 2 && map.ObjectData[61].Data[45] == 2026 && map.ObjectData[59].Data[45] == 0);
                Require(map.ObjectData[61].ContainerID == 21 && map.ObjectData[61].ContainerSlot == 4);
                Require(map.ObjectData[61].ParentID == 17 && map.ObjectData[61].Direction == 6);
            });
            Check(results, log, "OBJM SIGNED PLACEMENT FIELDS", () => {
                var map = Read(Fixture());
                var record = map.ObjectData[61];
                Require(record.SavedX == -65537 && record.SavedY == 448 && record.SavedLevel == 2);
                Require(record.Data[4] == short.MinValue && record.Data[5] == short.MaxValue && record.Data[6] == -1);
                Require(record.Data[7] == -200 && record.Data[8] == 2000);
                Require(record.Attributes.SequenceEqual(new short[] { 1997, 2026 }) && record.TempRegisters.SequenceEqual(Enumerable.Range(0, 8).Select(x => (short)x)));
            });
            Check(results, log, "OBJM MALFORMED RECORDS REJECTED", () => {
                var signature = Fixture(); signature[8] = 0;
                var compression = Fixture(); compression[12] = 0;
                foreach (var bytes in new[] { new byte[13], signature, compression, Fixture(true), Fixture(false, 61), Fixture(false, 999) })
                {
                    bool rejected = false;
                    try { Read(bytes); } catch (InvalidDataException) { rejected = true; }
                    Require(rejected);
                }
                var beyondEnd = Fixture();
                // Final record's declared end remains beyond this truncated input.
                Array.Resize(ref beyondEnd, beyondEnd.Length - 1);
                bool truncated = false;
                try { Read(beyondEnd); } catch (InvalidDataException) { truncated = true; }
                Require(truncated);
            });
            Check(results, log, "OBJM EMPTY LOT", () => {
                using (var stream = new MemoryStream())
                {
                    var writer = new BinaryWriter(stream);
                    Header(writer); writer.Write((byte)0);
                    var empty = Read(stream.ToArray());
                    Require(empty.IDToOBJT.Length == 0 && empty.ObjectData.Count == 0);
                }
            });
            return results;
        }

        private static OBJM Read(byte[] bytes)
        {
            var result = new OBJM();
            using (var stream = new MemoryStream(bytes)) result.Read(new IffFile(), stream);
            return result;
        }
        private static void Header(BinaryWriter writer)
        {
            writer.Write(0); writer.Write(0x49); writer.Write(0x4f626a4d); writer.Write((byte)1);
        }
        private static byte[] Fixture(bool shortRecord = false, int secondID = 59)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                Header(writer);
                var table = new Fields();
                foreach (int value in new[] { 61, 42, 59, 7, 0 }) table.Value(value, false);
                writer.Write(table.Bytes());
                foreach (int id in new[] { 61, secondID })
                {
                    var fields = new Fields();
                    foreach (int value in new[] { int.MinValue, -17, int.MaxValue, 65536, -65537, 448, 2 }) fields.Value(value, true);
                    foreach (int value in new[] { 1, 2, 1997, 2026 }) fields.Value(value, false);
                    for (int i = 0; i < 8; i++) fields.Value(i, false);
                    var data = new short[73];
                    data[1] = 6; data[2] = 21; data[3] = 4; data[4] = short.MinValue; data[5] = short.MaxValue;
                    data[6] = -1; data[7] = -200; data[8] = 2000; data[11] = (short)id; data[26] = 17;
                    data[45] = (short)(id == 61 ? 2026 : 0);
                    foreach (short value in data) fields.Value(value, false);
                    byte[] record = fields.Bytes();
                    if (shortRecord) record = record.Take(3).ToArray();
                    // Two suffix bytes stand for state this placement reader does not decode.
                    writer.Write((int)stream.Position + 4 + record.Length + 2 - 12);
                    writer.Write(record); writer.Write((byte)0xca); writer.Write((byte)0xfe);
                }
                return stream.ToArray();
            }
        }
        private sealed class Fields
        {
            private readonly List<byte> bytes = new List<byte>();
            private byte current;
            private int count;
            private void Bits(uint value, int width)
            {
                for (int i = width - 1; i >= 0; i--)
                {
                    current = (byte)(((uint)current << 1) | ((value >> i) & 1u));
                    if (++count == 8) { bytes.Add(current); count = 0; current = 0; }
                }
            }
            public void Value(int value, bool wide)
            {
                if (value == 0) { Bits(0, 1); return; }
                int[] widths = wide ? new[] { 6, 11, 21, 32 } : new[] { 5, 8, 13, 16 };
                int code = 0;
                while (code < 3 && (value < -(1L << (widths[code] - 1)) || value >= (1L << (widths[code] - 1)))) code++;
                Bits(1, 1); Bits((uint)code, 2); Bits(unchecked((uint)value), widths[code]);
            }
            public byte[] Bytes()
            {
                var result = new List<byte>(bytes);
                if (count != 0) result.Add((byte)(current << (8 - count)));
                return result.ToArray();
            }
        }
        private static void Require(bool value) { if (!value) throw new InvalidDataException("OBJM fixture mismatch."); }
        private static void Check(List<string> results, Action<string> log, string name, Action action)
        {
            try { action(); results.Add("PASS " + name); log("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
        }
    }
}