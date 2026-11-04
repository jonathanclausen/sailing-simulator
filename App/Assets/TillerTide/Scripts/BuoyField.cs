using System.Collections.Generic;
using TillerTide.Physics.Racing;
using TillerTide.Physics.Region;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>
    /// Draws the real buoys and beacons (IALA A colours and shapes, as you'd see them in Øresund) and the
    /// practice course marks. Floating marks bob on the waves; the next course mark gets a tall orange pole
    /// so you can find it from far away.
    /// </summary>
    public sealed class BuoyField : MonoBehaviour
    {
        private readonly List<Transform> _floating = new List<Transform>();
        private readonly List<Transform> _courseMarks = new List<Transform>();
        private readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();
        private Transform _beacon;
        private static Mesh _cone;

        private static readonly Color Red = new Color(0.85f, 0.12f, 0.1f);
        private static readonly Color Green = new Color(0.1f, 0.6f, 0.25f);
        private static readonly Color Yellow = new Color(0.98f, 0.82f, 0.1f);
        private static readonly Color Black = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.1f);

        private Material Mat(Color c)
        {
            if (!_mats.TryGetValue(c, out var m)) _mats[c] = m = Look.Lit(c);
            return m;
        }

        private readonly Dictionary<Color, Material> _doubleMats = new Dictionary<Color, Material>();
        private Material MatDouble(Color c)
        {
            if (!_doubleMats.TryGetValue(c, out var m)) _doubleMats[c] = m = Look.Lit(c, doubleSided: true);
            return m;
        }

        public void Build(IReadOnlyList<Seamark> marks)
        {
            if (marks == null) return;
            foreach (var s in marks)
            {
                var root = new GameObject(s.ToString()).transform;
                root.SetParent(transform, false);
                root.position = Look.ToUnity(s.Position);
                if (s.IsBeacon) BuildBeacon(s, root);
                else { BuildBuoy(s, root); _floating.Add(root); }
            }
        }

        private void BuildBuoy(Seamark s, Transform root)
        {
            switch (s.Kind)
            {
                case SeamarkKind.Port:
                    Cyl(root, 0, 1.6f, 0.55f, Red);
                    Cyl(root, 1.6f, 1.75f, 0.55f, Red); // flat top: a can
                    break;
                case SeamarkKind.Starboard:
                    Cyl(root, 0, 0.6f, 0.6f, Green);
                    Cone(root, 0.6f, 1.9f, 0.6f, Green, up: true);
                    break;
                case SeamarkKind.CardinalNorth: Cardinal(root, Black, Yellow, true, true); break;
                case SeamarkKind.CardinalSouth: Cardinal(root, Yellow, Black, false, false); break;
                case SeamarkKind.CardinalEast: Cardinal(root, Black, Yellow, true, false, eastBand: true); break;
                case SeamarkKind.CardinalWest: Cardinal(root, Yellow, Black, false, true, westBand: true); break;
                case SeamarkKind.IsolatedDanger:
                    Cyl(root, 0, 0.8f, 0.5f, Black); Cyl(root, 0.8f, 1.4f, 0.5f, Red); Cyl(root, 1.4f, 2.2f, 0.5f, Black);
                    Ball(root, 2.7f, 0.35f, Black); Ball(root, 3.2f, 0.35f, Black);
                    break;
                case SeamarkKind.SafeWater:
                    Ball(root, 0.8f, 1.6f, Red); Cyl(root, 1.5f, 2.6f, 0.08f, White); Ball(root, 2.8f, 0.4f, Red);
                    break;
                default:
                    Cyl(root, 0, 1.4f, 0.5f, Yellow);
                    break;
            }
        }

        private void Cardinal(Transform root, Color top, Color bottom, bool topUp, bool bottomUp, bool eastBand = false, bool westBand = false)
        {
            // Pillar in two halves (with a band for east/west) and two cone topmarks pointing to the safe side.
            Cyl(root, 0, 1.2f, 0.5f, bottom);
            Cyl(root, 1.2f, 2.4f, 0.4f, top);
            if (eastBand) Cyl(root, 0.8f, 1.6f, 0.52f, Yellow);
            if (westBand) Cyl(root, 0.8f, 1.6f, 0.52f, Black);
            Cyl(root, 2.4f, 3.2f, 0.04f, Black);
            Cone(root, 3.2f, 3.7f, 0.25f, Black, topUp);
            Cone(root, 3.75f, 4.25f, 0.25f, Black, bottomUp);
        }

        private void BuildBeacon(Seamark s, Transform root)
        {
            Color c = s.Kind == SeamarkKind.Port ? Red : s.Kind == SeamarkKind.Starboard ? Green
                    : s.Kind == SeamarkKind.Special ? Yellow : s.Kind >= SeamarkKind.CardinalNorth && s.Kind <= SeamarkKind.CardinalWest ? Yellow
                    : Black;
            Cyl(root, -2f, 5f, 0.12f, c);
            if (s.Kind == SeamarkKind.Starboard) Cone(root, 5f, 5.6f, 0.35f, Green, true);
            else if (s.Kind == SeamarkKind.Port) Cyl(root, 5f, 5.6f, 0.3f, Red);
        }

        public void ShowCourse(PracticeCourse course)
        {
            foreach (var t in _courseMarks) Destroy(t.gameObject);
            _courseMarks.Clear();
            if (_beacon != null) Destroy(_beacon.gameObject);
            if (course == null) return;
            var seen = new HashSet<CourseMark>();
            foreach (var m in course.Marks)
            {
                if (!seen.Add(m) || m.IsReal) continue; // real buoys are drawn already
                var root = new GameObject("Course: " + m.Name).transform;
                root.SetParent(transform, false);
                root.position = Look.ToUnity(m.Position);
                Cyl(root, -0.3f, 1.8f, 0.75f, Orange); // inflatable race mark
                _courseMarks.Add(root);
                _floating.Add(root);
            }
            _beacon = new GameObject("Next mark").transform;
            _beacon.SetParent(transform, false);
            Cyl(_beacon, 0, 14f, 0.06f, Orange);
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(flag.GetComponent<Collider>());
            flag.transform.SetParent(_beacon, false);
            flag.transform.localPosition = new Vector3(0.6f, 13.3f, 0);
            flag.transform.localScale = new Vector3(1.2f, 0.8f, 0.03f);
            flag.GetComponent<MeshRenderer>().sharedMaterial = Mat(Orange);
            _beacon.gameObject.SetActive(false);
        }

        public void SetNext(CourseMark next)
        {
            if (_beacon == null) return;
            _beacon.gameObject.SetActive(next != null);
            if (next != null) _beacon.position = Look.ToUnity(next.Position);
        }

        public void Animate(WaveField waves, float t, Vector3 camera)
        {
            _floating.RemoveAll(f => f == null);
            foreach (var f in _floating)
            {
                var p = f.position;
                if ((p - camera).sqrMagnitude > 1500f * 1500f) continue;
                float h = waves.HeightAt(p.x, p.z, t);
                var n = waves.NormalAt(p.x, p.z, t);
                f.SetPositionAndRotation(new Vector3(p.x, h - 0.3f, p.z), Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, n, 0.6f)));
            }
            if (_beacon != null && _beacon.gameObject.activeSelf)
            {
                var b = _beacon.position;
                _beacon.position = new Vector3(b.x, waves.HeightAt(b.x, b.z, t) - 0.3f, b.z);
                _beacon.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(camera, b) / 60f, 1f, 8f);
            }
        }

        // y0..y1 in metres above the waterline.
        private void Cyl(Transform root, float y0, float y1, float radius, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, (y0 + y1) / 2, 0);
            go.transform.localScale = new Vector3(radius * 2, (y1 - y0) / 2, radius * 2);
            Finish(go, c);
        }

        private void Ball(Transform root, float y, float diameter, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.transform.localScale = Vector3.one * diameter;
            Finish(go, c);
        }

        private void Cone(Transform root, float y0, float y1, float radius, Color c, bool up)
        {
            var go = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = ConeMesh();
            go.transform.localPosition = new Vector3(0, up ? y0 : y1, 0);
            go.transform.localScale = new Vector3(radius, (y1 - y0) * (up ? 1 : -1), radius);
            Finish(go, c, doubleSided: true);
        }

        private void Finish(GameObject go, Color c, bool doubleSided = false)
        {
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = doubleSided ? MatDouble(c) : Mat(c);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Unit cone: base radius 1 at y = 0, tip at y = 1. Drawn with a double-sided material, so a flipped cone still shows.</summary>
        private static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            const int n = 16;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int b = v.Count;
                v.Add(p0); v.Add(p1); v.Add(Vector3.up);
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
                int c = v.Count;
                v.Add(p0); v.Add(p1); v.Add(Vector3.zero);
                tri.Add(c); tri.Add(c + 1); tri.Add(c + 2);
            }
            _cone = new Mesh { name = "Cone" };
            _cone.SetVertices(v);
            _cone.SetTriangles(tri, 0);
            _cone.RecalculateNormals();
            return _cone;
        }
    }
}
