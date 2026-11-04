using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace TillerTide.App
{
    /// <summary>Current wind at the sailing area from Open-Meteo (free, no key). Falls back to a typical westerly.</summary>
    public sealed class LiveWind
    {
        public float FromDeg = 250f;
        public float SpeedMs = 6f;
        public float GustMs = 8.5f;
        public bool IsLive;
        public string Status = "Wind: typical westerly";

        [Serializable] private class Current { public float wind_speed_10m = -1; public float wind_direction_10m = 0; public float wind_gusts_10m = 0; }
        [Serializable] private class Response { public Current current = null; }

        public IEnumerator Fetch(double lat, double lon, Action<LiveWind> done)
        {
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:F3}&longitude={1:F3}&current=wind_speed_10m,wind_direction_10m,wind_gusts_10m&wind_speed_unit=ms",
                lat, lon);
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var r = JsonUtility.FromJson<Response>(req.downloadHandler.text);
                        if (r?.current != null && r.current.wind_speed_10m >= 0)
                        {
                            FromDeg = r.current.wind_direction_10m;
                            SpeedMs = r.current.wind_speed_10m;
                            GustMs = Mathf.Max(SpeedMs, r.current.wind_gusts_10m);
                            IsLive = true;
                            Status = $"Live wind {SpeedMs * 1.944f:F0} kn from {Compass(FromDeg)}";
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("Wind parse failed: " + e.Message); }
                }
                else Debug.LogWarning("Wind fetch failed: " + req.error);
            }
            done?.Invoke(this);
        }

        public static string Compass(float deg)
        {
            string[] names = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            return names[Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 22.5f) % 16];
        }
    }
}
