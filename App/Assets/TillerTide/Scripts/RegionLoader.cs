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
        public bool HasRealDepths;
        public bool HasCoast;
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
            string depthPath = CachePath("depth", region, ".tif");

            string coastJson = null, marksJson = null;
            FromCache = File.Exists(coastPath);
            Status = FromCache ? "Loading saved map…" : "Downloading coastline…";
            yield return Fetch(region.OverpassQuery, coastPath, j => coastJson = j);
            Status = "Downloading buoys…";
            yield return Fetch(Physics.Region.Seamarks.OverpassQuery(b), marksPath, j => marksJson = j);
            byte[] depthTiff = null;
            Status = "Downloading water depths…";
            yield return FetchBytes(Bathymetry.EmodnetUrl(b), depthPath, d => depthTiff = d);

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
                bool depths = false;
                if (depthTiff != null)
                {
                    try { grid.SetBathymetry(Bathymetry.FromGeoTiff(depthTiff, b), proj); depths = true; }
                    catch (Exception e) { Debug.LogWarning("Depth data unreadable: " + e.Message); TryDelete(depthPath); }
                }
                return (grid, marks, depths);
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
                HasRealDepths = task.Result.depths;
                HasCoast = true;
                Status = $"{region.Name}, {Seamarks.Count} buoys" + (HasRealDepths ? ", real depths" : "") + (FromCache ? " (saved)" : "");
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

        private static IEnumerator FetchBytes(string url, string cachePath, Action<byte[]> result)
        {
            if (File.Exists(cachePath))
            {
                result(File.ReadAllBytes(cachePath));
                yield break;
            }
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 60;
                yield return req.SendWebRequest();
                var data = req.downloadHandler.data;
                // A WCS error comes back as XML, so only keep answers that start like a TIFF.
                if (req.result == UnityWebRequest.Result.Success && data != null && data.Length > 8 &&
                    ((data[0] == 'I' && data[1] == 'I') || (data[0] == 'M' && data[1] == 'M')))
                {
                    try { File.WriteAllBytes(cachePath, data); } catch (Exception e) { Debug.LogWarning("Cache write failed: " + e.Message); }
                    result(data);
                }
                else Debug.LogWarning("Depth download failed: " + (req.error ?? "not a TIFF"));
            }
        }

        private static string CachePath(string kind, RegionInfo region, string ext = ".json")
        {
            string name = region.Name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, $"{kind}-{name.Replace(' ', '_')}{ext}");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }
}
