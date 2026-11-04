using System;

namespace TillerTide.Physics
{
    /// <summary>Wind field. Returns the air velocity (m/s, direction the air moves toward) in the world frame.</summary>
    public interface IWindField
    {
        Vec2 WindAt(Vec2 position, double heightMetres, double time);
    }

    /// <summary>Water current field. Returns water velocity over ground (m/s) in the world frame.</summary>
    public interface ICurrentField
    {
        Vec2 CurrentAt(Vec2 position, double time);
    }

    /// <summary>Tells the simulator where the water is too shallow to sail.</summary>
    public interface IDepthField
    {
        /// <summary>Water depth in metres (≤ 0 means land).</summary>
        double DepthAt(Vec2 position);
    }

    public static class Wind
    {
        /// <summary>
        /// Velocity vector for a wind coming FROM a compass bearing (radians) at a given speed (m/s).
        /// </summary>
        public static Vec2 FromDirection(double fromBearing, double speed) => Vec2.FromBearing(fromBearing) * -speed;

        /// <summary>Power-law wind gradient over water, referenced to 10 m height.</summary>
        public static double ShearFactor(double heightMetres, double exponent = 1.0 / 7.0)
        {
            double h = Math.Max(0.3, heightMetres);
            return Math.Pow(h / 10.0, exponent);
        }
    }

    /// <summary>Steady wind with a realistic height gradient. Speed is given at 10 m.</summary>
    public sealed class SteadyWind : IWindField
    {
        public double FromBearing { get; set; }
        public double Speed10m { get; set; }
        public double ShearExponent { get; set; } = 1.0 / 7.0;

        public SteadyWind(double fromBearing, double speed10m) { FromBearing = fromBearing; Speed10m = speed10m; }

        public Vec2 WindAt(Vec2 position, double heightMetres, double time) =>
            Wind.FromDirection(FromBearing, Speed10m * Wind.ShearFactor(heightMetres, ShearExponent));
    }

    /// <summary>
    /// Wind with oscillating shifts and gust patches that drift downwind. Deterministic for a given seed.
    /// </summary>
    public sealed class GustyWind : IWindField
    {
        public double FromBearing { get; set; }
        public double Speed10m { get; set; }
        public double ShiftAmplitude { get; set; } = 8 * Angles.Deg;
        public double GustStrength { get; set; } = 0.25;
        public double GustCellSize { get; set; } = 120;
        private readonly double _phase;

        public GustyWind(double fromBearing, double speed10m, int seed = 1)
        {
            FromBearing = fromBearing; Speed10m = speed10m;
            _phase = (seed * 2654435761u % 1000) / 1000.0 * 2 * Math.PI;
        }

        private double Shift(double t) =>
            ShiftAmplitude * (0.6 * Math.Sin(t * 0.065 + _phase) + 0.3 * Math.Sin(t * 0.18 + 1.3 + _phase) + 0.1 * Math.Sin(t * 0.45 + 2.1));

        public double Gust(Vec2 p, double t)
        {
            var drift = Vec2.FromBearing(FromBearing) * -(Speed10m * 0.8 * t);
            var q = p - drift;
            double g = Math.Sin(q.X / GustCellSize + _phase) * Math.Sin(q.Y / (GustCellSize * 1.27) + 0.7);
            return Math.Max(-0.6, g);
        }

        public Vec2 WindAt(Vec2 position, double heightMetres, double time)
        {
            double speed = Speed10m * (1 + GustStrength * Gust(position, time)) * Wind.ShearFactor(heightMetres);
            return Wind.FromDirection(FromBearing + Shift(time), speed);
        }
    }

    public sealed class UniformCurrent : ICurrentField
    {
        public Vec2 Velocity { get; set; }
        public UniformCurrent(Vec2 velocity) { Velocity = velocity; }
        public static UniformCurrent None => new UniformCurrent(Vec2.Zero);
        public Vec2 CurrentAt(Vec2 position, double time) => Velocity;
    }

    /// <summary>Tidal stream that floods and ebbs along an axis with a semi-diurnal period.</summary>
    public sealed class TidalCurrent : ICurrentField
    {
        public double FloodBearing { get; set; }
        public double MaxSpeed { get; set; }
        public double PeriodSeconds { get; set; } = 12.42 * 3600;
        public double PhaseSeconds { get; set; }

        public TidalCurrent(double floodBearing, double maxSpeed) { FloodBearing = floodBearing; MaxSpeed = maxSpeed; }

        public Vec2 CurrentAt(Vec2 position, double time) =>
            Vec2.FromBearing(FloodBearing) * (MaxSpeed * Math.Sin(2 * Math.PI * (time + PhaseSeconds) / PeriodSeconds));
    }

    public sealed class OpenWater : IDepthField
    {
        public double Depth { get; }
        public OpenWater(double depth = 50) { Depth = depth; }
        public double DepthAt(Vec2 position) => Depth;
    }
}
