using System;
using System.Collections.Generic;
using TillerTide.Physics;
using UnityEngine;

namespace TillerTide.App
{
    /// <summary>
    /// Instruments and touch controls. Steering (bottom left, springs back to centre like a tiller you let go of)
    /// and the mainsheet (right edge, stays where you leave it) are read from raw touches, so you can steer and
    /// trim at the same time. Drawn with IMGUI in a 390-point-wide portrait layout.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public BoatSimulator Sim;
        public Func<string> WindStatus = () => "";
        public Func<string> MapStatus = () => "";
        public Func<string> CurrentLabel = () => "";
        public Action OnReset, OnCycleCurrent, OnCameraBehind;

        public float Steer;           // -1..1
        public float Sheet = 0.35f;   // 0 in .. 1 out

        private const float LayoutWidth = 390f;
        private float _scale = 1, _w, _h, _top, _bottom;
        private Rect _steerPad, _sheetBar, _topBar, _buttons;
        private readonly List<Rect> _hudRects = new List<Rect>();
        private int _steerFinger = -1, _sheetFinger = -1;
        private bool _mouseSteer, _mouseSheet;
        private GUIStyle _big, _small, _label, _button, _hint;
        private Texture2D _white;

        private void Layout()
        {
            _scale = Screen.width / LayoutWidth;
            if (Screen.width > Screen.height) _scale = Screen.height / 600f; // landscape (Mac/editor)
            _w = Screen.width / _scale; _h = Screen.height / _scale;
            var safe = Screen.safeArea;
            _top = (Screen.height - safe.yMax) / _scale + 6;
            _bottom = safe.y / _scale + 8;
            _topBar = new Rect(8, _top, _w - 16, 92);
            _steerPad = new Rect(12, _h - _bottom - 92, _w - 130, 84);
            _sheetBar = new Rect(_w - 96, _h - _bottom - 330, 84, 322);
            _buttons = new Rect(12, _h - _bottom - 140, _w - 130, 40);
            _hudRects.Clear();
            _hudRects.Add(_topBar); _hudRects.Add(_steerPad); _hudRects.Add(_sheetBar); _hudRects.Add(_buttons);
        }

        private Vector2 ToGui(Vector2 screen) => new Vector2(screen.x / _scale, (Screen.height - screen.y) / _scale);

        /// <summary>True when a screen point (origin bottom-left) is on a control, so the camera ignores it.</summary>
        public bool IsOverHud(Vector2 screen)
        {
            var g = ToGui(screen);
            foreach (var r in _hudRects) if (r.Contains(g)) return true;
            return false;
        }

        private void Update()
        {
            Layout();
            bool steering = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var g = ToGui(t.position);
                bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                if (t.phase == TouchPhase.Began)
                {
                    if (_steerPad.Contains(g)) _steerFinger = t.fingerId;
                    else if (_sheetBar.Contains(g)) _sheetFinger = t.fingerId;
                }
                if (t.fingerId == _steerFinger)
                {
                    if (ended) _steerFinger = -1;
                    else { SteerFrom(g); steering = true; }
                }
                if (t.fingerId == _sheetFinger)
                {
                    if (ended) _sheetFinger = -1;
                    else SheetFrom(g);
                }
            }

            if (!Input.touchSupported)
            {
                var g = ToGui(Input.mousePosition);
                if (Input.GetMouseButtonDown(0)) { _mouseSteer = _steerPad.Contains(g); _mouseSheet = _sheetBar.Contains(g); }
                if (Input.GetMouseButtonUp(0)) _mouseSteer = _mouseSheet = false;
                if (_mouseSteer) { SteerFrom(g); steering = true; }
                if (_mouseSheet) SheetFrom(g);
            }

