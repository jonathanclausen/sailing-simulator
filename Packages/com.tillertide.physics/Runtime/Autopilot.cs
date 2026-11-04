using System;

namespace TillerTide.Physics
{
    /// <summary>
    /// Simple helmsman: steers to a heading and trims the sheet to the best angle of attack.
    /// Used by tests, the polar tool and later by AI boats.
    /// </summary>
    public sealed class Autopilot
    {
        public double TargetHeading;
        public double HeadingGain = 2.0;
        public double RateGain = 1.2;
        public bool TrimSheet = true;
        /// <summary>Angle of attack the sheet trimmer aims for (radians).</summary>
        public double TargetAngleOfAttack = 14 * Angles.Deg;
        /// <summary>Heel (radians, to leeward) above which the trimmer eases the sheet to depower.</summary>
        public double MaxHeel = 20 * Angles.Deg;

        public Autopilot(double targetHeading) { TargetHeading = targetHeading; }

        public void Apply(BoatSimulator sim, double dt)
        {
            var s = sim.State;
            double err = Angles.WrapPi(TargetHeading - s.Heading);
            sim.Controls.Steer = Angles.Clamp(HeadingGain * err - RateGain * s.YawRate, -1, 1);
            if (!TrimSheet) return;
            var t = sim.Telemetry;
            // Ease when the angle of attack is too high (stalled), pull in when luffing.
            double aoaErr = t.LuffingFraction > 0.3 ? -0.25 : (t.SailAngleOfAttack - TargetAngleOfAttack);
            double rate = aoaErr / (10 * Angles.Deg);
            // Overpowered: crew fully out and still heeling past the limit, so ease the sheet to spill wind.
            double heelToLeeward = t.BoomSide * s.Heel;
            double over = heelToLeeward - MaxHeel;
            if (over > 0 && s.Hike > 0.9) rate = Math.Max(rate, over / (10 * Angles.Deg));
            sim.Controls.Sheet = Angles.Clamp(sim.Controls.Sheet + dt * 0.8 * rate, 0, 1);
        }
    }
}
