using System;
using TillerTide.Physics;
using Xunit;

public class CruiserTests
{
    private static BoatSimulator Sim(double twsKnots, double headingDeg, out Autopilot pilot)
    {
        var sim = new BoatSimulator(BoatConfig.Cruiser40(), new SteadyWind(0, Units.FromKnots(twsKnots)));
        sim.State.Heading = headingDeg * Angles.Deg;
        sim.State.Surge = 2;
        sim.Controls.Sheet = sim.Controls.JibSheet = headingDeg / 180;
        pilot = new Autopilot(sim.State.Heading);
        return sim;
    }

    private static void Run(BoatSimulator sim, Autopilot pilot, double seconds)
    {
        for (int i = 0; i < seconds / BoatSimulator.FixedDt; i++) { pilot?.Apply(sim, BoatSimulator.FixedDt); sim.Step(BoatSimulator.FixedDt); }
    }

    [Theory]
    [InlineData(10, 45, 5.5, 6.8)]   // typical 40 ft cruiser: ~6 kn upwind in 10 kn
    [InlineData(10, 90, 6.3, 7.5)]
    [InlineData(10, 150, 4.2, 5.5)]
    [InlineData(22, 90, 8.5, 10.5)]  // a displacement hull: well past hull speed only in a lot of wind, no planing
    public void Polar_matches_a_typical_40_foot_cruiser(double tws, double twa, double min, double max)
    {
        var r = PolarSweep.Sail(BoatConfig.Cruiser40(), tws, twa);
        Assert.InRange(r.BoatSpeedKnots, min, max);
        Assert.False(r.Capsized);
    }

    [Fact]
    public void Upwind_it_heels_and_slips_like_a_keelboat()
    {
        var r = PolarSweep.Sail(BoatConfig.Cruiser40(), 14, 45);
        Assert.InRange(r.HeelDeg, 12, 24);
        Assert.InRange(r.LeewayDeg, 2, 6);
    }

    [Fact]
    public void Ballast_keel_brings_it_back_from_a_knockdown()
    {
        // 35 knots on the beam, everything sheeted in hard and nobody steering: knocked down, but never capsized.
        var sim = Sim(35, 90, out _);
        sim.Controls.Sheet = 0; sim.Controls.JibSheet = 0;
        double maxHeel = 0;
        for (int i = 0; i < 60 / BoatSimulator.FixedDt; i++) { sim.Step(BoatSimulator.FixedDt); maxHeel = Math.Max(maxHeel, Math.Abs(sim.State.Heel)); }
        Assert.False(sim.State.Capsized);
        Assert.True(maxHeel > 30 * Angles.Deg, $"max heel {maxHeel / Angles.Deg:F0}");
        // Let the sheets go: it comes upright.
        sim.Controls.Sheet = 1; sim.Controls.JibSheet = 1;
        for (int i = 0; i < 30 / BoatSimulator.FixedDt; i++) sim.Step(BoatSimulator.FixedDt);
        Assert.True(Math.Abs(sim.State.Heel) < 15 * Angles.Deg, $"heel {sim.State.Heel / Angles.Deg:F0}");
    }

    [Fact]
    public void Reefing_reduces_heel()
    {
        double Heel(int reefs)
        {
            var sim = Sim(20, 55, out var pilot);
            pilot.TrimSheet = false;
            sim.Controls.Sheet = 0.25; sim.Controls.JibSheet = 0.25; sim.Controls.Reef = reefs;
            Run(sim, pilot, 40);
            return Math.Abs(sim.State.Heel);
        }
        Assert.True(Heel(2) < Heel(0) - 2 * Angles.Deg, $"{Heel(0) / Angles.Deg:F1} vs {Heel(2) / Angles.Deg:F1}");
    }

    [Fact]
    public void Jib_sits_to_leeward_and_crosses_when_tacking()
    {
        var sim = Sim(12, 45, out var pilot); // heading NE, wind from the north: on the port bow
        Run(sim, pilot, 20);
        Assert.True(sim.State.Jib > 0, "jib should be out to starboard (leeward)");
        Assert.True(sim.State.Boom > 0);
        pilot.TargetHeading = -45 * Angles.Deg;
        Run(sim, pilot, 25);
        Assert.True(sim.State.Jib < 0, $"jib {sim.State.Jib / Angles.Deg:F0}° after the tack");
        Assert.True(sim.State.Boom < 0);
        Assert.True(sim.Telemetry.SpeedThroughWater > Units.FromKnots(3));
    }

    [Fact]
    public void Dinghy_has_no_jib()
    {
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, 5));
        sim.State.Heading = Math.PI / 2;
        for (int i = 0; i < 600; i++) sim.Step(BoatSimulator.FixedDt);
        Assert.Equal(0, sim.State.Jib);
        Assert.Equal(0, sim.Telemetry.JibLuffingFraction);
    }
}
