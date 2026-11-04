using System.Collections;
using TillerTide.Physics;
using TillerTide.Physics.Racing;
using TillerTide.Physics.Region;
using TillerTide.Physics.Traffic;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TillerTide.App
{
    /// <summary>
    /// Builds the whole scene in code when the app starts: sky, sun, ocean, the real coastline around Helsingør,
    /// Kronborg, the boat, the camera and the HUD. Then runs the physics at a fixed 120 Hz.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private static readonly RegionInfo Region = Regions.Helsingor;
        private const double KronborgLat = 56.0386, KronborgLon = 12.6217;

        private readonly LiveWind _wind = new LiveWind();
        private readonly RegionLoader _region = new RegionLoader();
        private readonly WaveField _waves = new WaveField();
        private BoatSimulator _sim;
        private GustyWind _windField;
        private BoatVisual _boat;
        private ChaseCamera _cam;
        private Hud _hud;
        private Transform _sky;
        private BuoyField _buoys;
        private CourseRunner _runner;
        private int _courseMode; // 0 off, 1 windward-leeward, 2 buoy tour
        private readonly List<Ferry> _ferries = new List<Ferry>();
        private readonly List<FerryVisual> _ferryVisuals = new List<FerryVisual>();
        private double _ferryClock;
        private float _lastHorn = -100;
        private SoundScape _sound;
        private Vec2 _start;
        private int _currentMode; // 0 none, 1 north-going, 2 south-going
        private bool _ready;
        private GUIStyle _loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameController>() != null) return;
            new GameObject("Tiller & Tide").AddComponent<GameController>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;
        }

        private IEnumerator Start()
        {
            BuildEnvironment();
            bool windDone = false, mapDone = false;
            StartCoroutine(_wind.Fetch(Region.StartLat, Region.StartLon, _ => windDone = true));
            StartCoroutine(_region.Load(Region, _ => mapDone = true));
            while (!windDone || !mapDone) yield return null;
            BuildWorld();
            _ready = true;
        }

        private void BuildEnvironment()
        {
            Look.PushSkyColours();
            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Look.SkyHorizon;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 12000f;
            cam.fieldOfView = 55f;
            cam.transform.position = new Vector3(0, 4, -12);
            _cam = cam.gameObject.AddComponent<ChaseCamera>();
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<AudioSource>();
            _sound = cam.gameObject.AddComponent<SoundScape>();

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) Destroy(l.gameObject);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 215f, 0f);
            RenderSettings.sun = sun;

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.42f, 0.47f, 0.52f));
            sh.AddDirectionalLight(Vector3.up, Look.SkyZenith * 0.35f, 1f);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Look.SkyHorizon;
            RenderSettings.fogStartDistance = 400f;
            RenderSettings.fogEndDistance = 6500f;

            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "Sky";
            Destroy(sky.GetComponent<Collider>());
            sky.transform.localScale = Vector3.one * 20000f;
            var sr = sky.GetComponent<MeshRenderer>();
            sr.sharedMaterial = Look.Sky();
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            _sky = sky.transform;
        }

        private void BuildWorld()
        {
            var grid = _region.Grid;
            var proj = _region.Projection;

            var ocean = new GameObject("Ocean").AddComponent<OceanSurface>();
            ocean.Init(Look.Ocean());
            ocean.SetRegion(grid);
            ocean.Follow = _cam.transform;

            TerrainBuilder.Build(grid, Look.Lit(Color.white));
            var kp = proj.ToLocal(KronborgLat, KronborgLon);
            if (grid.IsLand(kp))
                TerrainBuilder.Kronborg(Look.ToUnity(kp, (float)grid.TerrainHeightAt(kp)),
                    Look.Lit(new Color(0.78f, 0.72f, 0.62f)), Look.Lit(new Color(0.33f, 0.52f, 0.45f)));

            _buoys = new GameObject("Buoys").AddComponent<BuoyField>();
            _buoys.Build(_region.Seamarks);

            // The Helsingør–Helsingborg ferries, on a timetable that follows the clock so each launch differs.
            if (_region.HasCoast)
            {
                _ferries.AddRange(Ferry.HelsingorHelsingborg(proj, p => grid.NearestWater(p, 6)));
                var ferryRoot = new GameObject("Ferries").transform;
                foreach (var f in _ferries) _ferryVisuals.Add(FerryVisual.Create(f, ferryRoot));
                _ferryClock = System.DateTime.Now.TimeOfDay.TotalSeconds;
            }

            _windField = new GustyWind(_wind.FromDeg * Angles.Deg, _wind.SpeedMs, seed: 7)
            {
                GustStrength = Mathf.Clamp((_wind.GustMs - _wind.SpeedMs) / Mathf.Max(1f, _wind.SpeedMs), 0.1f, 0.45f),
            };
            _waves.SetFromWind(_wind.FromDeg, _wind.SpeedMs);

            var cfg = new BoatConfig();
            IWindField wind = _region.HasCoast ? new LandShadowWind(_windField, grid) : (IWindField)_windField;
            _sim = new BoatSimulator(cfg, wind, CurrentFor(0), grid);
            _start = grid.NearestWater(proj.ToLocal(Region.StartLat, Region.StartLon));
            PlaceAtStart();

            _boat = new GameObject("Boat").AddComponent<BoatVisual>();
            _boat.Build(cfg);
            _boat.Apply(_sim, _waves, Time.time, 0f);
            _cam.Target = _boat.transform;
            _cam.SnapBehind();

            _hud = gameObject.AddComponent<Hud>();
            _hud.Sim = _sim;
            _hud.WindStatus = () => _wind.Status;
            _hud.MapStatus = () => _region.Status;
            _hud.CurrentLabel = () => _currentMode == 0 ? "No current" : _currentMode == 1 ? "Current 1 kn north" : "Current 1 kn south";
            _hud.OnReset = () => { if (_sim.State.Capsized) _sim.Recover(); else PlaceAtStart(); };
            _hud.OnCycleCurrent = () => { _currentMode = (_currentMode + 1) % 3; _sim.Current = CurrentFor(_currentMode); };
            _hud.OnCameraBehind = () => _cam.SnapBehind();
            _hud.OnCycleCourse = CycleCourse;
            _cam.IsOverHud = _hud.IsOverHud;
            _hud.Chart = new ChartView(grid) { Seamarks = _region.Seamarks, Ferries = _ferries };
        }

        private void CycleCourse()
        {
            _courseMode = (_courseMode + 1) % 3;
            PracticeCourse course = null;
            var here = _sim.State.Position;
            var grid = _region.Grid;
            if (_courseMode == 1)
                course = PracticeCourse.WindwardLeeward(here, _wind.FromDeg * Angles.Deg, legLength: 600, laps: 2,
                    buoys: _region.Seamarks, toWater: p => grid.NearestWater(p, 3));
            else if (_courseMode == 2)
            {
                course = PracticeCourse.BuoyTour(here, _region.Seamarks, count: 4, depth: grid);
                if (course.Marks.Count < 2) { _hud.Flash("No buoys nearby: sail closer to the harbour"); course = null; _courseMode = 0; }
            }
            _runner = course != null ? new CourseRunner(course, _sim.State.Time) : null;
            _hud.Runner = _runner;
            _hud.BestTime = course != null ? Format(PlayerPrefs.GetFloat(BestKey, 0)) : "";
            _buoys.ShowCourse(course);
            _buoys.SetNext(_runner?.Next);
            if (course != null) _hud.Flash(course.Name + ": go!");
            else if (_courseMode == 0) _hud.Flash("Free sailing");
        }

        /// <summary>Øresund's stream is mostly driven by wind and sea level, setting north or south through the Sound.</summary>
        private static ICurrentField CurrentFor(int mode) =>
            mode == 0 ? (ICurrentField)UniformCurrent.None
                      : new UniformCurrent(Vec2.FromBearing(mode == 1 ? 350 * Angles.Deg : 170 * Angles.Deg) * Units.FromKnots(1.0));

        /// <summary>Start on a beam reach, stopped, sheet half out.</summary>
        private void PlaceAtStart()
        {
            _sim.State = new BoatState
            {
                Position = _start,
                Heading = Angles.Wrap2Pi(_wind.FromDeg * Angles.Deg + 90 * Angles.Deg),
                Surge = 1.0,
            };
            if (_hud != null) _hud.Sheet = 0.45f;
        }

        private void Update()
        {
            if (_sky != null) _sky.position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float t = Time.time;
            _waves.PushToShaders(t);
            if (!_ready) return;

            _sim.Controls.Steer = _hud.Steer;
            _sim.Controls.Sheet = _hud.Sheet;
            _sim.Controls.AutoHike = true;
            _sim.Advance(Time.deltaTime);
            _boat.Apply(_sim, _waves, t, Time.deltaTime);
            _buoys.Animate(_waves, t, _cam.transform.position);
            UpdateFerries(t);
            var tel = _sim.Telemetry;
            _sound.ApparentWind = (float)tel.ApparentWindSpeed;
            _sound.BoatSpeed = (float)tel.SpeedThroughWater;
            _sound.Luffing = _sim.State.Capsized ? 0 : (float)tel.LuffingFraction;

            if (_runner != null)
            {
                var ev = _runner.Update(_sim.State.Position, _sim.State.Time);
                if (ev == CourseEvent.MarkRounded) { _hud.Flash($"Mark {_runner.NextIndex} rounded"); _buoys.SetNext(_runner.Next); _sound.Chime(); }
                else if (ev == CourseEvent.Finished)
                {
                    float time = (float)_runner.Elapsed(_sim.State.Time);
                    float best = PlayerPrefs.GetFloat(BestKey, 0);
                    bool record = best <= 0 || time < best;
                    if (record) { PlayerPrefs.SetFloat(BestKey, time); PlayerPrefs.Save(); _hud.BestTime = Format(time); }
                    _hud.Flash($"Finished in {Format(time)}" + (record ? ": new best!" : $" (best {Format(best)})"), 6f);
                    _buoys.SetNext(null);
                    _sound.Chime();
                }
            }
        }

        private string BestKey => $"best-{Region.Name}-{_courseMode}";

        private static string Format(float seconds)
        {
            if (seconds <= 0) return "";
            var ts = System.TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}";
        }

        private void UpdateFerries(float t)
        {
            if (_ferries.Count == 0) return;
            double clock = _ferryClock + _sim.State.Time;
            var boat = _sim.State.Position;
            float hornLevel = 0;
            for (int i = 0; i < _ferries.Count; i++)
            {
                var f = _ferries[i];
                f.Update(clock);
                _ferryVisuals[i].Apply(_waves, t);
                if (!_sim.State.Capsized && f.Hits(boat, 1.0))
                {
                    // Run down by the ferry: knocked flat and pushed clear of the hull.
                    var away = Vec2.FromBearing(f.Heading + System.Math.PI / 2);
                    if (Vec2.Dot(boat - f.Position, away) < 0) away = -away;
                    _sim.State.Position = f.Position + away * (Ferry.Beam / 2 + 6) + (boat - f.Position - away * Vec2.Dot(boat - f.Position, away));
                    _sim.State.Surge = 0; _sim.State.Sway = 0;
                    _sim.State.Heel = 1.7; _sim.State.Capsized = true;
                    _hud.Flash($"Hit by the ferry {f.Name}! Ferries can't stop: keep clear.", 5f);
                }
                if (f.IsInPath(boat, 350))
                    hornLevel = Mathf.Max(hornLevel, Mathf.Clamp01(1.2f - (float)Vec2.Distance(boat, f.Position) / 600f));
            }
            if (hornLevel > 0 && Time.time - _lastHorn > 25f)
            {
                _lastHorn = Time.time;
                _sound.HornLevel = hornLevel;
                _sound.Horn();
                _hud.Flash("Ferry horn! You're in its path: get clear.", 4f);
            }
        }

        private void OnGUI()
        {
            if (_ready) return;
            if (_loading == null)
            {
                _loading = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.width / 22f) };
                _loading.normal.textColor = new Color(0.1f, 0.2f, 0.3f);
            }
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, Screen.height * 0.2f), "Tiller & Tide\n" + _region.Status, _loading);
        }
    }
}
