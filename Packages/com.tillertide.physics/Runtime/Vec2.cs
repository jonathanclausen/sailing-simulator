using System;

namespace TillerTide.Physics
{
    /// <summary>
    /// 2D vector in the horizontal plane. World frame: X = east, Y = north (metres).
    /// Boat frame: X = forward, Y = starboard.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;
        public Vec2 Normalized { get { var l = Length; return l > 1e-12 ? new Vec2(X / l, Y / l) : Zero; } }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;

        /// <summary>Unit vector pointing along a compass bearing (0 = north, clockwise, radians).</summary>
        public static Vec2 FromBearing(double bearing) => new Vec2(Math.Sin(bearing), Math.Cos(bearing));

        /// <summary>Compass bearing this vector points towards (radians, 0..2π).</summary>
        public double Bearing => Angles.Wrap2Pi(Math.Atan2(X, Y));

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    public static class Angles
    {
        public const double Deg = Math.PI / 180.0;

        /// <summary>Wrap to (-π, π].</summary>
        public static double WrapPi(double a)
        {
            a %= 2 * Math.PI;
            if (a <= -Math.PI) a += 2 * Math.PI;
            else if (a > Math.PI) a -= 2 * Math.PI;
            return a;
        }

        /// <summary>Wrap to [0, 2π).</summary>
        public static double Wrap2Pi(double a)
        {
            a %= 2 * Math.PI;
            if (a < 0) a += 2 * Math.PI;
            return a;
        }

        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public static class Units
    {
        public const double MetresPerSecondToKnots = 1.943844;
        public static double ToKnots(double ms) => ms * MetresPerSecondToKnots;
        public static double FromKnots(double kn) => kn / MetresPerSecondToKnots;
    }
}
