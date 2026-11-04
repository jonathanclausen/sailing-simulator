using TillerTide.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>
    /// Procedural dinghy: hull, deck, mast, boom, a sail that bellies, luffs and twists from the physics,
    /// a sailor who hikes, a rudder and a foam wake. The physics says where the boat is; the waves add
    /// heave and pitch on top.
    /// Boat-local axes: +Z forward, +X starboard, +Y up. Physics boat frame X forward maps to local +Z.
    /// </summary>
    public sealed class BoatVisual : MonoBehaviour
    {
        private Transform _heel, _boom, _crew, _rudder, _vane;
        private Mesh _sailMesh;
        private Vector3[] _sailVerts;
        private const int SailCols = 10, SailRows = 12;
        private BoatConfig _cfg;
        private float _heave, _pitch, _roll;

        public void Build(BoatConfig cfg)
        {
            _cfg = cfg;
            _heel = new GameObject("Heel").transform;
            _heel.SetParent(transform, false);

            var hullMat = Look.Lit(Look.HullOrange);
            var deckMat = Look.Lit(new Color(0.95f, 0.95f, 0.92f));
            var sparMat = Look.Lit(new Color(0.18f, 0.22f, 0.26f));
            var sailMat = Look.Lit(new Color(0.98f, 0.97f, 0.93f), doubleSided: true);

            Part("Hull", HullMesh(4.2f, 1.45f, 0.42f, deck: false), hullMat, _heel);
            Part("Deck", HullMesh(4.2f, 1.45f, 0.42f, deck: true), deckMat, _heel);

            float mastZ = (float)cfg.MastX;
            var mast = Cylinder("Mast", 0.045f, (float)(cfg.BoomHeight + cfg.LuffLength + 0.3), sparMat, _heel);
            mast.localPosition = new Vector3(0, 0.3f, mastZ);

            _boom = new GameObject("Boom").transform;
            _boom.SetParent(_heel, false);
            _boom.localPosition = new Vector3(0, (float)cfg.BoomHeight, mastZ);
            var boomSpar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(boomSpar.GetComponent<Collider>());
            boomSpar.transform.SetParent(_boom, false);
            boomSpar.transform.localScale = new Vector3(0.06f, 0.06f, (float)cfg.FootLength);
            boomSpar.transform.localPosition = new Vector3(0, -0.04f, -(float)cfg.FootLength / 2);
            boomSpar.GetComponent<MeshRenderer>().sharedMaterial = sparMat;

            _sailMesh = new Mesh { name = "Sail" };
            _sailMesh.MarkDynamic();
            _sailVerts = new Vector3[(SailCols + 1) * (SailRows + 1)];
            var tris = new int[SailCols * SailRows * 6];
            int k = 0;
            for (int r = 0; r < SailRows; r++)
                for (int c = 0; c < SailCols; c++)
                {
                    int a = r * (SailCols + 1) + c, b = a + 1, d = a + SailCols + 1, e = d + 1;
                    tris[k++] = a; tris[k++] = d; tris[k++] = b; tris[k++] = b; tris[k++] = d; tris[k++] = e;
                }
            _sailMesh.vertices = _sailVerts;
            _sailMesh.triangles = tris;
            var sail = new GameObject("Sail", typeof(MeshFilter), typeof(MeshRenderer));
            sail.transform.SetParent(_boom, false);
            sail.GetComponent<MeshFilter>().sharedMesh = _sailMesh;
            sail.GetComponent<MeshRenderer>().sharedMaterial = sailMat;

            _rudder = new GameObject("Rudder").transform;
            _rudder.SetParent(_heel, false);
            _rudder.localPosition = new Vector3(0, 0.1f, -2.12f);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(_rudder, false);
            blade.transform.localScale = new Vector3(0.03f, 1.0f, 0.3f);
            blade.transform.localPosition = new Vector3(0, -0.4f, -0.1f);
            blade.GetComponent<MeshRenderer>().sharedMaterial = deckMat;
            var tiller = Cylinder("Tiller", 0.02f, 1.1f, sparMat, _rudder);
            tiller.localRotation = Quaternion.Euler(90, 0, 0);
            tiller.localPosition = new Vector3(0, 0.25f, 0.55f);

            _crew = new GameObject("Sailor").transform;
            _crew.SetParent(_heel, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_crew, false);
            body.transform.localScale = new Vector3(0.36f, 0.45f, 0.3f);
            body.transform.localPosition = new Vector3(0, 0.45f, 0);
            body.GetComponent<MeshRenderer>().sharedMaterial = Look.Lit(new Color(0.12f, 0.3f, 0.5f));
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(_crew, false);
            head.transform.localScale = Vector3.one * 0.24f;
            head.transform.localPosition = new Vector3(0, 1.02f, 0);
            head.GetComponent<MeshRenderer>().sharedMaterial = Look.Lit(new Color(0.95f, 0.78f, 0.62f));

            _vane = new GameObject("Wind vane").transform;
            _vane.SetParent(_heel, false);
            _vane.localPosition = new Vector3(0, (float)(cfg.BoomHeight + cfg.LuffLength + 0.45), mastZ);
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(arrow.GetComponent<Collider>());
            arrow.transform.SetParent(_vane, false);
            arrow.transform.localScale = new Vector3(0.02f, 0.08f, 0.45f);
            arrow.transform.localPosition = new Vector3(0, 0, -0.18f);
            arrow.GetComponent<MeshRenderer>().sharedMaterial = Look.Lit(new Color(0.9f, 0.1f, 0.1f));

            var wakeGo = new GameObject("Wake");
            wakeGo.transform.SetParent(transform, false);
            wakeGo.transform.localPosition = new Vector3(0, 0.02f, -2.0f);
            var trail = wakeGo.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Look.Foam();
            trail.time = 7f;
            trail.minVertexDistance = 0.6f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1.1f), new Keyframe(1, 4.5f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.55f, 0), new GradientAlphaKey(0, 1) },
            };
            trail.alignment = LineAlignment.TransformZ;
            wakeGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Pose the boat from the simulation and the waves under it.</summary>
        public void Apply(BoatSimulator sim, WaveField waves, float t, float dt)
        {
            var s = sim.State;
            var tel = sim.Telemetry;
            var pos = Look.ToUnity(s.Position);
            float headingDeg = (float)(s.Heading * Mathf.Rad2Deg);

            // Heave, pitch and roll from the water surface along and across the hull, smoothed by the hull's inertia.
            var fwd = Quaternion.Euler(0, headingDeg, 0) * Vector3.forward;
            var stb = Quaternion.Euler(0, headingDeg, 0) * Vector3.right;
            float hC = waves.HeightAt(pos.x, pos.z, t);
            float hBow = waves.HeightAt(pos.x + fwd.x * 1.8f, pos.z + fwd.z * 1.8f, t);
            float hStern = waves.HeightAt(pos.x - fwd.x * 1.8f, pos.z - fwd.z * 1.8f, t);
            float hS = waves.HeightAt(pos.x + stb.x * 0.7f, pos.z + stb.z * 0.7f, t);
            float hP = waves.HeightAt(pos.x - stb.x * 0.7f, pos.z - stb.z * 0.7f, t);
            float k = 1 - Mathf.Exp(-dt * 6f);
            _heave = Mathf.Lerp(_heave, (hC + hBow + hStern) / 3f, k);
            _pitch = Mathf.Lerp(_pitch, Mathf.Atan2(hStern - hBow, 3.6f) * Mathf.Rad2Deg, k);
            _roll = Mathf.Lerp(_roll, Mathf.Atan2(hP - hS, 1.4f) * Mathf.Rad2Deg * 0.5f, k);
            // Planing lifts the bow a little.
            float planing = Mathf.Clamp01(((float)s.Surge - 3.2f) / 2f) * -2.5f;

            transform.SetPositionAndRotation(new Vector3(pos.x, _heave - 0.05f, pos.z), Quaternion.Euler(_pitch + planing, headingDeg, 0));
            _heel.localRotation = Quaternion.Euler(0, 0, -(float)(s.Heel * Mathf.Rad2Deg) + _roll);

            float boomDeg = (float)(s.Boom * Mathf.Rad2Deg);
            _boom.localRotation = Quaternion.Euler(0, 180f - boomDeg, 0); // local +Z of the boom points aft
            ShapeSail(tel, s, t);

            _rudder.localRotation = Quaternion.Euler(0, -(float)(tel.RudderAngle * Mathf.Rad2Deg), 0);
            float crewX = (float)(s.CrewOffset * _cfg.CrewMaxArm);
            _crew.localPosition = new Vector3(crewX * 0.85f, 0.32f, -0.9f);
            _crew.localRotation = Quaternion.Euler(0, 0, -Mathf.Clamp(crewX, -1, 1) * 55f);
            // Masthead vane points where the apparent wind blows.
            float awaDeg = (float)(tel.ApparentWindAngle * Mathf.Rad2Deg);
            _vane.localRotation = Quaternion.Euler(0, awaDeg + 180f, 0);
        }

        private void ShapeSail(BoatTelemetry tel, BoatState s, float t)
        {
            // Sail lives in boom space: +Z along the boom (aft), +Y up the mast. It bellies to leeward,
            // which is the side the boom is on.
            float side = s.Boom >= 0 ? 1f : -1f;
            float luff = (float)tel.LuffingFraction;
            float sheet = Mathf.Clamp01((float)tel.BoomAngle / (85f * Mathf.Deg2Rad));
            float twist = Mathf.Lerp(4f, 18f, sheet) * Mathf.Deg2Rad;
            float depth = 0.22f;
            float foot = (float)_cfg.FootLength, height = (float)_cfg.LuffLength;
            for (int r = 0; r <= SailRows; r++)
            {
                float v = r / (float)SailRows;
                float chord = foot * (1 - v);
                float ang = twist * v;
                for (int c = 0; c <= SailCols; c++)
                {
                    float u = c / (float)SailCols;
                    float along = u * chord;
                    float camber = depth * chord / foot * Mathf.Sin(Mathf.PI * u) * (1 - luff);
                    float flap = luff * 0.25f * Mathf.Sin(t * 22f + u * 8f + v * 5f) * Mathf.Sin(Mathf.PI * u) * (1 - v);
                    float x = side * (camber + flap);
                    // Twist rotates the upper sail further out around the mast.
                    float xr = x * Mathf.Cos(ang) + side * along * Mathf.Sin(ang);
                    float zr = along * Mathf.Cos(ang) - x * side * Mathf.Sin(ang);
                    _sailVerts[r * (SailCols + 1) + c] = new Vector3(xr * -1f, v * height + 0.05f, zr);
                }
            }
            _sailMesh.vertices = _sailVerts;
            _sailMesh.RecalculateNormals();
            _sailMesh.RecalculateBounds();
        }

        private static Transform Part(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Transform Cylinder(string name, float radius, float length, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var holder = new GameObject(name + " pivot").transform;
            holder.SetParent(parent, false);
            go.transform.SetParent(holder, false);
            go.transform.localPosition = new Vector3(0, length / 2, 0);
            return holder;
        }

        /// <summary>Lofted dinghy hull: a fine bow, a broad flat stern and a shallow rounded bottom.</summary>
        private static Mesh HullMesh(float length, float beam, float depth, bool deck)
        {
            const int stations = 18, around = 10;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            float HalfBeam(float s) // s = 0 at the stern, 1 at the bow
            {
                float b = s < 0.6f ? Mathf.Lerp(0.78f, 1f, Mathf.Sin(s / 0.6f * Mathf.PI / 2)) : Mathf.Cos((s - 0.6f) / 0.4f * Mathf.PI / 2);
                return Mathf.Max(0.02f, b) * beam / 2;
            }
            float Depth(float s) => depth * (s < 0.7f ? 0.85f + 0.15f * s / 0.7f : 1f - 0.6f * (s - 0.7f) / 0.3f);
            float sheer = 0.32f;
            for (int i = 0; i <= stations; i++)
            {
                float s = i / (float)stations;
                float z = (s - 0.5f) * length;
                float hb = HalfBeam(s), d = Depth(s);
                if (deck)
                {
                    verts.Add(new Vector3(-hb * 0.97f, sheer + 0.01f + 0.04f * s, z));
                    verts.Add(new Vector3(hb * 0.97f, sheer + 0.01f + 0.04f * s, z));
                }
                else
                {
                    for (int j = 0; j <= around; j++)
                    {
                        float a = Mathf.PI * j / around; // 0 = port sheer, π = starboard sheer
                        float x = -Mathf.Cos(a) * hb;
                        float y = sheer + 0.04f * s - Mathf.Pow(Mathf.Sin(a), 0.6f) * d;
                        verts.Add(new Vector3(x, y, z));
                    }
                }
            }
            int ring = deck ? 2 : around + 1;
            for (int i = 0; i < stations; i++)
                for (int j = 0; j < ring - 1; j++)
                {
                    int a = i * ring + j, b = a + 1, c = a + ring, e = c + 1;
                    if (deck) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(e); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(e); tris.Add(c); }
                }
            if (!deck)
            {
                // Transom.
                int centre = verts.Count;
                verts.Add(new Vector3(0, sheer - Depth(0) * 0.5f, -length / 2));
                for (int j = 0; j < around; j++) { tris.Add(centre); tris.Add(j + 1); tris.Add(j); }
            }
            var mesh = new Mesh { name = deck ? "Deck" : "Hull" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
