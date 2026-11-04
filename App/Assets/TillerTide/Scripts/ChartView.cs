using System.Collections.Generic;
using TillerTide.Physics;
using TillerTide.Physics.Racing;
using TillerTide.Physics.Region;
using TillerTide.Physics.Traffic;
using UnityEngine;

namespace TillerTide.App
{
    /// <summary>
    /// A nautical-chart style picture of the sailing area (land, depth shading, the 3 m and 6 m contours) drawn
    /// with IMGUI: a small north-up map around the boat, and a full-screen chart with buoys, course and ferries.
    /// </summary>
    public sealed class ChartView
    {
        private readonly RegionGrid _grid;
        private readonly Texture2D _tex;
        private readonly Vec2 _min, _size;
        public IReadOnlyList<Seamark> Seamarks;
        public IReadOnlyList<Ferry> Ferries;

        private static readonly Color Land = new Color(0.93f, 0.86f, 0.62f);
        private static readonly Color Shallow = new Color(0.62f, 0.82f, 0.93f);
        private static readonly Color Deep = new Color(0.96f, 0.98f, 1f);
        private static readonly Color Contour = new Color(0.3f, 0.5f, 0.7f);

        public ChartView(RegionGrid grid)
        {
            _grid = grid;
            _min = grid.Min;
            _size = grid.Max - grid.Min;
            int w = 768, h = Mathf.Clamp(Mathf.RoundToInt(768 * (float)(_size.Y / _size.X)), 64, 1024);
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var depth = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    depth[y * w + x] = (float)grid.DepthAt(Pixel(x, y, w, h));
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = depth[y * w + x];
                    Color c;
                    if (d <= 0) c = Land;
                    else
                    {
                        // Like a paper chart: blue in the shallows, white in deep water, thin contour lines.
                        c = d < 3 ? Color.Lerp(Shallow * 0.92f, Shallow, d / 3f) : d < 6 ? Shallow : Color.Lerp(Shallow, Deep, Mathf.Clamp01((d - 6) / 10f));
                        float r = x + 1 < w ? depth[y * w + x + 1] : d, u = y + 1 < h ? depth[(y + 1) * w + x] : d;
                        if (Crosses(d, r, 3) || Crosses(d, u, 3) || Crosses(d, r, 6) || Crosses(d, u, 6) || Crosses(d, r, 10) || Crosses(d, u, 10))
                            c = Color.Lerp(c, Contour, 0.6f);
                    }
                    if (d > 0 && ((x + 1 < w && depth[y * w + x + 1] <= 0) || (y + 1 < h && depth[(y + 1) * w + x] <= 0))) c = new Color(0.35f, 0.3f, 0.2f);
                    px[y * w + x] = c;
                }
            _tex.SetPixels32(px);
            _tex.Apply();
        }

        private static bool Crosses(float a, float b, float level) => b > 0 && (a - level) * (b - level) < 0;

        private Vec2 Pixel(int x, int y, int w, int h) => _min + new Vec2((x + 0.5) / w * _size.X, (y + 0.5) / h * _size.Y);

        /// <summary>North-up map of the water around the boat, <paramref name="span"/> metres across.</summary>
        public void DrawMini(Rect r, BoatState boat, CourseRunner runner, float span = 2500)
        {
            var c = boat.Position;
            var uv = new Rect((float)((c.X - span / 2 - _min.X) / _size.X), (float)((c.Y - span / 2 - _min.Y) / _size.Y),
                              (float)(span / _size.X), (float)(span / _size.Y));
            GUI.DrawTextureWithTexCoords(r, _tex, uv);
            Vector2 Map(Vec2 p) => new Vector2(r.x + (float)((p.X - c.X) / span + 0.5) * r.width, r.y + (float)(0.5 - (p.Y - c.Y) / span) * r.height);
            DrawOverlay(r, Map, boat, runner, 3f);
        }

        /// <summary>The whole area, fitted inside <paramref name="area"/>.</summary>
        public Rect DrawFull(Rect area, BoatState boat, CourseRunner runner)
        {
            float aspect = (float)(_size.X / _size.Y);
            var r = area;
            if (area.width / area.height > aspect) { r.width = area.height * aspect; r.x = area.center.x - r.width / 2; }
            else { r.height = area.width / aspect; r.y = area.center.y - r.height / 2; }
            GUI.DrawTexture(r, _tex);
            Vector2 Map(Vec2 p) => new Vector2(r.x + (float)((p.X - _min.X) / _size.X) * r.width, r.y + (float)(1 - (p.Y - _min.Y) / _size.Y) * r.height);
            DrawOverlay(r, Map, boat, runner, 2.5f);
            return r;
        }

        private void DrawOverlay(Rect clip, System.Func<Vec2, Vector2> map, BoatState boat, CourseRunner runner, float dot)
        {
            GUI.BeginClip(clip);
            Vector2 M(Vec2 p) => map(p) - clip.position;
            if (Seamarks != null)
                foreach (var s in Seamarks) Dot(M(s.Position), dot, Colour(s.Kind));
            if (runner != null)
            {
                var marks = runner.Course.Marks;
                for (int i = 0; i + 1 < marks.Count; i++) Line(M(marks[i].Position), M(marks[i + 1].Position), new Color(1f, 0.5f, 0.1f, 0.5f), 1.5f);
                if (runner.Next != null) Dot(M(runner.Next.Position), dot * 2.6f, new Color(1f, 0.45f, 0.1f));
            }
            if (Ferries != null)
                foreach (var f in Ferries)
                {
                    var a = M(f.Position + Vec2.FromBearing(f.Heading) * (Ferry.Length / 2));
                    var b = M(f.Position - Vec2.FromBearing(f.Heading) * (Ferry.Length / 2));
                    Line(a, b, new Color(0.2f, 0.25f, 0.4f), Mathf.Max(3f, (a - b).magnitude * 0.25f));
                }
            // The boat: an orange arrow along the heading.
            var bp = M(boat.Position);
            var fwd = new Vector2(Mathf.Sin((float)boat.Heading), -Mathf.Cos((float)boat.Heading));
            Line(bp - fwd * 5, bp + fwd * 9, new Color(1f, 0.42f, 0.24f), 4f);
            Dot(bp + fwd * 9, 5f, new Color(1f, 0.42f, 0.24f));
            GUI.EndClip();
        }

        private static Color Colour(SeamarkKind k)
        {
            switch (k)
            {
                case SeamarkKind.Port: return new Color(0.85f, 0.1f, 0.1f);
                case SeamarkKind.Starboard: return new Color(0.05f, 0.6f, 0.2f);
                case SeamarkKind.Special:
                case SeamarkKind.CardinalNorth: case SeamarkKind.CardinalEast:
                case SeamarkKind.CardinalSouth: case SeamarkKind.CardinalWest: return new Color(0.9f, 0.7f, 0f);
                default: return new Color(0.1f, 0.1f, 0.1f);
            }
        }

        private static void Dot(Vector2 p, float size, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>A line drawn as a row of squares, which keeps working under any GUI scale or clip.</summary>
        private static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            float len = (b - a).magnitude;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(1f, width * 0.6f)), 1, 400);
            for (int i = 0; i <= n; i++) Dot(Vector2.Lerp(a, b, i / (float)n), width, c);
        }
    }
}
