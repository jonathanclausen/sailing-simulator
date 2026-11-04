using System;
using System.Collections.Generic;
using System.Globalization;

namespace TillerTide.Physics.Region
{
    /// <summary>
    /// Coastline as polylines in local metres. OpenStreetMap convention: land is on the LEFT of the way's direction,
    /// water on the right.
    /// </summary>
    public sealed class Coastline
    {
        public List<Vec2[]> Lines { get; } = new List<Vec2[]>();

        public int SegmentCount
        {
            get { int n = 0; foreach (var l in Lines) n += Math.Max(0, l.Length - 1); return n; }
        }

        /// <summary>
        /// Reads the "geometry" arrays from an Overpass API JSON response (`out geom`). A small purpose-built
        /// scanner, so the physics package needs no JSON library.
        /// </summary>
        public static Coastline FromOverpassJson(string json, GeoProjection projection)
        {
            var coast = new Coastline();
            int i = 0;
            while ((i = json.IndexOf("\"geometry\"", i, StringComparison.Ordinal)) >= 0)
            {
                int start = json.IndexOf('[', i);
                if (start < 0) break;
                int depth = 0, end = start;
                for (; end < json.Length; end++)
                {
                    char ch = json[end];
                    if (ch == '[') depth++;
                    else if (ch == ']' && --depth == 0) break;
                }
                var points = new List<Vec2>();
                int j = start;
                while (true)
                {
                    int lat = json.IndexOf("\"lat\"", j, end - j, StringComparison.Ordinal);
                    if (lat < 0) break;
                    int lon = json.IndexOf("\"lon\"", lat, end - lat, StringComparison.Ordinal);
                    if (lon < 0) break;
                    double la = ReadNumber(json, lat + 5), lo = ReadNumber(json, lon + 5);
                    points.Add(projection.ToLocal(la, lo));
                    j = lon + 5;
                }
                if (points.Count >= 2) coast.Lines.Add(points.ToArray());
                i = end;
            }
            return coast;
        }

        private static double ReadNumber(string s, int from)
        {
            int k = from;
            while (k < s.Length && (s[k] == ':' || s[k] == ' ')) k++;
            int st = k;
            while (k < s.Length && (char.IsDigit(s[k]) || s[k] == '.' || s[k] == '-' || s[k] == 'e' || s[k] == 'E' || s[k] == '+')) k++;
            return double.Parse(s.Substring(st, k - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
