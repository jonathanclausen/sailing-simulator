using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TillerTide.Physics.Region
{
    /// <summary>What a buoy or beacon means to a sailor (IALA region A, as in Denmark).</summary>
    public enum SeamarkKind
    {
        Other,
        Port,              // red can: keep it on your port side going into harbour / with the buoyage direction
        Starboard,         // green cone
        CardinalNorth,     // safe water lies north of it
        CardinalEast,
        CardinalSouth,
        CardinalWest,
        IsolatedDanger,
        SafeWater,
        Special,           // yellow: race marks, outfalls, restricted areas
    }

    public sealed class Seamark
    {
        public Vec2 Position;
        public SeamarkKind Kind;
        public bool IsBeacon;      // fixed on the bottom rather than floating
        public string Name = "";
        public string Colour = ""; // OpenSeaMap colour list, e.g. "red" or "black;yellow;black"
        public string Shape = "";  // can, conical, pillar, spar, stake, tower...

        public override string ToString() => string.IsNullOrEmpty(Name) ? Kind.ToString() : $"{Name} ({Kind})";
    }

    /// <summary>Buoys and beacons from OpenSeaMap (OpenStreetMap seamark tags), read from an Overpass response.</summary>
    public static class Seamarks
    {
        public static string OverpassQuery(GeoBounds b) =>
            FormattableString.Invariant(
                $"[out:json][timeout:60];node[\"seamark:type\"~\"^(buoy|beacon)_\"]({b.South},{b.West},{b.North},{b.East});out;");

        public static List<Seamark> FromOverpassJson(string json, GeoProjection projection)
        {
            var list = new List<Seamark>();
            int arr = json.IndexOf("\"elements\"", StringComparison.Ordinal);
            if (arr < 0) return list;
            int i = json.IndexOf('[', arr);
            if (i < 0) return list;
            i++;
            while (true)
            {
                int open = NextNonSpace(json, i);
                if (open < 0 || json[open] == ']') break;
                if (json[open] == ',') { i = open + 1; continue; }
                if (json[open] != '{') break;
                int close = MatchBrace(json, open);
                if (close < 0) break;
                var mark = ParseElement(json, open, close, projection);
                if (mark != null) list.Add(mark);
                i = close + 1;
            }
            return list;
        }

        private static Seamark ParseElement(string s, int open, int close, GeoProjection projection)
        {
            double? lat = null, lon = null;
            Dictionary<string, string> tags = null;
            int i = open + 1;
            while (i < close)
            {
                int k = s.IndexOf('"', i, close - i);
                if (k < 0) break;
                string key = ReadString(s, k, out int after);
                int colon = s.IndexOf(':', after, close - after);
                if (colon < 0) break;
                int v = NextNonSpace(s, colon + 1);
                char c = s[v];
                if (c == '{')
                {
                    int end = MatchBrace(s, v);
                    if (key == "tags") tags = ReadObject(s, v, end);
                    i = end + 1;
                }
                else if (c == '"') { ReadString(s, v, out i); }
                else if (c == '[') { i = MatchBracket(s, v) + 1; }
                else
                {
                    int e = v;
                    while (e < close && s[e] != ',' && s[e] != '}' && !char.IsWhiteSpace(s[e])) e++;
                    string num = s.Substring(v, e - v);
                    if (key == "lat") lat = double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
                    else if (key == "lon") lon = double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
                    i = e;
                }
            }
            if (lat == null || lon == null || tags == null) return null;
            if (!tags.TryGetValue("seamark:type", out var type)) return null;
            string Tag(string suffix) => tags.TryGetValue($"seamark:{type}:{suffix}", out var t) ? t : "";

            var m = new Seamark
            {
                Position = projection.ToLocal(lat.Value, lon.Value),
                IsBeacon = type.StartsWith("beacon", StringComparison.Ordinal),
                Colour = Tag("colour"),
                Shape = Tag("shape"),
            };
            m.Name = tags.TryGetValue("seamark:name", out var n) ? n : tags.TryGetValue("name", out n) ? n : "";
            string cat = Tag("category");
            if (type.EndsWith("_lateral", StringComparison.Ordinal))
                m.Kind = cat == "port" || (cat == "" && m.Colour.StartsWith("red", StringComparison.Ordinal)) ? SeamarkKind.Port
                       : cat == "starboard" || (cat == "" && m.Colour.StartsWith("green", StringComparison.Ordinal)) ? SeamarkKind.Starboard
                       : SeamarkKind.Other;
            else if (type.EndsWith("_cardinal", StringComparison.Ordinal))
                m.Kind = cat == "north" ? SeamarkKind.CardinalNorth : cat == "east" ? SeamarkKind.CardinalEast
                       : cat == "south" ? SeamarkKind.CardinalSouth : cat == "west" ? SeamarkKind.CardinalWest : SeamarkKind.Other;
            else if (type.EndsWith("_isolated_danger", StringComparison.Ordinal)) m.Kind = SeamarkKind.IsolatedDanger;
            else if (type.EndsWith("_safe_water", StringComparison.Ordinal)) m.Kind = SeamarkKind.SafeWater;
            else if (type.EndsWith("_special_purpose", StringComparison.Ordinal)) m.Kind = SeamarkKind.Special;
            else m.Kind = SeamarkKind.Other;
            return m;
        }

        private static Dictionary<string, string> ReadObject(string s, int open, int close)
        {
            var d = new Dictionary<string, string>();
            int i = open + 1;
            while (i < close)
            {
                int k = s.IndexOf('"', i, close - i);
                if (k < 0) break;
                string key = ReadString(s, k, out int after);
                int v = s.IndexOf('"', after, close - after);
                if (v < 0) break;
                d[key] = ReadString(s, v, out i);
            }
            return d;
        }

        /// <summary>Reads a JSON string starting at the opening quote; returns the index after the closing quote.</summary>
        private static string ReadString(string s, int quote, out int after)
        {
            var sb = new StringBuilder();
            int i = quote + 1;
            for (; i < s.Length && s[i] != '"'; i++)
            {
                if (s[i] != '\\') { sb.Append(s[i]); continue; }
                char e = s[++i];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            after = i + 1;
            return sb.ToString();
        }

        private static int NextNonSpace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i < s.Length ? i : -1;
        }

        private static int MatchBrace(string s, int open) => Match(s, open, '{', '}');
        private static int MatchBracket(string s, int open) => Match(s, open, '[', ']');

        private static int Match(string s, int open, char o, char c)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"') { ReadString(s, i, out int after); i = after - 1; }
                else if (ch == o) depth++;
                else if (ch == c && --depth == 0) return i;
            }
            return -1;
        }
    }
}
