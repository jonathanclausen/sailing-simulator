using System.Collections.Generic;
using TillerTide.Physics.Region;
using UnityEngine;
using UnityEngine.Rendering;
using Vec2 = TillerTide.Physics.Vec2;

namespace TillerTide.App
{
    /// <summary>Builds the land as height-field mesh tiles from the region grid, with beaches and green hills.</summary>
    public static class TerrainBuilder
    {
        private const int TileCells = 96;

        public static GameObject Build(RegionGrid grid, Material material)
        {
            var root = new GameObject("Land");
            for (int ty = 0; ty < grid.Height; ty += TileCells)
                for (int tx = 0; tx < grid.Width; tx += TileCells)
                {
                    int w = Mathf.Min(TileCells, grid.Width - 1 - tx), h = Mathf.Min(TileCells, grid.Height - 1 - ty);
                    if (w < 1 || h < 1 || !HasLand(grid, tx, ty, w, h)) continue;
                    var go = new GameObject($"Land {tx},{ty}", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = Tile(grid, tx, ty, w, h);
                    var r = go.GetComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            return root;
        }

        private static bool HasLand(RegionGrid g, int tx, int ty, int w, int h)
        {
            for (int y = ty; y <= ty + h; y++)
                for (int x = tx; x <= tx + w; x++)
                    if (g.SignedDistance[y * g.Width + x] > -40) return true;
            return false;
        }

        private static Mesh Tile(RegionGrid g, int tx, int ty, int w, int h)
        {
            int nx = w + 1, ny = h + 1;
            var verts = new Vector3[nx * ny];
            var cols = new Color[nx * ny];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    var p = new Vec2(g.Min.X + (tx + x + 0.5) * g.CellSize, g.Min.Y + (ty + y + 0.5) * g.CellSize);
                    float height = Mathf.Max(-3f, (float)g.TerrainHeightAt(p));
                    verts[y * nx + x] = new Vector3((float)p.X, height, (float)p.Y);
                    float woods = Mathf.PerlinNoise((float)p.X / 180f + 31f, (float)p.Y / 180f + 7f);
                    Color land = Color.Lerp(Look.Grass, Look.Woods, Mathf.SmoothStep(0.35f, 0.7f, woods));
                    cols[y * nx + x] = height < 1.9f ? Look.Sand : Color.Lerp(Look.Sand, land, Mathf.InverseLerp(1.9f, 3.2f, height));
                }
            var idx = new List<int>(w * h * 6);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int a = y * nx + x, b = a + 1, c = a + nx, d = c + 1;
                    idx.Add(a); idx.Add(c); idx.Add(b);
                    idx.Add(b); idx.Add(c); idx.Add(d);
                }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "Land tile" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A simple Kronborg: a square castle with four corner towers, so Helsingør is recognisable from the water.</summary>
        public static GameObject Kronborg(Vector3 centre, Material stone, Material roof)
        {
            var root = new GameObject("Kronborg");
            root.transform.position = centre;
            void Block(Vector3 pos, Vector3 size, Material m, PrimitiveType type = PrimitiveType.Cube)
            {
                var b = GameObject.CreatePrimitive(type);
                Object.Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(root.transform, false);
                b.transform.localPosition = pos;
                b.transform.localScale = size;
                b.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            Block(new Vector3(0, 9, 0), new Vector3(70, 18, 70), stone);
            Block(new Vector3(0, 19, 0), new Vector3(72, 2, 72), roof);
            foreach (var c in new[] { new Vector3(-35, 0, -35), new Vector3(35, 0, -35), new Vector3(35, 0, 35), new Vector3(-35, 0, 35) })
            {
                Block(c + new Vector3(0, 14, 0), new Vector3(12, 14, 12), stone, PrimitiveType.Cylinder);
                Block(c + new Vector3(0, 31, 0), new Vector3(9, 4, 9), roof, PrimitiveType.Cylinder);
            }
            Block(new Vector3(30, 45, 30), new Vector3(3, 14, 3), roof, PrimitiveType.Cylinder); // the tall spire
            // Ramparts around the castle.
            Block(new Vector3(0, 3, 0), new Vector3(170, 6, 170), stone);
            return root;
        }
    }
}
