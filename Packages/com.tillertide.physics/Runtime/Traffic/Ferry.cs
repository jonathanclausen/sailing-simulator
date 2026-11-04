using System;
using System.Collections.Generic;
using TillerTide.Physics.Region;

namespace TillerTide.Physics.Traffic
{
    /// <summary>
    /// A double-ended ferry shuttling between two berths on a timetable, like the Helsingør–Helsingborg ferries:
    /// wait at the berth, cross, wait, cross back. It never turns round; it just drives the other way.
    /// </summary>
    public sealed class Ferry
    {
        public const double Length = 111, Beam = 28;

        public string Name { get; }
        public Vec2 A { get; }
        public Vec2 B { get; }
        public double DwellSeconds { get; set; } = 300;
        public double CrossingSeconds { get; set; } = 1200;
        public double PhaseSeconds { get; set; }

        public Vec2 Position { get; private set; }
        /// <summary>Bearing of the hull axis, from A towards B.</summary>
        public double Heading { get; }
        /// <summary>Signed speed along the axis (m/s): positive towards B.</summary>
        public double Speed { get; private set; }

        public double CycleSeconds => 2 * (DwellSeconds + CrossingSeconds);

        public Ferry(string name, Vec2 a, Vec2 b)
        {
            Name = name; A = a; B = b;
            Heading = (b - a).Bearing;
            Position = a;
        }

        public void Update(double time)
        {
            double T = CycleSeconds;
            double t = ((time + PhaseSeconds) % T + T) % T;
            double len = Vec2.Distance(A, B);
            double s, v;
            if (t < DwellSeconds) { s = 0; v = 0; }
            else if (t < DwellSeconds + CrossingSeconds) { Profile((t - DwellSeconds) / CrossingSeconds, out s, out v); v *= len / CrossingSeconds; }
            else if (t < 2 * DwellSeconds + CrossingSeconds) { s = 1; v = 0; }
            else { Profile((t - 2 * DwellSeconds - CrossingSeconds) / CrossingSeconds, out s, out v); s = 1 - s; v *= -len / CrossingSeconds; }
            Position = A + (B - A) * s;
            Speed = v;
        }

        // Smooth start and stop (smootherstep) so it eases out of and into the berth.
        private static void Profile(double u, out double s, out double ds)
        {
            s = u * u * u * (u * (u * 6 - 15) + 10);
            ds = 30 * u * u * (u - 1) * (u - 1);
        }

        /// <summary>Position of a point in the ferry's frame: along = towards B, across = to its right.</summary>
        private void Local(Vec2 p, out double along, out double across)
        {
            var axis = Vec2.FromBearing(Heading);
            var right = new Vec2(axis.Y, -axis.X);
            var d = p - Position;
            along = Vec2.Dot(d, axis);
            across = Vec2.Dot(d, right);
        }

        /// <summary>True when the point is inside the hull (plus a margin).</summary>
        public bool Hits(Vec2 p, double margin = 0)
        {
            Local(p, out double along, out double across);
            return Math.Abs(along) <= Length / 2 + margin && Math.Abs(across) <= Beam / 2 + margin;
        }

        /// <summary>True when the ferry is under way and the point is in its path within <paramref name="lookAhead"/> metres.</summary>
        public bool IsInPath(Vec2 p, double lookAhead = 300)
        {
            if (Math.Abs(Speed) < 0.5) return false;
            Local(p, out double along, out double across);
            double ahead = Math.Sign(Speed) * along - Length / 2;
            return ahead > -Length / 2 && ahead < lookAhead && Math.Abs(across) < Beam * 1.5;
        }

        /// <summary>The Helsingør–Helsingborg crossing: berths just off each ferry harbour, three ferries on the route.</summary>
        public static List<Ferry> HelsingorHelsingborg(GeoProjection projection, Func<Vec2, Vec2> toWater = null, int count = 3)
        {
            toWater = toWater ?? (p => p);
            var dk = toWater(projection.ToLocal(56.0372, 12.6205));
            var se = toWater(projection.ToLocal(56.0418, 12.6880));
            string[] names = { "Hamlet", "Tycho", "Aurora", "Mercandia" };
            var list = new List<Ferry>();
            for (int i = 0; i < count; i++)
            {
                var f = new Ferry(names[i % names.Length], dk, se);
                f.PhaseSeconds = f.CycleSeconds * i / count;
                list.Add(f);
            }
            return list;
        }
    }
}
