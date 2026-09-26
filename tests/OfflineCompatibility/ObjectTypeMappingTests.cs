using System;
using System.Collections.Generic;
using System.IO;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    internal static class ObjectTypeMappingTests
    {
        public static bool Run()
        {
            try
            {
                var types = Types();
                var map = Map(61, 42, 59, 7);
                map.ResolveTypes(types);
                Require(map.ObjectData[61].GUID == 111 && map.ObjectData[59].GUID == 222,
                    "Object IDs were confused with type IDs or list offsets.");
                var empty = new OBJM { IDToOBJT = new ushort[0], ObjectData = new Dictionary<int, OBJM.MappedObject>() };
                empty.ResolveTypes(new OBJT { Entries = new List<OBJTEntry>() });
                foreach (var bad in new[] {
                    Map(61), Map(61, 42, 59, 99), Map(61, 42, 99, 7),
                    Map(61, 42, 61, 7), Map(61, 42)
                })
                {
                    bool rejected = false;
                    try { bad.ResolveTypes(types); } catch (InvalidDataException) { rejected = true; }
                    Require(rejected, "Invalid mapping accepted.");
                    Require(bad.ObjectData[61].GUID == 0, "Invalid mapping partially changed objects.");
                }
                bool missingLotRejected = false;
                try { new VMWorldActivator(null, null).LoadFromIff(new IffFile()); }
                catch (InvalidDataException) { missingLotRejected = true; }
                Require(missingLotRejected, "Missing object data reached VM initialization.");
                Console.WriteLine("PASS OBJECT TYPE MAPPING: sparse IDs, empty lot, malformed pairs, atomic failure");
                return true;
            }
            catch (Exception ex) { Console.WriteLine("FAIL OBJECT TYPE MAPPING " + ex); return false; }
        }

        private static OBJM Map(params ushort[] table)
        {
            return new OBJM { IDToOBJT = table, ObjectData = new Dictionary<int, OBJM.MappedObject> {
                { 61, new OBJM.MappedObject { ObjectID = 61 } },
                { 59, new OBJM.MappedObject { ObjectID = 59 } }
            } };
        }
        private static OBJT Types()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write(0); writer.Write(2); writer.Write(0);
                WriteType(writer, 111, 42); WriteType(writer, 222, 7);
                writer.Flush(); stream.Position = 0;
                var result = new OBJT(); result.Read(new IffFile(), stream); return result;
            }
        }
        private static void WriteType(BinaryWriter writer, uint guid, ushort type)
        {
            writer.Write(guid);
            for (int i = 0; i < 4; i++) writer.Write((ushort)0);
            writer.Write(type); writer.Write((ushort)0);
            writer.Write((byte)'A'); writer.Write((byte)0);
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
        }
    }
}