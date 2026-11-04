using System;

namespace TillerTide.Physics
{
    /// <summary>Result of sailing at a fixed true wind angle until the speed settles.</summary>
    public struct SteadySailing
    {
        public double TrueWindSpeedKnots;
        public double TrueWindAngleDeg;
        public double BoatSpeedKnots;
        public double HeelDeg;
        public double LeewayDeg;
        public double Sheet;
        public double Hike;
        public double VmgKnots;
        public bool Capsized;
    }

    /// <summary>
    /// Velocity-prediction by simulation: holds a true wind angle with the autopilot and averages the settled
    /// speed. This is how the model is checked against real boat polars.
    /// </summary>
    public static class PolarSweep
    {
        public static SteadySailing Sail(BoatConfig config, double twsKnots, double twaDeg,
                                         double settleSeconds = 60, double measureSeconds = 30)
        {
            const double windFrom = 0; // north
            var sim = new BoatSimulator(config, new SteadyWind(windFrom, Units.FromKnots(twsKnots)));
            double heading = Angles.Wrap2Pi(windFrom + twaDeg * Angles.Deg);
            sim.State.Heading = heading;
            sim.State.Surge = 1.0;
            sim.Controls = BoatControls.Default;
            sim.Controls.Sheet = Angles.Clamp(twaDeg / 180.0, 0, 1);
            var pilot = new Autopilot(heading);

            double dt = BoatSimulator.FixedDt, sumSpeed = 0, sumHeel = 0, sumLee = 0, sumVmg = 0;
            int steps = (int)((settleSeconds + measureSeconds) / dt), settle = (int)(settleSeconds / dt), count = 0;
            for (int i = 0; i < steps; i++)
            {
                pilot.Apply(sim, dt);
                sim.Step(dt);
                if (i >= settle)
                {
                    sumSpeed += sim.Telemetry.SpeedThroughWater;
                    sumHeel += Math.Abs(sim.State.Heel);
                    sumLee += Math.Abs(sim.Telemetry.Leeway);
                    sumVmg += sim.Telemetry.VelocityMadeGood;
                    count++;
                }
            }
            return new SteadySailing
            {
                TrueWindSpeedKnots = twsKnots,
                TrueWindAngleDeg = twaDeg,
                BoatSpeedKnots = Units.ToKnots(sumSpeed / count),
                HeelDeg = sumHeel / count / Angles.Deg,
                LeewayDeg = sumLee / count / Angles.Deg,
                Sheet = sim.Controls.Sheet,
                Hike = sim.State.Hike,
                VmgKnots = Units.ToKnots(sumVmg / count),
                Capsized = sim.State.Capsized,
            };
        }
    }
}
