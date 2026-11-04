using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TillerTide.Physics.Region;
using UnityEngine;
using UnityEngine.Networking;

namespace TillerTide.App
{
    /// <summary>
    /// Downloads the real coastline and buoys for a region from OpenStreetMap / OpenSeaMap (Overpass), caches them
    /// on the phone, and builds the depth/land grid off the main thread. Without a network or cache it falls back
    /// to open sea.
    /// </summary>
    public sealed class RegionLoader
    {
        public RegionGrid Grid;
        public GeoProjection Projection;
        public List<Seamark> Seamarks = new List<Seamark>();
        public string Status = "Loading map…";
        public bool FromCache;

        private const string Endpoint = "https://overpass-api.de/api/interpreter";

        public IEnumerator Load(RegionInfo region, Action<RegionLoader> done)
        {
            var b = region.Bounds;
            Projection = new GeoProjection(b.CentreLat, b.CentreLon);
            var min = Projection.ToLocal(b.South, b.West);
            var max = Projection.ToLocal(b.North, b.East);
            string coastPath = CachePath("coast", region);
            string marksPath = CachePath("seamarks", region);

            string coastJson = null, marksJson = null;
            FromCache = File.Exists(coastPath);
            Status = FromCache ? "Loading saved map…" : "Downloading coastline…";
            yield return Fetch(region.OverpassQuery, coastPath, j => coastJson = j);
            Status = "Downloading buoys…";
            yield return Fetch(Physics.Region.Seamarks.OverpassQuery(b), marksPath, j => marksJson = j);

            if (coastJson == null)
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
                var coast = Coastline.FromOverpassJson(coastJson, proj);
                var grid = coast.SegmentCount == 0 ? RegionGrid.OpenSea(min, max) : RegionGrid.Build(coast, min, max);
                var marks = marksJson != null ? Physics.Region.Seamarks.FromOverpassJson(marksJson, proj) : new List<Seamark>();
                return (grid, marks);
            });
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                Grid = RegionGrid.OpenSea(min, max);
                Status = "Map failed: open sea";
                TryDelete(coastPath);
                TryDelete(marksPath);
            }
            else
            {
                Grid = task.Result.grid;
                Seamarks = task.Result.marks;
                Status = $"{region.Name}, {Seamarks.Count} buoys" + (FromCache ? " (saved map)" : "");
            }
            done?.Invoke(this);
        }

        /// <summary>Returns the cached response if there is one, otherwise asks Overpass and saves the answer.</summary>
        private static IEnumerator Fetch(string query, string cachePath, Action<string> result)
        {
            if (File.Exists(cachePath))
            {
                result(File.ReadAllText(cachePath));
                yield break;
            }
            var form = new WWWForm();
            form.AddField("data", query);
            using (var req = UnityWebRequest.Post(Endpoint, form))
            {
                req.timeout = 90;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.text.Contains("\"elements\""))
                {
                    string json = req.downloadHandler.text;
                    try { File.WriteAllText(cachePath, json); } catch (Exception e) { Debug.LogWarning("Cache write failed: " + e.Message); }
                    result(json);
                }
                else Debug.LogWarning("Overpass request failed: " + req.error);
            }
        }

        private static string CachePath(string kind, RegionInfo region)
        {
            string name = region.Name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, $"{kind}-{name.Replace(' ', '_')}.json");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }
}
