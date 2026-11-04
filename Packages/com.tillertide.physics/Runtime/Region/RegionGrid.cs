using System;
using System.Collections.Generic;

namespace TillerTide.Physics.Region
{
    /// <summary>
    /// Signed distance to the coastline on a regular grid (positive on land, negative at sea).
    /// From it come the land mask for collisions, an estimated water depth and a land height for the terrain mesh.
    /// </summary>
    public sealed class RegionGrid : IDepthField
    {
        public Vec2 Min { get; }
        public double CellSize { get; }
        public int Width { get; }
        public int Height { get; }
        public float[] SignedDistance { get; }

        /// <summary>Depth rises this many metres per metre away from the shore, capped at MaxDepth.</summary>
        public double ShoreSlope { get; set; } = 0.08;
        public double MaxDepth { get; set; } = 30;
        public double MaxLandHeight { get; set; } = 16;

        private RegionGrid(Vec2 min, double cell, int w, int h, float[] sd)
        {
            Min = min; CellSize = cell; Width = w; Height = h; SignedDistance = sd;
        }

        public Vec2 Max => Min + new Vec2(Width * CellSize, Height * CellSize);

        /// <summary>
        /// Builds the grid from coastline polylines. Cells further than <paramref name="searchRadius"/> from any
        /// coastline take the side of their neighbours.
        /// </summary>
        public static RegionGrid Build(Coastline coast, Vec2 min, Vec2 max, double cellSize = 25, double searchRadius = 1200)
        {
            int w = Math.Max(2, (int)Math.Ceiling((max.X - min.X) / cellSize));
            int h = Math.Max(2, (int)Math.Ceiling((max.Y - min.Y) / cellSize));
            var sd = new float[w * h];
            var segs = new List<Seg>();
            foreach (var line in coast.Lines)
                for (int i = 0; i + 1 < line.Length; i++)
                    segs.Add(new Seg { A = line[i], B = line[i + 1], Prev = i > 0 ? line[i - 1] : (Vec2?)null, Next = i + 2 < line.Length ? line[i + 2] : (Vec2?)null });

            // Bin segments so each cell only looks at nearby ones.
            double bin = Math.Max(cellSize * 8, 200);
            int bw = (int)Math.Ceiling((max.X - min.X + 2 * searchRadius) / bin) + 1;
            int bh = (int)Math.Ceiling((max.Y - min.Y + 2 * searchRadius) / bin) + 1;
            var bins = new List<int>[bw * bh];
            Vec2 bmin = min - new Vec2(searchRadius, searchRadius);
            for (int s = 0; s < segs.Count; s++)
            {
                var a = segs[s].A; var b = segs[s].B;
                int x0 = Clamp((int)((Math.Min(a.X, b.X) - bmin.X) / bin), 0, bw - 1), x1 = Clamp((int)((Math.Max(a.X, b.X) - bmin.X) / bin), 0, bw - 1);
                int y0 = Clamp((int)((Math.Min(a.Y, b.Y) - bmin.Y) / bin), 0, bh - 1), y1 = Clamp((int)((Math.Max(a.Y, b.Y) - bmin.Y) / bin), 0, bh - 1);
                for (int by = y0; by <= y1; by++)
                    for (int bx = x0; bx <= x1; bx++)
                        (bins[by * bw + bx] ??= new List<int>()).Add(s);
            }

            int reach = (int)Math.Ceiling(searchRadius / bin);
            var known = new bool[w * h];
            for (int cy = 0; cy < h; cy++)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    var p = new Vec2(min.X + (cx + 0.5) * cellSize, min.Y + (cy + 0.5) * cellSize);
                    int pbx = (int)((p.X - bmin.X) / bin), pby = (int)((p.Y - bmin.Y) / bin);
                    double best = double.MaxValue; int bestSeg = -1; double bestT = 0;
                    // Search rings of bins outwards; stop once nothing in a further ring can be closer.
                    for (int ring = 0; ring <= reach; ring++)
                    {
                        for (int by = pby - ring; by <= pby + ring; by++)
                        {
                            if (by < 0 || by >= bh) continue;
                            bool edgeRow = by == pby - ring || by == pby + ring;
                            for (int bx = pbx - ring; bx <= pbx + ring; bx += edgeRow ? 1 : 2 * Math.Max(ring, 1))
                            {
                                if (bx < 0 || bx >= bw) continue;
                                var list = bins[by * bw + bx];
                                if (list == null) continue;
                                foreach (int s in list)
                                {
                                    double d2 = DistSq(p, segs[s].A, segs[s].B, out double t);
                                    if (d2 < best) { best = d2; bestSeg = s; bestT = t; }
                                }
                            }
                        }
                        double safe = ring * bin;
                        if (bestSeg >= 0 && best <= safe * safe) break;
                    }
                    int idx = cy * w + cx;
                    if (bestSeg < 0 || best > searchRadius * searchRadius) continue;
                    bool land = IsLandSide(p, segs[bestSeg], bestT);
                    float d = (float)Math.Sqrt(best);
                    sd[idx] = land ? d : -d;
                    known[idx] = true;
                }
            }
            FillUnknown(sd, known, w, h, (float)searchRadius);
            return new RegionGrid(min, cellSize, w, h, sd);
        }

        /// <summary>Sea everywhere: used when no map could be loaded.</summary>
        public static RegionGrid OpenSea(Vec2 min, Vec2 max, double cellSize = 100)
        {
            int w = Math.Max(2, (int)Math.Ceiling((max.X - min.X) / cellSize));
            int h = Math.Max(2, (int)Math.Ceiling((max.Y - min.Y) / cellSize));
            var sd = new float[w * h];
            for (int i = 0; i < sd.Length; i++) sd[i] = -5000;
            return new RegionGrid(min, cellSize, w, h, sd);
        }

        /// <summary>Signed distance to the shore (m), bilinear between cell centres. Outside the grid counts as open sea.</summary>
        public double SignedDistanceAt(Vec2 p)
        {
            double fx = (p.X - Min.X) / CellSize - 0.5, fy = (p.Y - Min.Y) / CellSize - 0.5;
            if (fx < -1 || fy < -1 || fx > Width || fy > Height) return -MaxDepth / ShoreSlope;
            int x0 = Clamp((int)Math.Floor(fx), 0, Width - 1), y0 = Clamp((int)Math.Floor(fy), 0, Height - 1);
            int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1);
            double tx = Angles.Clamp(fx - x0, 0, 1), ty = Angles.Clamp(fy - y0, 0, 1);
            double a = SignedDistance[y0 * Width + x0], b = SignedDistance[y0 * Width + x1];
            double c = SignedDistance[y1 * Width + x0], d = SignedDistance[y1 * Width + x1];
            return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty;
        }

        public bool IsLand(Vec2 p) => SignedDistanceAt(p) > 0;

        /// <summary>Measured depths, when they could be loaded. Without them depth is estimated from the shore distance.</summary>
        public Bathymetry Bathymetry { get; private set; }
        private GeoProjection _bathyProjection;

        /// <summary>How far out from the beach (m) the estimated shelf blends into the measured depths.</summary>
        public double BlendDistance { get; set; } = 300;

        public void SetBathymetry(Bathymetry bathymetry, GeoProjection projection)
        {
            Bathymetry = bathymetry;
            _bathyProjection = projection;
        }

        /// <summary>Depth in metres; zero or negative on land. Uses real soundings when loaded.</summary>
        public double DepthAt(Vec2 p)
        {
            double sd = SignedDistanceAt(p);
            if (sd >= 0) return -sd;
            double shelf = Math.Min(MaxDepth, -sd * ShoreSlope - 0.5);
            if (Bathymetry == null) return shelf;
            var (lat, lon) = _bathyProjection.ToGeo(p);
            double? measured = Bathymetry.DepthAt(lat, lon);
            if (measured == null) return shelf;
            // The coastline is sharper than the ~200 m soundings, so near the beach the shelf from the coastline
            // limits the depth (a real shoal still wins), and further out the soundings take over.
            double real = Math.Max(0.3, measured.Value);
            double w = Angles.Clamp(-sd / BlendDistance, 0, 1);
            w = w * w * (3 - 2 * w);
            return Math.Min(shelf, real) * (1 - w) + real * w;
        }

        /// <summary>Terrain height above sea level (m). Negative values are the sea bed.</summary>
        public double TerrainHeightAt(Vec2 p)
        {
            double sd = SignedDistanceAt(p);
            if (sd < 0) return -Math.Max(0.3, DepthAt(p));
            double rolling = 0.5 + 0.5 * Math.Sin(p.X / 310) * Math.Sin(p.Y / 270);
            return 1.2 + (MaxLandHeight * (0.6 + 0.4 * rolling)) * (1 - Math.Exp(-sd / 220));
        }

        /// <summary>Nearest point with at least <paramref name="minDepth"/> of water, searching outwards.</summary>
        public Vec2 NearestWater(Vec2 p, double minDepth = 3)
        {
            if (DepthAt(p) >= minDepth) return p;
            for (double r = CellSize; r < 5000; r += CellSize)
                for (int k = 0; k < 32; k++)
                {
                    var q = p + Vec2.FromBearing(k * Math.PI / 16) * r;
                    if (DepthAt(q) >= minDepth) return q;
                }
            return p;
        }

        private struct Seg { public Vec2 A, B; public Vec2? Prev, Next; }

        private static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        private static bool IsLandSide(Vec2 p, Seg s, double t)
        {
            // Away from the ends the segment decides. At a vertex both segments meeting there are used, so convex and
            // concave corners are both classified correctly.
            if (t > 1e-6 && t < 1 - 1e-6) return Cross(s.B - s.A, p - s.A) > 0;
            if (t <= 1e-6 && s.Prev.HasValue) return Corner(s.Prev.Value, s.A, s.B, p);
            if (t >= 1 - 1e-6 && s.Next.HasValue) return Corner(s.A, s.B, s.Next.Value, p);
            return Cross(s.B - s.A, p - s.A) > 0;
        }

        private static bool Corner(Vec2 a, Vec2 v, Vec2 b, Vec2 p)
        {
            bool l1 = Cross(v - a, p - a) > 0, l2 = Cross(b - v, p - v) > 0;
            bool leftTurn = Cross(v - a, b - v) > 0;
            return leftTurn ? (l1 && l2) : (l1 || l2);
        }

        private static double DistSq(Vec2 p, Vec2 a, Vec2 b, out double t)
        {
            var ab = b - a;
            double len2 = ab.LengthSquared;
            t = len2 > 1e-12 ? Angles.Clamp(Vec2.Dot(p - a, ab) / len2, 0, 1) : 0;
            var q = a + ab * t;
            return (p - q).LengthSquared;
        }

        private static void FillUnknown(float[] sd, bool[] known, int w, int h, float far)
        {
            var queue = new Queue<int>();
            for (int i = 0; i < sd.Length; i++) if (known[i]) queue.Enqueue(i);
            bool any = queue.Count > 0;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                float sign = sd[i] >= 0 ? 1 : -1;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (known[j]) continue;
                    known[j] = true;
                    sd[j] = sign * far;
                    queue.Enqueue(j);
                }
            }
            if (!any) for (int i = 0; i < sd.Length; i++) sd[i] = -far;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