            // Keyboard: arrows / A D steer, W S trim, R reset.
            float key = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
            if (key != 0) { Steer = Mathf.MoveTowards(Steer, key, 2.5f * Time.deltaTime); steering = true; }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) Sheet = Mathf.Clamp01(Sheet - 0.5f * Time.deltaTime);
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) Sheet = Mathf.Clamp01(Sheet + 0.5f * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.R)) OnReset?.Invoke();

            if (!steering) Steer = Mathf.MoveTowards(Steer, 0, 3f * Time.deltaTime);
        }

        private void SteerFrom(Vector2 g) => Steer = Mathf.Clamp((g.x - _steerPad.center.x) / (_steerPad.width * 0.42f), -1, 1);
        private void SheetFrom(Vector2 g) => Sheet = Mathf.Clamp01((g.y - _sheetBar.y - 20) / (_sheetBar.height - 40));

        private void Styles()
        {
            if (_big != null) return;
            _white = Texture2D.whiteTexture;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _small = new GUIStyle(_big) { fontSize = 11, fontStyle = FontStyle.Normal };
            _small.normal.textColor = new Color(1, 1, 1, 0.75f);
            _label = new GUIStyle(_small) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _hint = new GUIStyle(_big) { fontSize = 15 };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }

        private void Box(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (Sim == null) return;
            Styles();
            Layout();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            var panel = new Color(0.05f, 0.12f, 0.2f, 0.55f);
            var s = Sim.State; var tel = Sim.Telemetry;

            // Instruments.
            Box(_topBar, panel);
            float cw = _topBar.width / 4;
            Instrument(0, cw, $"{Units.ToKnots(tel.SpeedOverGround):F1}", "SPEED kn");
            Instrument(1, cw, $"{s.Heading * Mathf.Rad2Deg:000}°", "HEADING");
            Instrument(2, cw, $"{Units.ToKnots(tel.TrueWindSpeed):F0}", $"WIND kn {LiveWind.Compass((float)(tel.TrueWindDirection * Mathf.Rad2Deg))}");
            Instrument(3, cw, $"{Mathf.Abs((float)(s.Heel * Mathf.Rad2Deg)):F0}°", "HEEL");
            GUI.Label(new Rect(_topBar.x + 8, _topBar.y + 62, _topBar.width - 16, 14), WindStatus(), _label);
            GUI.Label(new Rect(_topBar.x + 8, _topBar.y + 76, _topBar.width - 16, 14), $"{MapStatus()}  ·  {CurrentLabel()}", _label);

            WindArrow(new Rect(_w / 2 - 34, _topBar.yMax + 8, 68, 68), (float)(tel.ApparentWindAngle * Mathf.Rad2Deg), (float)(tel.TrueWindAngle * Mathf.Rad2Deg));

            // Trim hint.
            string hint; Color hc;
            if (s.Capsized) { hint = "Capsized! Tap Recover"; hc = new Color(1, 0.4f, 0.3f); }
            else if (s.Aground) { hint = "Aground: steer to deeper water"; hc = new Color(1, 0.6f, 0.2f); }
            else if (Mathf.Abs((float)tel.TrueWindAngle) < 38 * Mathf.Deg2Rad && tel.SpeedThroughWater < 0.8) { hint = "In irons: bear away"; hc = new Color(1, 0.8f, 0.3f); }
            else switch (tel.SailFlow)
            {
                case SailFlow.Luffing: hint = "Sail luffing: sheet in"; hc = new Color(1, 0.85f, 0.3f); break;
                case SailFlow.Stalled: hint = "Sail stalled: ease out"; hc = new Color(1, 0.6f, 0.3f); break;
                case SailFlow.Running: hint = "Running: ease right out"; hc = new Color(0.7f, 0.9f, 1f); break;
                default: hint = "Good trim"; hc = new Color(0.5f, 1f, 0.6f); break;
            }
            var old = _hint.normal.textColor; _hint.normal.textColor = hc;
            GUI.Label(new Rect(0, _topBar.yMax + 80, _w, 24), hint, _hint);
            _hint.normal.textColor = old;

            // Buttons.
            float bw = (_buttons.width - 16) / 3;
            if (GUI.Button(new Rect(_buttons.x, _buttons.y, bw, _buttons.height), s.Capsized ? "Recover" : "Reset", _button)) OnReset?.Invoke();
            if (GUI.Button(new Rect(_buttons.x + bw + 8, _buttons.y, bw, _buttons.height), "Current", _button)) OnCycleCurrent?.Invoke();
            if (GUI.Button(new Rect(_buttons.x + 2 * (bw + 8), _buttons.y, bw, _buttons.height), "Camera", _button)) OnCameraBehind?.Invoke();

            // Tiller pad.
            Box(_steerPad, panel);
            Box(new Rect(_steerPad.center.x - 1, _steerPad.y + 8, 2, _steerPad.height - 16), new Color(1, 1, 1, 0.3f));
            float kx = _steerPad.center.x + Steer * _steerPad.width * 0.42f;
            Box(new Rect(kx - 16, _steerPad.y + 14, 32, _steerPad.height - 28), new Color(1f, 0.55f, 0.3f, 0.95f));
            GUI.Label(new Rect(_steerPad.x, _steerPad.yMax - 18, _steerPad.width, 16), "◀ port      STEER      starboard ▶", _small);

            // Mainsheet bar.
            Box(_sheetBar, panel);
            float fill = (_sheetBar.height - 40) * Sheet;
            Box(new Rect(_sheetBar.center.x - 3, _sheetBar.y + 20, 6, _sheetBar.height - 40), new Color(1, 1, 1, 0.25f));
            Box(new Rect(_sheetBar.x + 10, _sheetBar.y + 20 + fill - 12, _sheetBar.width - 20, 24), new Color(0.9f, 0.95f, 1f, 0.95f));
            GUI.Label(new Rect(_sheetBar.x, _sheetBar.y + 2, _sheetBar.width, 16), "IN", _small);
            GUI.Label(new Rect(_sheetBar.x, _sheetBar.yMax - 18, _sheetBar.width, 16), "OUT", _small);
            GUI.Label(new Rect(_sheetBar.x - 70, _sheetBar.y + 20 + fill - 8, 66, 16), $"sheet {Sheet * 100:F0}%", _small);
        }

        private void Instrument(int i, float cw, string value, string caption)
        {
            GUI.Label(new Rect(_topBar.x + i * cw, _topBar.y + 6, cw, 34), value, _big);
            GUI.Label(new Rect(_topBar.x + i * cw, _topBar.y + 38, cw, 16), caption, _small);
        }

        /// <summary>Little boat seen from above with the apparent (solid) and true (faint) wind arrows.</summary>
        private void WindArrow(Rect r, float awaDeg, float twaDeg)
        {
            Box(r, new Color(0.05f, 0.12f, 0.2f, 0.4f));
            var c = r.center;
            Box(new Rect(c.x - 4, c.y - 14, 8, 28), new Color(1f, 0.55f, 0.3f, 0.9f));
            Pointer(c, twaDeg, r.width * 0.45f, new Color(1, 1, 1, 0.35f));
            Pointer(c, awaDeg, r.width * 0.45f, new Color(0.6f, 0.9f, 1f, 1f));
        }

        private void Pointer(Vector2 c, float angleDeg, float radius, Color col)
        {
            // A wind angle of 0 = from dead ahead; draw dots from the rim toward the boat.
            float a = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            for (int k = 0; k < 5; k++)
            {
                var p = c + dir * (radius - k * radius * 0.13f);
                float sz = 3 + k * 0.8f;
                Box(new Rect(p.x - sz / 2, p.y - sz / 2, sz, sz), col);
            }
        }
    }
}
