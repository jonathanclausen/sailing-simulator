using System;

namespace TillerTide.Physics.Region
{
    /// <summary>
    /// Maps latitude/longitude to local metres around an origin (X east, Y north).
    /// An equirectangular projection is accurate to well under a metre over a 20 km sailing area.
    /// </summary>
    public sealed class GeoProjection
    {
        private const double EarthRadius = 6371008.8;
        public double OriginLat { get; }
        public double OriginLon { get; }
        private readonly double _cosLat;

        public GeoProjection(double originLat, double originLon)
        {
            OriginLat = originLat;
            OriginLon = originLon;
            _cosLat = Math.Cos(originLat * Angles.Deg);
        }

        public Vec2 ToLocal(double lat, double lon) =>
            new Vec2((lon - OriginLon) * Angles.Deg * EarthRadius * _cosLat, (lat - OriginLat) * Angles.Deg * EarthRadius);

        public (double lat, double lon) ToGeo(Vec2 p) =>
            (OriginLat + p.Y / EarthRadius / Angles.Deg, OriginLon + p.X / (EarthRadius * _cosLat) / Angles.Deg);
    }

    /// <summary>A rectangle in latitude/longitude.</summary>
    public readonly struct GeoBounds
    {
        public readonly double South, West, North, East;
        public GeoBounds(double south, double west, double north, double east) { South = south; West = west; North = north; East = east; }
        public double CentreLat => (South + North) / 2;
        public double CentreLon => (West + East) / 2;
    }

    /// <summary>Sailing areas the app knows about.</summary>
    public static class Regions
    {
        /// <summary>Helsingør and the narrowest part of Øresund, with Helsingborg on the Swedish side.</summary>
        public static readonly RegionInfo Helsingor = new RegionInfo(
            "Helsingør", new GeoBounds(55.985, 12.540, 56.095, 12.760), startLat: 56.0345, startLon: 12.6420, startHeadingDeg: 0);
    }

    public sealed class RegionInfo
    {
        public string Name { get; }
        public GeoBounds Bounds { get; }
        public double StartLat { get; }
        public double StartLon { get; }
        public double StartHeadingDeg { get; }

        public RegionInfo(string name, GeoBounds bounds, double startLat, double startLon, double startHeadingDeg)
        {
            Name = name; Bounds = bounds; StartLat = startLat; StartLon = startLon; StartHeadingDeg = startHeadingDeg;
        }

        /// <summary>Overpass API query for every OpenStreetMap coastline way in the area.</summary>
        public string OverpassQuery =>
            FormattableString.Invariant(
                $"[out:json][timeout:60];way[\"natural\"=\"coastline\"]({Bounds.South},{Bounds.West},{Bounds.North},{Bounds.East});out geom;");
    }
}
