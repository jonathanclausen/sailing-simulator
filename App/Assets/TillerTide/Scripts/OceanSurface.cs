using TillerTide.Physics.Region;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>
    /// A fine grid of water that follows the camera (snapped to its cell size so waves don't swim), plus a large
    /// flat skirt out to the horizon. Waves are displaced in the shader.
    /// </summary>
    public sealed class OceanSurface : MonoBehaviour
    {
        public Transform Follow;
        private Material _material;
        private const float Cell = 2.5f;
        private const int Cells = 240; // 600 m across

        public void Init(Material material)
        {
            _material = material;
            var near = new GameObject("Ocean near", typeof(MeshFilter), typeof(MeshRenderer));
            near.transform.SetParent(transform, false);
            near.GetComponent<MeshFilter>().sharedMesh = Grid(Cells, Cell);
            var nr = near.GetComponent<MeshRenderer>();
            nr.sharedMaterial = material;
            nr.shadowCastingMode = ShadowCastingMode.Off;

            var far = new GameObject("Ocean far", typeof(MeshFilter), typeof(MeshRenderer));
            far.transform.SetParent(transform, false);
            far.transform.localPosition = new Vector3(0, -0.15f, 0);
            far.GetComponent<MeshFilter>().sharedMesh = Skirt(Cells * Cell * 0.5f - Cell, 9000f);
            var fr = far.GetComponent<MeshRenderer>();
            fr.sharedMaterial = material;
            fr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Give the shader a texture of how shallow the water is, so shoals show up green-blue.</summary>
        public void SetRegion(RegionGrid grid)
        {
            int w = Mathf.Min(grid.Width, 1024), h = Mathf.Min(grid.Height, 1024);
            var tex = new Texture2D(w, h, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var size = grid.Max - grid.Min;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = grid.Min + new Physics.Vec2((x + 0.5) / w * size.X, (y + 0.5) / h * size.Y);
                    double depth = grid.DepthAt(p);
                    float shallow = depth <= 0 ? 1f : Mathf.Clamp01(1f - (float)depth / 9f);
                    px[y * w + x] = new Color32((byte)(shallow * 255), 0, 0, 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _material.SetTexture("_RegionTex", tex);
            _material.SetVector("_RegionRect", new Vector4((float)grid.Min.X, (float)grid.Min.Y, (float)size.X, (float)size.Y));
        }

        private void LateUpdate()
        {
            if (Follow == null) return;
            var p = Follow.position;
            transform.position = new Vector3(Mathf.Round(p.x / Cell) * Cell, 0, Mathf.Round(p.z / Cell) * Cell);
        }

        private static Mesh Grid(int cells, float cell)
        {
            int n = cells + 1;
            var verts = new Vector3[n * n];
            float half = cells * cell * 0.5f;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    verts[z * n + x] = new Vector3(x * cell - half, 0, z * cell - half);
            var idx = new int[cells * cells * 6];
            int k = 0;
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
                    idx[k++] = a; idx[k++] = c; idx[k++] = b;
                    idx[k++] = b; idx[k++] = c; idx[k++] = d;
                }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "Ocean grid" };
            mesh.vertices = verts;
            mesh.triangles = idx;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(cells * cell + 20, 10, cells * cell + 20));
            return mesh;
        }

        /// <summary>Square ring from the near grid's edge out to the horizon.</summary>
        private static Mesh Skirt(float inner, float outer)
        {
            var v = new[]
            {
                new Vector3(-outer, 0, -outer), new Vector3(outer, 0, -outer), new Vector3(outer, 0, outer), new Vector3(-outer, 0, outer),
                new Vector3(-inner, 0, -inner), new Vector3(inner, 0, -inner), new Vector3(inner, 0, inner), new Vector3(-inner, 0, inner),
            };
            var t = new[] { 0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 6, 2, 6, 3, 3, 6, 7, 3, 7, 0, 0, 7, 4 };
            var mesh = new Mesh { name = "Ocean skirt", vertices = v, triangles = t };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
