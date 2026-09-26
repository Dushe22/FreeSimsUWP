using System;
using System.Collections.Generic;
using System.IO;
using FSO.Common.Platform;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;

namespace FreeSims.Tests
{
    public static class LotPlacementTests
    {
        public static OBJM Load(GamePaths paths, int house)
        {
            var store = new NeighborhoodStore(paths, 0);
            var iff = new IffFile(store.GetReadPath("Houses/House" + house.ToString("00") + ".iff"));
            var simi = iff.Get<SIMI>(1);
            var map = iff.Get<OBJM>(1);
            if (simi == null || simi.GlobalData[23] < 1 || simi.GlobalData[23] > 64 || map == null)
                throw new InvalidDataException("Missing or invalid lot placement data.");
            map.ResolveTypes(iff.Get<OBJT>(0));
            if (map.ObjectData.Count == 0) throw new InvalidDataException("Expected a populated lot.");
            return map;
        }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            foreach (int house in new[] { 2, 28 })
            {
                string name = "HOUSE " + house + " OBJECT PLACEMENTS";
                try
                {
                    var map = Load(paths, house);
                    results.Add("PASS " + name);
                    log("PASS " + name + " OBJECTS=" + map.ObjectData.Count + " PAIRS=" + map.IDToOBJT.Length / 2);
                }
                catch (Exception ex) { results.Add("FAIL " + name); log("FAIL " + name + " " + ex); }
            }
            return results;
        }
    }
}