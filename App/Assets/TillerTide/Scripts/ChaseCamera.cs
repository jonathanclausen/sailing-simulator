using System;
using UnityEngine;

namespace TillerTide.App
{
    /// <summary>
    /// Chase camera behind the boat. Drag to look around (it drifts back behind the boat when you let go),
    /// pinch or scroll to zoom. Touches that start on the HUD are ignored.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public Func<Vector2, bool> IsOverHud = _ => false; // screen point, origin bottom-left
        public float Distance = 15f;
        public float Pitch = 21f;
        public bool FreeLook;

        private float _yawOffset;
        private float _yaw;
        private int _dragFinger = -1;
        private Vector2 _lastDrag;
        private float _lastPinch;
        private float _releaseTime;

        private void LateUpdate()
        {
            if (Target == null) return;
            HandleInput();

            float targetYaw = Target.eulerAngles.y;
            if (_dragFinger < 0 && !FreeLook && Time.time - _releaseTime > 2.5f)
                _yawOffset = Mathf.MoveTowardsAngle(_yawOffset, 0, 40f * Time.deltaTime);
            // Follow the boat's heading smoothly, so tacking doesn't whip the camera round.
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1 - Mathf.Exp(-1.6f * Time.deltaTime));

            var rot = Quaternion.Euler(Pitch, _yaw + _yawOffset, 0);
            var focus = Target.position + Vector3.up * 2.2f;
            var pos = focus - rot * Vector3.forward * Distance;
            pos.y = Mathf.Max(pos.y, 0.8f);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos));
        }

        public void SnapBehind()
        {
            if (Target != null) _yaw = Target.eulerAngles.y;
            _yawOffset = 0;
        }

        private void HandleInput()
        {
            Distance = Mathf.Clamp(Distance * (1 - Input.mouseScrollDelta.y * 0.1f), 4f, 60f);

            if (Input.touchCount >= 2)
            {
                _dragFinger = -1;
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                if (IsOverHud(a.position) || IsOverHud(b.position)) return;
                float d = Vector2.Distance(a.position, b.position);
                if (b.phase == TouchPhase.Began || a.phase == TouchPhase.Began) _lastPinch = d;
                else if (_lastPinch > 1f) Distance = Mathf.Clamp(Distance * _lastPinch / d, 4f, 60f);
                _lastPinch = d;
                return;
            }

            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began && !IsOverHud(t.position)) { _dragFinger = t.fingerId; _lastDrag = t.position; }
                else if (t.fingerId == _dragFinger && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)) Drag(t.position);
                else if (t.fingerId == _dragFinger) { _dragFinger = -1; _releaseTime = Time.time; }
                return;
            }

            if (Input.touchSupported && Input.touchCount == 0 && _dragFinger >= 0) { _dragFinger = -1; _releaseTime = Time.time; }

            // Mouse (editor / Mac): drag with the left button.
            if (!Input.touchSupported)
            {
                Vector2 m = Input.mousePosition;
                if (Input.GetMouseButtonDown(0) && !IsOverHud(m)) { _dragFinger = 99; _lastDrag = m; }
                else if (Input.GetMouseButton(0) && _dragFinger == 99) Drag(m);
                else if (Input.GetMouseButtonUp(0) && _dragFinger == 99) { _dragFinger = -1; _releaseTime = Time.time; }
            }
        }

        private void Drag(Vector2 p)
        {
            var d = p - _lastDrag;
            _lastDrag = p;
            float scale = 180f / Mathf.Max(400f, Screen.width);
            _yawOffset += d.x * scale;
            Pitch = Mathf.Clamp(Pitch - d.y * scale, 4f, 70f);
        }
    }
}
