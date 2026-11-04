using TillerTide.Physics.Traffic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>A big double-ended car ferry: dark hull, white superstructure, funnels, lit bridges at both ends.</summary>
    public sealed class FerryVisual : MonoBehaviour
    {
        public Ferry Ferry;
        private float _roll;

        public static FerryVisual Create(Ferry ferry, Transform parent)
        {
            var go = new GameObject("Ferry " + ferry.Name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<FerryVisual>();
            v.Ferry = ferry;
            v.Build();
            return v;
        }

        private void Build()
        {
            float L = (float)Ferry.Length, B = (float)Ferry.Beam;
            var hull = Look.Lit(new Color(0.1f, 0.16f, 0.3f));
            var white = Look.Lit(new Color(0.93f, 0.93f, 0.9f));
            var red = Look.Lit(new Color(0.75f, 0.12f, 0.1f));
            var glass = Look.Lit(new Color(0.15f, 0.2f, 0.25f));
            Box(new Vector3(0, 2.5f, 0), new Vector3(B, 9, L - 6), hull);
            Box(new Vector3(0, 2.5f, L / 2 - 4), new Vector3(B * 0.8f, 9, 8), hull);   // rounded-ish ends
            Box(new Vector3(0, 2.5f, -L / 2 + 4), new Vector3(B * 0.8f, 9, 8), hull);
            Box(new Vector3(0, 7.5f, 0), new Vector3(B + 0.2f, 1f, L - 8), white);    // white band
            Box(new Vector3(0, 11.5f, 0), new Vector3(B - 2, 7, L * 0.72f), white);   // superstructure
            Box(new Vector3(0, 11.5f, 0), new Vector3(B - 1.8f, 1.6f, L * 0.7f), glass); // window row
            Box(new Vector3(0, 16.5f, L * 0.3f), new Vector3(B - 6, 3, 6), white);    // bridges at both ends
            Box(new Vector3(0, 16.5f, -L * 0.3f), new Vector3(B - 6, 3, 6), white);
            Box(new Vector3(0, 16.6f, L * 0.3f + 3.05f), new Vector3(B - 6.2f, 1.4f, 0.2f), glass);
            Box(new Vector3(0, 16.6f, -L * 0.3f - 3.05f), new Vector3(B - 6.2f, 1.4f, 0.2f), glass);
            Box(new Vector3(-6, 19f, 0), new Vector3(3, 8, 5), red);                  // funnels
            Box(new Vector3(6, 19f, 0), new Vector3(3, 8, 5), red);
        }

        private void Box(Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(transform, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            var r = b.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void Apply(WaveField waves, float t)
        {
            var p = Look.ToUnity(Ferry.Position);
            // A ship this size barely notices dinghy-sized waves: a slow, small roll only.
            _roll = Mathf.Sin(t * 0.4f + p.x * 0.01f) * 0.4f;
            transform.SetPositionAndRotation(new Vector3(p.x, waves.HeightAt(p.x, p.z, t) * 0.2f - 0.4f, p.z),
                Quaternion.Euler(0, (float)(Ferry.Heading * Mathf.Rad2Deg), _roll));
        }
    }
}
