using System;
using System.Collections.Generic;
using System.IO;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    public static class TS1ParserTests
    {
        public static List<string> Run(Action<string> log)
        {
            var results = new List<string>();
            Check(results, log, "NEIGHBORHOOD WITHOUT INVENTORY", () => {
                byte[] bytes = new byte[44];
                BitConverter.GetBytes((uint)0x49).CopyTo(bytes,4);
                System.Text.Encoding.ASCII.GetBytes("HBGN").CopyTo(bytes,8);
                BitConverter.GetBytes((short)123).CopyTo(bytes,12);
                var chunk = new NGBH();
                using (var stream = new MemoryStream(bytes)) chunk.Read(new IffFile(),stream);
                Require(chunk.NeighborhoodData[0] == 123 && chunk.InventoryByID.Count == 0);
                bool rejected = false;
                using (var stream = new MemoryStream(new byte[45]))
                    try { new NGBH().Read(new IffFile(),stream); } catch (EndOfStreamException) { rejected = true; }
                Require(rejected);
            });
            Check(results, log, "NEIGHBORHOOD INVENTORY ROUNDTRIP", () => {
                var chunk = new NGBH();
                chunk.InventoryByID[7] = new List<InventoryItem> { new InventoryItem { Type=1, GUID=123, Count=2 } };
                chunk.InventoryByID[8] = new List<InventoryItem>();
                byte[] bytes;
                using (var stream = new MemoryStream()) { chunk.Write(new IffFile(),stream); bytes = stream.ToArray(); }
                var read = new NGBH();
                using (var stream = new MemoryStream(bytes)) read.Read(new IffFile(),stream);
                Require(read.InventoryByID.Count == 2 && read.InventoryByID[7][0].GUID == 123 && read.InventoryByID[7][0].Count == 2);
            });
            Check(results, log, "LEGACY OBJECT DEFINITION", () => {
                byte[] bytes = new byte[160];
                BitConverter.GetBytes((uint)136).CopyTo(bytes,0);
                BitConverter.GetBytes((uint)0x12345678).CopyTo(bytes,28);
                BitConverter.GetBytes((ushort)19).CopyTo(bytes,158);
                var chunk = new OBJD();
                using (var stream = new MemoryStream(bytes)) chunk.Read(new IffFile(),stream);
                Require(chunk.GUID == 0x12345678 && chunk.ShadowBrightness == 19 && chunk.BHAV_Repair == 0);
                bool rejected = false;
                using (var stream = new MemoryStream(bytes,0,158))
                    try { new OBJD().Read(new IffFile(),stream); } catch (EndOfStreamException) { rejected = true; }
                Require(rejected);
                var expanded = new byte[190];
                BitConverter.GetBytes((uint)138).CopyTo(expanded,0);
                BitConverter.GetBytes((ushort)99).CopyTo(expanded,160);
                using (var stream = new MemoryStream(expanded)) chunk.Read(new IffFile(),stream);
                Require(chunk.BHAV_Repair == 99);
            });
            return results;
        }
        private static void Require(bool condition) { if (!condition) throw new InvalidDataException("Parser regression."); }
        private static void Check(List<string> results, Action<string> log, string name, Action test)
        {
            try { test(); results.Add("PASS " + name); log("PASS " + name); }
            catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
        }
    }
}
