using FSO.Files.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSO.Files.Formats.IFF.Chunks
{
    public struct OBJMResource
    {
        public OBJD OBJD;
        public OBJTEntry OBJT;
    }
    // Placement metadata only; saved execution state is not restored.

    public class OBJM : IffChunk
    {
        // Placement metadata only; saved execution state is not restored.

        public ushort[] IDToOBJT;

        public Dictionary<int, MappedObject> ObjectData;

        // Read the placement prefix only. Stack, relationships, slots and person state
        // remain unparsed; this is not a complete simulation/save-state decoder.
        // Format reference: FreeSO tso.files/Formats/IFF/Chunks/OBJM.cs.
        public override void Read(IffFile iff, Stream stream)
        {
            byte[] bytes;
            using (var copy = new MemoryStream()) { stream.CopyTo(copy); bytes = copy.ToArray(); }
            if (bytes.Length < 14) throw new InvalidDataException("Truncated OBJM header.");
            using (var input = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(input))
            {
                reader.ReadUInt32();
                reader.ReadUInt32(); // save version; placement prefix is shared by supported TS1 versions
                if (reader.ReadUInt32() != 0x4f626a4d) throw new InvalidDataException("Invalid OBJM signature.");
                const int offsetBase = 12;
                if (reader.ReadByte() != 1) throw new InvalidDataException("Unsupported OBJM compression.");
                var fields = new PlacementFields(bytes, 13, bytes.Length);
                var table = new List<ushort>();
                var ids = new HashSet<int>();
                while (true)
                {
                    ushort id = unchecked((ushort)fields.Short());
                    if (id == 0) break;
                    ushort type = unchecked((ushort)fields.Short());
                    if (type == 0 || !ids.Add(id)) throw new InvalidDataException("Invalid or duplicate OBJM mapping.");
                    table.Add(id); table.Add(type);
                }
                input.Position = fields.AlignedPosition;
                var objects = new Dictionary<int, MappedObject>();
                for (int record = 0; record < ids.Count; record++)
                {
                    if (input.Length - input.Position < 4) throw new InvalidDataException("Missing OBJM record boundary.");
                    long end = offsetBase + (long)reader.ReadInt32();
                    int start = (int)input.Position;
                    if (end <= start || end > bytes.Length) throw new InvalidDataException("Invalid OBJM record boundary.");
                    fields = new PlacementFields(bytes, start, (int)end);
                    for (int i = 0; i < 4; i++) fields.Int(); // footprint
                    int x = fields.Int(), y = fields.Int(), level = fields.Int();
                    if (level != 1 && level != 2) throw new InvalidDataException("Invalid OBJM object level.");
                    fields.Short(); // unknown prefix field
                    int attributes = fields.Short();
                    if (attributes < 0) throw new InvalidDataException("Negative OBJM attribute count.");
                    for (int i = 0; i < attributes + 8; i++) fields.Short(); // attributes and temporary registers
                    var data = new short[73]; // 68 object variables plus 5 extra variables
                    for (int i = 0; i < data.Length; i++) data[i] = fields.Short();
                    int id = data[11];
                    if (!ids.Contains(id) || objects.ContainsKey(id)) throw new InvalidDataException("Unknown or duplicate OBJM record ID: " + id);
                    objects.Add(id, new MappedObject {
                        ObjectID = id, Direction = data[1], ContainerID = data[2],
                        ContainerSlot = data[3], ParentID = data[26], Data = data,
                        SavedX = x, SavedY = y, SavedLevel = level
                    });
                    input.Position = end; // skip the unsupported simulation-state suffix
                }
                IDToOBJT = table.ToArray();
                ObjectData = objects;
            }
        }

        // Bit fields are MSB-first, signed, and have separate short/int widths.
        // Unlike the legacy decoder, an exhausted record throws instead of returning zeros.
        private sealed class PlacementFields
        {
            private static readonly int[] ShortWidths = { 5, 8, 13, 16 };
            private static readonly int[] IntWidths = { 6, 11, 21, 32 };
            private readonly byte[] bytes;
            private readonly int end;
            private int position, bit;
            public int AlignedPosition { get { return position + (bit == 0 ? 0 : 1); } }
            public PlacementFields(byte[] bytes, int start, int end)
            {
                this.bytes = bytes; position = start; this.end = end;
            }
            private uint Bits(int count)
            {
                uint value = 0;
                for (int i = 0; i < count; i++)
                {
                    if (position >= end) throw new InvalidDataException("Truncated OBJM field.");
                    value = (value << 1) | (uint)((bytes[position] >> (7 - bit)) & 1);
                    if (++bit == 8) { bit = 0; position++; }
                }
                return value;
            }
            private int Value(bool wide)
            {
                if (Bits(1) == 0) return 0;
                int code = (int)Bits(2);
                int width = wide ? IntWidths[code] : ShortWidths[code];
                uint value = Bits(width);
                if (width < 32 && (value & (1u << (width - 1))) != 0) value |= uint.MaxValue << width;
                return unchecked((int)value);
            }
            public short Short() { return (short)Value(false); }
            public int Int() { return Value(true); }
        }
        /// <summary>Resolve the OBJM object-ID/type-ID pairs using OBJT's explicit type IDs.</summary>
        public void ResolveTypes(OBJT types)
        {
            if (types == null || types.Entries == null || IDToOBJT == null || ObjectData == null)
                throw new InvalidDataException("Missing lot object/type table.");
            if (IDToOBJT.Length % 2 != 0)
                throw new InvalidDataException("Incomplete OBJM object/type pair.");

            var byType = new Dictionary<ushort, OBJTEntry>();
            foreach (var entry in types.Entries.Where(x => x.GUID != 0))
            {
                if (byType.ContainsKey(entry.TypeID))
                    throw new InvalidDataException("Duplicate OBJT type ID: " + entry.TypeID);
                byType.Add(entry.TypeID, entry);
            }
            var resolved = new Dictionary<int, OBJTEntry>();
            for (int i = 0; i < IDToOBJT.Length; i += 2)
            {
                int objectID = IDToOBJT[i];
                ushort typeID = IDToOBJT[i + 1];
                OBJTEntry entry;
                if (!ObjectData.ContainsKey(objectID))
                    throw new InvalidDataException("OBJM parser did not recover object ID: " + objectID);
                if (!byType.TryGetValue(typeID, out entry))
                    throw new InvalidDataException("Missing OBJT type ID: " + typeID + " for object " + objectID);
                if (resolved.ContainsKey(objectID))
                    throw new InvalidDataException("Duplicate OBJM object ID: " + objectID);
                resolved.Add(objectID, entry);
            }
            if (resolved.Count != ObjectData.Count)
                throw new InvalidDataException("OBJM contains unmapped object data.");
            // Validate the whole table before updating any object.
            foreach (var pair in resolved)
            {
                ObjectData[pair.Key].GUID = pair.Value.GUID;
                ObjectData[pair.Key].Name = pair.Value.Name;
            }
        }
        public class MappedObject {
            public string Name;
            public uint GUID;
            public int ObjectID;
            public int Direction;
            public int ParentID;

            public int ContainerID;
            public int ContainerSlot;

            public short[] Data;

            public int SavedX, SavedY, SavedLevel;
            public int ArryX;
            public int ArryY;
            public int ArryLevel;

            public override string ToString()
            {
                return Name ?? "(unreferenced)";
            }
        }

    }
}
