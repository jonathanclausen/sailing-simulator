using System;

namespace TillerTide.Physics
{
    /// <summary>Piecewise-linear lookup table y(x), x ascending.</summary>
    public sealed class Table
    {
        private readonly double[] _x;
        private readonly double[] _y;

        public Table(params (double x, double y)[] points)
        {
            if (points.Length < 2) throw new ArgumentException("Need at least two points");
            _x = new double[points.Length];
            _y = new double[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                _x[i] = points[i].x;
                _y[i] = points[i].y;
                if (i > 0 && _x[i] <= _x[i - 1]) throw new ArgumentException("x must be ascending");
            }
        }

        public double this[double x]
        {
            get
            {
                if (x <= _x[0]) return _y[0];
                int n = _x.Length;
                if (x >= _x[n - 1]) return _y[n - 1];
                int i = 1;
                while (x > _x[i]) i++;
                double t = (x - _x[i - 1]) / (_x[i] - _x[i - 1]);
                return _y[i - 1] + (_y[i] - _y[i - 1]) * t;
            }
        }
    }

    /// <summary>Lift and drag coefficients of a soft sail against angle of attack.</summary>
    public sealed class SailPolar
    {
        public Table Lift { get; }
        public Table Drag { get; }
        /// <summary>Drag coefficient of a luffing (flogging) sail.</summary>
        public double LuffDrag { get; }

        public SailPolar(Table lift, Table drag, double luffDrag)
        {
            Lift = lift; Drag = drag; LuffDrag = luffDrag;
        }

        /// <summary>
        /// Generic single-sail (cat rig) mainsail. Shape follows published wind-tunnel data for
        /// soft sails: lift peaks around 18–22°, then the flow separates and drag dominates.
        /// </summary>
        public static SailPolar Mainsail() => new SailPolar(
            new Table((0, 0.0), (5, 0.55), (10, 1.0), (15, 1.32), (20, 1.45), (25, 1.38), (30, 1.25),
                      (45, 1.0), (60, 0.72), (75, 0.38), (90, 0.0), (180, 0.0)),
            new Table((0, 0.06), (5, 0.07), (10, 0.10), (15, 0.16), (20, 0.25), (25, 0.36), (30, 0.48),
                      (45, 0.78), (60, 1.02), (75, 1.18), (90, 1.28), (180, 1.28)),
            luffDrag: 0.12);

        /// <summary>Angle of attack (deg) above which the flow is considered stalled.</summary>
        public const double StallAngleDeg = 25;
    }

    /// <summary>
    /// Underwater foil (centreboard, keel, rudder) with a symmetric section of finite aspect ratio.
    /// Lift slope from lifting-line theory, stall around 12–14°.
    /// </summary>
    public sealed class FoilPolar
    {
        public double AspectRatio { get; }
        public double StallAngle { get; }
        public double ProfileDrag { get; }
        private readonly double _slope;

        public FoilPolar(double aspectRatio, double stallAngleDeg = 13, double profileDrag = 0.009)
        {
            AspectRatio = aspectRatio;
            StallAngle = stallAngleDeg * Angles.Deg;
            ProfileDrag = profileDrag;
            _slope = 2 * Math.PI * aspectRatio / (aspectRatio + 2);
        }

        /// <summary>Lift coefficient for angle of attack (radians, signed).</summary>
        public double Lift(double alpha)
        {
            double a = Math.Abs(alpha), s = Math.Sign(alpha);
            if (a <= StallAngle) return s * _slope * a;
            double peak = _slope * StallAngle;
            // Post-stall: drop to about 60% of peak, then follow a flat-plate curve toward 90°.
            double flat = 1.1 * Math.Sin(2 * a);
            double blend = Angles.Clamp((a - StallAngle) / (8 * Angles.Deg), 0, 1);
            return s * ((1 - blend) * peak + blend * Math.Min(0.6 * peak, flat));
        }

        public double Drag(double alpha)
        {
            double cl = Lift(alpha);
            double induced = cl * cl / (Math.PI * 0.9 * AspectRatio);
            double a = Math.Abs(alpha);
            double separation = a > StallAngle ? 1.8 * Math.Pow(Math.Sin(a), 2) : 0;
            return ProfileDrag + induced + separation;
        }
    }
}
