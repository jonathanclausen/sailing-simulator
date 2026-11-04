using System.Collections.Generic;
using TillerTide.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>
    /// A modern 40 ft cruising sloop, built from code: white hull with a wide transom and a dark stripe, antifouled
    /// bottom, coachroof with windows, cockpit with a wheel, stanchions and guardrails, a fractional rig with
    /// spreaders and stays, the mainsail on the boom and a genoa on the forestay (both shaped live from the
    /// physics), fin keel with a bulb, spade rudder, and two crew.
    /// Boat-local axes: +Z forward, +X starboard, +Y up, origin at the centre of gravity on the waterline.
    /// </summary>
    public sealed class CruiserVisual : MonoBehaviour
    {
        private const float Loa = 12.2f, Beam = 3.95f;
        private const int SailCols = 10, SailRows = 14;

        private BoatConfig _cfg;
        private Transform _heel, _boom, _rudder, _wheel, _trimmer;
        private Mesh _mainMesh, _jibMesh;
        private Vector3[] _mainVerts, _jibVerts;
        private float _heave, _pitch, _roll;
        private float _mastTop;
        private Vector3 _jibTack, _jibHead;

        private static readonly Color White = new Color(0.96f, 0.96f, 0.95f);
        private static readonly Color Navy = new Color(0.08f, 0.12f, 0.22f);
        private static readonly Color Antifoul = new Color(0.12f, 0.14f, 0.2f);
        private static readonly Color DeckGrey = new Color(0.82f, 0.82f, 0.8f);
        private static readonly Color Teak = new Color(0.62f, 0.47f, 0.3f);
        private static readonly Color Metal = new Color(0.72f, 0.74f, 0.76f);
        private static readonly Color Spar = new Color(0.78f, 0.79f, 0.8f);
        private static readonly Color Glass = new Color(0.1f, 0.13f, 0.17f);
        private static readonly Color Cloth = new Color(0.97f, 0.96f, 0.93f);

        public void Build(BoatConfig cfg)
        {
            _cfg = cfg;
            _heel = new GameObject("Heel").transform;
            _heel.SetParent(transform, false);

            var vertexLit = Look.Lit(Color.white);
            var white = Look.Lit(White);
            var deck = Look.Lit(DeckGrey);
            var teak = Look.Lit(Teak);
            var metal = Look.Lit(Metal);
            var spar = Look.Lit(Spar);
            var glass = Look.Lit(Glass);
            var navy = Look.Lit(Navy);
            var dark = Look.Lit(new Color(0.2f, 0.2f, 0.22f));

            // Hull and deck.
            Part("Hull", HullMesh(false), vertexLit, _heel);
            Part("Deck", HullMesh(true), deck, _heel);

            // Coachroof with a band of dark windows, sloping a little at the front.
            Box("Coachroof", new Vector3(0, 1.62f, 0.2f), new Vector3(2.5f, 0.5f, 4.4f), white);
            Box("Coachroof front", new Vector3(0, 1.55f, 2.55f), new Vector3(2.2f, 0.36f, 0.5f), white).localRotation = Quaternion.Euler(-25, 0, 0);
            Box("Windows P", new Vector3(-1.255f, 1.66f, 0.4f), new Vector3(0.02f, 0.18f, 2.8f), glass);
            Box("Windows S", new Vector3(1.255f, 1.66f, 0.4f), new Vector3(0.02f, 0.18f, 2.8f), glass);
            Box("Hatch", new Vector3(0, 1.88f, 1.4f), new Vector3(0.7f, 0.04f, 0.7f), glass);
            Box("Sprayhood", new Vector3(0, 1.8f, -2.2f), new Vector3(2.3f, 0.55f, 0.8f), navy);

            // Cockpit: teak seats and sole, coamings, and the wheel on its pedestal.
            Box("Cockpit sole", new Vector3(0, 1.05f, -3.6f), new Vector3(2.6f, 0.06f, 2.6f), teak);
            Box("Seat P", new Vector3(-1.05f, 1.33f, -3.3f), new Vector3(0.55f, 0.06f, 2.4f), teak);
            Box("Seat S", new Vector3(1.05f, 1.33f, -3.3f), new Vector3(0.55f, 0.06f, 2.4f), teak);
            Box("Coaming P", new Vector3(-1.4f, 1.48f, -3.1f), new Vector3(0.08f, 0.3f, 2.8f), white);
            Box("Coaming S", new Vector3(1.4f, 1.48f, -3.1f), new Vector3(0.08f, 0.3f, 2.8f), white);
            Cylinder("Pedestal", new Vector3(0, 1.08f, -4.6f), 0.07f, 0.75f, metal, _heel);
            _wheel = new GameObject("Wheel").transform;
            _wheel.SetParent(_heel, false);
            _wheel.localPosition = new Vector3(0, 2.05f, -4.6f);
            var rim = new GameObject("Rim", typeof(MeshFilter), typeof(MeshRenderer));
            rim.transform.SetParent(_wheel, false);
            rim.GetComponent<MeshFilter>().sharedMesh = Torus(0.55f, 0.025f, 32, 8);
            rim.GetComponent<MeshRenderer>().sharedMaterial = metal;
            for (int i = 0; i < 6; i++)
            {
                var spoke = Box("Spoke", Vector3.zero, new Vector3(0.02f, 1.08f, 0.02f), metal, _wheel);
                spoke.localRotation = Quaternion.Euler(0, 0, i * 30);
            }

            // Stanchions and guardrails along the sheer, a pulpit and a pushpit.
            var posts = new List<Vector3>();
            for (int side = -1; side <= 1; side += 2)
            {
                posts.Clear();
                for (float s = 0.06f; s <= 0.9f; s += 0.12f)
                {
                    float z = (s - 0.5f) * Loa, hb = HalfBeam(s) * 0.93f;
                    posts.Add(new Vector3(side * hb, Sheer(s), z));
                }
                foreach (var p in posts) Cylinder("Stanchion", p, 0.015f, 0.62f, metal, _heel);
                for (int k = 0; k < 2; k++)
                    for (int i = 0; i + 1 < posts.Count; i++)
                        Rod(posts[i] + Vector3.up * (0.62f - k * 0.3f), posts[i + 1] + Vector3.up * (0.62f - k * 0.3f), 0.006f, metal, _heel);
            }

            // Rig: a fractional sloop with two sets of spreaders.
            float deckAtMast = Sheer(0.5f + (float)cfg.MastX / Loa) + 0.35f;
            _mastTop = (float)(cfg.BoomHeight + cfg.LuffLength + 1.0);
            var mastBase = new Vector3(0, deckAtMast, (float)cfg.MastX);
            var mastTop = new Vector3(0, _mastTop, (float)cfg.MastX);
            Rod(mastBase, mastTop, 0.08f, spar, _heel, square: true);
            foreach (float h in new[] { 0.4f, 0.7f })
            {
                float y = Mathf.Lerp(deckAtMast, _mastTop, h), half = h < 0.5f ? 1.25f : 0.8f;
                Rod(new Vector3(-half, y, (float)cfg.MastX - 0.15f), new Vector3(half, y, (float)cfg.MastX - 0.15f), 0.02f, spar, _heel);
            }
            float chainZ = (float)cfg.MastX - 0.4f, chainY = Sheer(0.5f + chainZ / Loa);
            for (int side = -1; side <= 1; side += 2)
            {
                var chain = new Vector3(side * (Beam / 2 - 0.15f), chainY, chainZ);
                var lower = new Vector3(side * 1.25f, Mathf.Lerp(deckAtMast, _mastTop, 0.4f), chainZ + 0.25f);
                var upper = new Vector3(side * 0.8f, Mathf.Lerp(deckAtMast, _mastTop, 0.7f), chainZ + 0.25f);
                Rod(chain, lower, 0.006f, dark, _heel);
                Rod(lower, upper, 0.006f, dark, _heel);
                Rod(upper, mastTop - Vector3.up * 0.4f, 0.006f, dark, _heel);
            }
            _jibTack = new Vector3(0, (float)cfg.JibTackHeight, (float)cfg.JibTackX);
            _jibHead = new Vector3(0, (float)(cfg.JibTackHeight + cfg.JibLuffLength * 1.02), (float)cfg.MastX + 0.1f);
            Rod(_jibTack, _jibHead, 0.008f, dark, _heel); // forestay
            Rod(new Vector3(0, Sheer(0) + 0.1f, -Loa / 2 + 0.2f), mastTop, 0.006f, dark, _heel); // backstay
            Box("Pulpit", new Vector3(0, Sheer(1) + 0.4f, Loa / 2 - 0.5f), new Vector3(1.0f, 0.03f, 0.03f), metal);
            Box("Bow roller", new Vector3(0, Sheer(1) + 0.05f, Loa / 2 + 0.1f), new Vector3(0.25f, 0.15f, 0.6f), metal);
            Box("Pushpit", new Vector3(0, Sheer(0) + 0.65f, -Loa / 2 + 0.45f), new Vector3(Beam - 0.8f, 0.03f, 0.03f), metal);

            // Boom and mainsail.
            _boom = new GameObject("Boom").transform;
            _boom.SetParent(_heel, false);
            _boom.localPosition = new Vector3(0, (float)cfg.BoomHeight, (float)cfg.MastX);
            var boomSpar = Box("Boom spar", new Vector3(0, -0.08f, -(float)cfg.FootLength / 2 - 0.1f), new Vector3(0.12f, 0.18f, (float)cfg.FootLength + 0.3f), spar, _boom);
            Box("Stack pack", new Vector3(0, 0.08f, -(float)cfg.FootLength / 2), new Vector3(0.3f, 0.18f, (float)cfg.FootLength * 0.95f), navy, _boom);
            _mainMesh = SailMesh("Mainsail", out _mainVerts, _boom, Look.Lit(Cloth, doubleSided: true));
            _jibMesh = SailMesh("Genoa", out _jibVerts, _heel, Look.Lit(Cloth, doubleSided: true));

            // Fin keel with a bulb and the spade rudder (visible when she heels, and from underwater).
            Box("Keel fin", new Vector3(0, -1.2f, (float)cfg.BoardX - 2.0f), new Vector3(0.16f, 1.4f, 1.5f), navy).localRotation = Quaternion.Euler(-8, 0, 0);
            var bulb = Box("Keel bulb", new Vector3(0, -1.95f, (float)cfg.BoardX - 2.15f), new Vector3(0.42f, 0.32f, 2.2f), navy);
            _rudder = new GameObject("Rudder").transform;
            _rudder.SetParent(_heel, false);
            _rudder.localPosition = new Vector3(0, -0.2f, (float)cfg.RudderX);
            Box("Rudder blade", new Vector3(0, -0.75f, -0.15f), new Vector3(0.08f, 1.3f, 0.55f), navy, _rudder);

            // Crew: the helmsman behind the wheel and a trimmer on the windward side.
            Person("Helmsman", new Vector3(0, 1.1f, -5.15f), new Color(0.75f, 0.15f, 0.12f), standing: true);
            _trimmer = Person("Trimmer", new Vector3(1.0f, 1.36f, -2.9f), new Color(0.95f, 0.75f, 0.1f), standing: false);

            // Wake.
            var wakeGo = new GameObject("Wake");
            wakeGo.transform.SetParent(transform, false);
            wakeGo.transform.localPosition = new Vector3(0, 0.03f, -Loa / 2 + 0.3f);
            wakeGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var trail = wakeGo.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Look.Foam();
            trail.time = 12f;
            trail.minVertexDistance = 1.2f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 3.2f), new Keyframe(1, 12f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.5f, 0), new GradientAlphaKey(0, 1) },
            };
            trail.alignment = LineAlignment.TransformZ;
            trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Pose the boat from the simulation and the waves under it.</summary>
        public void Apply(BoatSimulator sim, WaveField waves, float t, float dt)
        {
            var s = sim.State;
            var tel = sim.Telemetry;
            var pos = Look.ToUnity(s.Position);
            float headingDeg = (float)(s.Heading * Mathf.Rad2Deg);

            // A 12 m hull spans several of the Sound's short waves, so sample along and across it and smooth hard.
            var fwd = Quaternion.Euler(0, headingDeg, 0) * Vector3.forward;
            var stb = Quaternion.Euler(0, headingDeg, 0) * Vector3.right;
            float hC = waves.HeightAt(pos.x, pos.z, t);
            float hBow = waves.HeightAt(pos.x + fwd.x * 4.5f, pos.z + fwd.z * 4.5f, t);
            float hStern = waves.HeightAt(pos.x - fwd.x * 4.5f, pos.z - fwd.z * 4.5f, t);
            float hS = waves.HeightAt(pos.x + stb.x * 1.7f, pos.z + stb.z * 1.7f, t);
            float hP = waves.HeightAt(pos.x - stb.x * 1.7f, pos.z - stb.z * 1.7f, t);
            float k = 1 - Mathf.Exp(-dt * 2.5f);
            _heave = Mathf.Lerp(_heave, (hC * 2 + hBow + hStern) / 4f, k);
            _pitch = Mathf.Lerp(_pitch, Mathf.Atan2(hStern - hBow, 9f) * Mathf.Rad2Deg, k);
            _roll = Mathf.Lerp(_roll, Mathf.Atan2(hP - hS, 3.4f) * Mathf.Rad2Deg * 0.4f, k);
            // Squat: the stern settles a little at speed.
            float squat = Mathf.Clamp01((float)s.Surge / 4.2f) * -0.8f;

            transform.SetPositionAndRotation(new Vector3(pos.x, _heave, pos.z), Quaternion.Euler(_pitch + squat, headingDeg, 0));
            _heel.localRotation = Quaternion.Euler(0, 0, -(float)(s.Heel * Mathf.Rad2Deg) + _roll);

            float boomDeg = (float)(s.Boom * Mathf.Rad2Deg);
            _boom.localRotation = Quaternion.Euler(0, 180f - boomDeg, 0); // local +Z of the boom points aft
            ShapeMain(tel, s, t);
            ShapeJib(tel, s, t);

            float rudderDeg = (float)(tel.RudderAngle * Mathf.Rad2Deg);
            _rudder.localRotation = Quaternion.Euler(0, -rudderDeg, 0);
            _wheel.localRotation = Quaternion.Euler(0, 0, rudderDeg * 6f); // about one and a half turns lock to lock
            // The trimmer sits on the windward side (or the leeward side to trim in light air).
            float side = s.CrewOffset >= 0 ? 1f : -1f;
            _trimmer.localPosition = new Vector3(side * Mathf.Lerp(0.6f, 1.5f, Mathf.Abs((float)s.CrewOffset)), 1.36f, -2.9f);
        }

        // ---------- Sails ----------

        private void ShapeMain(BoatTelemetry tel, BoatState s, float t)
        {
            // Boom space: +Z along the boom (aft), +Y up the mast; it bellies to leeward, the side the boom is on.
            float side = s.Boom >= 0 ? 1f : -1f;
            float luff = (float)tel.LuffingFraction;
            float sheet = Mathf.Clamp01((float)tel.BoomAngle / (80f * Mathf.Deg2Rad));
            float twist = Mathf.Lerp(3f, 14f, sheet) * Mathf.Deg2Rad;
            float foot = (float)_cfg.FootLength, height = (float)_cfg.LuffLength;
            for (int r = 0; r <= SailRows; r++)
            {
                float v = r / (float)SailRows;
                // A modern square-top main: the chord stays wide high up.
                float chord = foot * (1 - Mathf.Pow(v, 1.6f)) + 0.25f * v;
                float ang = twist * v;
                for (int c = 0; c <= SailCols; c++)
                {
                    float u = c / (float)SailCols;
                    float along = u * chord;
                    float camber = 0.11f * chord * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.8f)) * (1 - luff);
                    float flap = luff * 0.35f * Mathf.Sin(t * 14f + u * 7f + v * 4f) * Mathf.Sin(Mathf.PI * u) * (1 - 0.6f * v);
                    float x = side * (camber + flap);
                    float xr = x * Mathf.Cos(ang) + side * along * Mathf.Sin(ang);
                    float zr = along * Mathf.Cos(ang) - x * side * Mathf.Sin(ang);
                    _mainVerts[r * (SailCols + 1) + c] = new Vector3(-xr, 0.12f + v * height, zr + 0.1f);
                }
            }
            _mainMesh.vertices = _mainVerts;
            _mainMesh.RecalculateNormals();
            _mainMesh.RecalculateBounds();
        }

        private void ShapeJib(BoatTelemetry tel, BoatState s, float t)
        {
            // Heel space: the luff runs up the forestay, the clew is pulled aft and out to leeward by the sheet.
            float side = s.Jib >= 0 ? 1f : -1f;
            float angle = Mathf.Abs((float)s.Jib);
            float luff = (float)tel.JibLuffingFraction;
            float twist = Mathf.Lerp(5f, 20f, Mathf.Clamp01(angle / (70f * Mathf.Deg2Rad))) * Mathf.Deg2Rad;
            float foot = (float)_cfg.JibFootLength;
            for (int r = 0; r <= SailRows; r++)
            {
                float v = r / (float)SailRows;
                var luffPoint = Vector3.Lerp(_jibTack, _jibHead, v);
                float chord = foot * (1 - v) + 0.1f;
                float a = angle + twist * v;
                // Chord direction (aft and out to leeward) and the belly direction (to leeward, slightly forward).
                var dir = new Vector3(side * Mathf.Sin(a), -0.08f * (1 - v), -Mathf.Cos(a));
                var belly = new Vector3(side * Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int c = 0; c <= SailCols; c++)
                {
                    float u = c / (float)SailCols;
                    float camber = 0.13f * chord * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)) * (1 - luff);
                    float flap = luff * 0.4f * Mathf.Sin(t * 16f + u * 8f + v * 5f) * Mathf.Sin(Mathf.PI * u);
                    _jibVerts[r * (SailCols + 1) + c] = luffPoint + dir * (u * chord) + belly * (camber + flap);
                }
            }
            _jibMesh.vertices = _jibVerts;
            _jibMesh.RecalculateNormals();
            _jibMesh.RecalculateBounds();
        }

        private static Mesh SailMesh(string name, out Vector3[] verts, Transform parent, Material mat)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            verts = new Vector3[(SailCols + 1) * (SailRows + 1)];
            var tris = new int[SailCols * SailRows * 6];
            int k = 0;
            for (int r = 0; r < SailRows; r++)
                for (int c = 0; c < SailCols; c++)
                {
                    int a = r * (SailCols + 1) + c, b = a + 1, d = a + SailCols + 1, e = d + 1;
                    tris[k++] = a; tris[k++] = d; tris[k++] = b; tris[k++] = b; tris[k++] = d; tris[k++] = e;
                }
            mesh.vertices = verts;
            mesh.triangles = tris;
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return mesh;
        }

        // ---------- Hull ----------

        /// <summary>Half beam at the sheer, s = 0 at the transom, 1 at the bow: beam carried well aft, plumb bow.</summary>
        private static float HalfBeam(float s)
        {
            float b = s < 0.45f ? Mathf.Lerp(0.86f, 1f, Mathf.Sin(s / 0.45f * Mathf.PI / 2))
                                : Mathf.Pow(Mathf.Max(0f, Mathf.Cos((s - 0.45f) / 0.55f * Mathf.PI / 2)), 0.75f);
            return Mathf.Max(0.03f, b) * Beam / 2;
        }

        /// <summary>Sheer height above the waterline: higher at the bow.</summary>
        private static float Sheer(float s) => 1.15f + 0.35f * s * s;

        /// <summary>Depth of the canoe body below the waterline.</summary>
        private static float BodyDepth(float s) => s < 0.85f ? 0.6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(0.15f + s * 0.95f)) + 0.12f : Mathf.Lerp(0.5f, 0.05f, (s - 0.85f) / 0.15f);

        private static Mesh HullMesh(bool deck)
        {
            const int stations = 30, around = 16;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= stations; i++)
            {
                float s = i / (float)stations;
                float z = (s - 0.5f) * Loa;
                float hb = HalfBeam(s), top = Sheer(s), bottom = -BodyDepth(s);
                if (deck)
                {
                    verts.Add(new Vector3(-hb * 0.98f, top + 0.02f, z));
                    verts.Add(new Vector3(0, top + 0.08f, z));
                    verts.Add(new Vector3(hb * 0.98f, top + 0.02f, z));
                    continue;
                }
                for (int j = 0; j <= around; j++)
                {
                    // Round from port sheer, down through a firm bilge and flat-ish bottom, up to starboard sheer.
                    float a = Mathf.PI * j / around;
                    float sinA = Mathf.Max(0f, Mathf.Sin(a)); // sin(PI) is slightly negative in float, and Pow of that is NaN
                    float x = -Mathf.Cos(a) * hb * Mathf.Lerp(1f, 0.92f, sinA);
                    float y = Mathf.Lerp(top, bottom, Mathf.Pow(sinA, 0.45f));
                    verts.Add(new Vector3(x, y, z));
                    cols.Add(y < 0.08f ? Antifoul : (y > 0.72f && y < 0.84f ? Navy : White));
                }
            }
            int ring = deck ? 3 : around + 1;
            for (int i = 0; i < stations; i++)
                for (int j = 0; j < ring - 1; j++)
                {
                    int a = i * ring + j, b = a + 1, c = a + ring, e = c + 1;
                    if (deck) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(e); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(e); tris.Add(c); }
                }
            if (!deck)
            {
                // Wide, flat transom.
                int centre = verts.Count;
                verts.Add(new Vector3(0, (Sheer(0) - BodyDepth(0)) * 0.5f, -Loa / 2));
                cols.Add(White);
                for (int j = 0; j < around; j++) { tris.Add(centre); tris.Add(j + 1); tris.Add(j); }
            }
            var mesh = new Mesh { name = deck ? "Cruiser deck" : "Cruiser hull" };
            mesh.SetVertices(verts);
            if (!deck) mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh Torus(float radius, float tube, int seg, int sides)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var centre = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides;
                    v.Add(centre + (centre.normalized * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * tube);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1); tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = new Mesh { name = "Torus" };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            return m;
        }

        // ---------- Small helpers ----------

        private Transform Person(string name, Vector3 at, Color jacket, bool standing)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_heel, false);
            root.localPosition = at;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localScale = new Vector3(0.42f, standing ? 0.55f : 0.4f, 0.32f);
            body.transform.localPosition = new Vector3(0, standing ? 0.6f : 0.45f, 0);
            body.GetComponent<MeshRenderer>().sharedMaterial = Look.Lit(jacket);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root, false);
            head.transform.localScale = Vector3.one * 0.24f;
            head.transform.localPosition = new Vector3(0, standing ? 1.32f : 1.0f, 0);
            head.GetComponent<MeshRenderer>().sharedMaterial = Look.Lit(new Color(0.93f, 0.76f, 0.6f));
            return root;
        }

        private Transform Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent != null ? parent : _heel, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return b.transform;
        }

        private void Cylinder(string name, Vector3 bottom, float radius, float length, Material mat, Transform parent) =>
            Rod(bottom, bottom + Vector3.up * length, radius, mat, parent);

        /// <summary>A thin cylinder (or square spar) between two points: stays, guardrails, spreaders.</summary>
        private static void Rod(Vector3 a, Vector3 b, float radius, Material mat, Transform parent, bool square = false)
        {
            var go = GameObject.CreatePrimitive(square ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var d = b - a;
            go.transform.localPosition = (a + b) / 2;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.transform.localScale = square ? new Vector3(radius * 2, d.magnitude, radius * 3) : new Vector3(radius * 2, d.magnitude / 2, radius * 2);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = radius < 0.03f ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        private static void Part(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
