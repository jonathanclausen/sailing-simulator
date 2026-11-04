using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using TillerTide.Physics;
using TillerTide.Physics.Region;
using UnityEngine;
using UnityEngine.Networking;

namespace TillerTide.App
{
    /// <summary>
    /// Downloads the real coastline for a region from OpenStreetMap (Overpass), caches it on the phone, and builds
    /// the depth/land grid off the main thread. Without a network or cache it falls back to open sea.
    /// </summary>
    public sealed class RegionLoader
    {
        public RegionGrid Grid;
        public GeoProjection Projection;
        public string Status = "Loading map…";
        public bool FromCache;

        private const string Endpoint = "https://overpass-api.de/api/interpreter";

        public IEnumerator Load(RegionInfo region, Action<RegionLoader> done)
        {
            var b = region.Bounds;
            Projection = new GeoProjection(b.CentreLat, b.CentreLon);
            var min = Projection.ToLocal(b.South, b.West);
            var max = Projection.ToLocal(b.North, b.East);

            string cachePath = Path.Combine(Application.persistentDataPath, "coast-" + Safe(region.Name) + ".json");
            string json = null;
            if (File.Exists(cachePath))
            {
                json = File.ReadAllText(cachePath);
                FromCache = true;
            }
            else
            {
                Status = "Downloading coastline…";
                var form = new WWWForm();
                form.AddField("data", region.OverpassQuery);
                using (var req = UnityWebRequest.Post(Endpoint, form))
                {
                    req.timeout = 60;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"elements\""))
                    {
                        json = req.downloadHandler.text;
                        try { File.WriteAllText(cachePath, json); } catch (Exception e) { Debug.LogWarning("Cache write failed: " + e.Message); }
                    }
                    else Debug.LogWarning("Coastline download failed: " + req.error);
                }
            }

            if (json == null)
            {
                Grid = RegionGrid.OpenSea(min, max);
                Status = "Offline: open sea (no map yet)";
                done?.Invoke(this);
                yield break;
            }

            Status = "Building map…";
            var proj = Projection;
            var task = Task.Run(() =>
            {
                var coast = Coastline.FromOverpassJson(json, proj);
                return coast.SegmentCount == 0 ? RegionGrid.OpenSea(min, max) : RegionGrid.Build(coast, min, max);
            });
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                Grid = RegionGrid.OpenSea(min, max);
                Status = "Map failed: open sea";
                if (FromCache) File.Delete(cachePath);
            }
            else
            {
                Grid = task.Result;
                Status = region.Name + (FromCache ? " (saved map)" : "");
            }
            done?.Invoke(this);
        }

        private static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
