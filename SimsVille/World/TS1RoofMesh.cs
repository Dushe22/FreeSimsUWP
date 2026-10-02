using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace FSO.LotView
{
    // A continuous hip surface over the actual indoor footprint, including diagonal
    // corners and courtyards. Boundary distance defines hips/valleys; shared samples
    // give adjoining triangles exactly the same height.
    public static class TS1RoofMesh
    {
        public static List<Vector3[]> Build(IList<Vector2[]> footprint, float elevation, float pitch)
        {
            if (footprint == null || footprint.Count > 8192) throw new ArgumentException("Invalid roof footprint.");
            if (float.IsNaN(elevation) || float.IsInfinity(elevation) || float.IsNaN(pitch) || pitch <= 0 || pitch > 2)
                throw new ArgumentOutOfRangeException("pitch");
            var edges = new Dictionary<string, Vector2[]>();
            foreach (var triangle in footprint) {
                if (triangle == null || triangle.Length != 3) throw new InvalidDataException("Invalid roof triangle.");
                for (int i = 0; i < 3; i++) {
                    var a = triangle[i]; var b = triangle[(i + 1) % 3];
                    if (a.X != (int)a.X || a.Y != (int)a.Y || Math.Abs(a.X) > 64 || Math.Abs(a.Y) > 64)
                        throw new InvalidDataException("Roof footprint must use bounded tile corners.");
                    if (a.X > b.X || (a.X == b.X && a.Y > b.Y)) { var swap = a; a = b; b = swap; }
                    string key = a.X + ":" + a.Y + ":" + b.X + ":" + b.Y;
                    if (edges.ContainsKey(key)) edges.Remove(key); else edges.Add(key, new[] {a, b});
                }
            }
            var boundary = edges.Values.ToArray();
            var samples = new Dictionary<Vector2, Vector3>();
            Func<Vector2, Vector3> sample = p => {
                Vector3 value;
                if (samples.TryGetValue(p, out value)) return value;
                float distance = float.MaxValue;
                foreach (var edge in boundary) {
                    var span = edge[1] - edge[0];
                    float t = MathHelper.Clamp(Vector2.Dot(p - edge[0], span) / span.LengthSquared(), 0, 1);
                    distance = Math.Min(distance, Vector2.Distance(p, edge[0] + t * span));
                }
                if (boundary.Length == 0) throw new InvalidDataException("Roof has no boundary.");
                value = new Vector3(p, elevation + pitch * distance);
                samples.Add(p, value); return value;
            };
            var result = new List<Vector3[]>();
            foreach (var t in footprint) {
                var a = sample(t[0]); var b = sample(t[1]); var c = sample(t[2]);
                var ab = sample((t[0] + t[1]) / 2); var bc = sample((t[1] + t[2]) / 2); var ca = sample((t[2] + t[0]) / 2);
                result.Add(new[] {a, ab, ca}); result.Add(new[] {ab, b, bc});
                result.Add(new[] {ca, bc, c}); result.Add(new[] {ab, bc, ca});
            }
            return result;
        }
    }
}
