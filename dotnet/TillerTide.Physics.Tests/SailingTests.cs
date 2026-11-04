using System;
using TillerTide.Physics;
using Xunit;

public class SailingTests
{
    private static BoatSimulator Boat(double twsKnots, double headingDeg, double startSpeed = 1.5)
    {
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, Units.FromKnots(twsKnots)));
        sim.State.Heading = headingDeg * Angles.Deg;
        sim.State.Surge = startSpeed;
        return sim;
    }

    private static void Run(BoatSimulator sim, double seconds, Autopilot pilot = null)
    {
        int steps = (int)(seconds / BoatSimulator.FixedDt);
        for (int i = 0; i < steps; i++)
        {
            pilot?.Apply(sim, BoatSimulator.FixedDt);
            sim.Step(BoatSimulator.FixedDt);
        }
    }

    [Fact]
    public void Head_to_wind_the_boat_stops_in_irons()
    {
        var sim = Boat(12, 0, startSpeed: 1.5);
        sim.Controls.Sheet = 0.2;
        Run(sim, 1);
        Assert.Equal(SailFlow.Luffing, sim.Telemetry.SailFlow);
        Run(sim, 3);
        // Losing way fast; after a while the bow falls off onto one tack, as a real boat does.
        Assert.True(sim.Telemetry.SpeedThroughWater < 1.5 * 0.7, $"speed {sim.Telemetry.SpeedThroughWater}");
    }

    [Fact]
    public void Polar_has_the_shape_of_a_real_dinghy()
    {
        var cfg = new BoatConfig();
        double up = PolarSweep.Sail(cfg, 12, 45).BoatSpeedKnots;
        double reach = PolarSweep.Sail(cfg, 12, 100).BoatSpeedKnots;
        double run = PolarSweep.Sail(cfg, 12, 180).BoatSpeedKnots;
        Assert.InRange(up, 3.5, 6.5);
        Assert.InRange(reach, 5.0, 10.0);
        Assert.InRange(run, 3.0, 6.0);
        Assert.True(reach > up && reach > run, $"up {up} reach {reach} run {run}");
    }

    [Fact]
    public void Planes_on_a_reach_in_strong_wind()
    {
        var r = PolarSweep.Sail(new BoatConfig(), 20, 100);
        // Displacement hull speed of a 3.9 m waterline is about 5 kn; planing takes it well beyond.
        Assert.True(r.BoatSpeedKnots > 8, $"speed {r.BoatSpeedKnots}");
        Assert.False(r.Capsized);
    }

    [Fact]
    public void More_wind_means_more_speed_on_a_reach()
    {
        var cfg = new BoatConfig();
        double v6 = PolarSweep.Sail(cfg, 6, 90).BoatSpeedKnots;
        double v10 = PolarSweep.Sail(cfg, 10, 90).BoatSpeedKnots;
        double v16 = PolarSweep.Sail(cfg, 16, 90).BoatSpeedKnots;
        Assert.True(v6 < v10 && v10 < v16, $"{v6} {v10} {v16}");
    }

    [Fact]
    public void Leeway_close_hauled_is_a_few_degrees()
    {
        var r = PolarSweep.Sail(new BoatConfig(), 12, 45);
        Assert.InRange(r.LeewayDeg, 1, 8);
    }

    [Fact]
    public void Positive_steer_turns_the_bow_to_starboard()
    {
        var sim = Boat(10, 90, startSpeed: 2.5);
        sim.Controls.Sheet = 0.4;
        sim.Controls.Steer = 0.6;
        Run(sim, 2);
        Assert.True(sim.State.YawRate > 0.05, $"yaw rate {sim.State.YawRate}");
    }

    [Fact]
    public void Rudder_does_nothing_without_flow()
    {
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, 0));
        sim.Controls.Steer = 1;
        Run(sim, 3);
        Assert.True(Math.Abs(sim.State.YawRate) < 1e-6);
    }

    [Fact]
    public void Current_carries_a_boat_in_calm_air()
    {
        var current = new Vec2(0.5, 0); // 0.5 m/s toward the east
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, 0), new UniformCurrent(current));
        Run(sim, 30);
        // Drifting with the water makes an apparent breeze, so windage on the rig holds the boat back a little.
        Assert.InRange(sim.State.Position.X, 13.0, 15.0);
        Assert.InRange(sim.Telemetry.SpeedOverGround, 0.4, 0.5);
        Assert.True(sim.Telemetry.SpeedThroughWater < 0.1);
    }

    [Fact]
    public void Boat_coasts_to_a_stop_with_no_wind()
    {
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, 0));
        sim.State.Surge = 3;
        double last = 3;
        for (int i = 0; i < 30; i++)
        {
            Run(sim, 1);
            Assert.True(sim.State.Surge <= last + 1e-9);
            last = sim.State.Surge;
        }
        // Friction falls with the square of speed, so a light dinghy glides a long way before stopping.
        Assert.True(last < 1.2, $"speed {last}");
    }

    [Fact]
    public void Too_much_wind_with_nobody_hiking_capsizes_the_boat()
    {
        var sim = Boat(25, 90, startSpeed: 2);
        sim.Controls.AutoHike = false;
        sim.Controls.Hike = 0;
        sim.Controls.Sheet = 0.15;
        Run(sim, 10);
        Assert.True(sim.State.Capsized);
    }

    [Fact]
    public void Hiking_keeps_the_boat_up_where_sitting_in_does_not()
    {
        var hiked = Boat(16, 60, startSpeed: 2);
        hiked.Controls.Sheet = 0.3;
        var pilot = new Autopilot(60 * Angles.Deg) { TrimSheet = false };
        Run(hiked, 20, pilot);
        Assert.False(hiked.State.Capsized);
        Assert.True(Math.Abs(hiked.State.Heel) < 35 * Angles.Deg, $"heel {hiked.State.Heel / Angles.Deg}");
    }

    [Fact]
    public void Sheet_too_far_out_luffs_and_too_far_in_on_a_run_stalls()
    {
        var luff = Boat(12, 50, startSpeed: 2);
        luff.Controls.Sheet = 1;
        Run(luff, 3);
        Assert.Equal(SailFlow.Luffing, luff.Telemetry.SailFlow);

        var stall = Boat(12, 150, startSpeed: 2);
        stall.Controls.Sheet = 0;
        Run(stall, 3);
        Assert.Equal(SailFlow.Stalled, stall.Telemetry.SailFlow);
    }

    [Fact]
    public void Running_aground_stops_the_boat()
    {
        var sim = Boat(12, 90, startSpeed: 2);
        sim.Depth = new Shoal(eastOf: 20);
        Run(sim, 30);
        Assert.True(sim.State.Aground);
        Assert.True(sim.State.Position.X < 20);
    }

    [Fact]
    public void Simulation_is_deterministic()
    {
        BoatSimulator Make()
        {
            var s = new BoatSimulator(new BoatConfig(), new GustyWind(350 * Angles.Deg, 6, seed: 7), new TidalCurrent(90 * Angles.Deg, 0.4));
            s.State.Heading = 45 * Angles.Deg; s.State.Surge = 2;
            return s;
        }
        var a = Make(); var b = Make();
        var pa = new Autopilot(45 * Angles.Deg); var pb = new Autopilot(45 * Angles.Deg);
        Run(a, 60, pa); Run(b, 60, pb);
        Assert.Equal(a.State.Position, b.State.Position);
        Assert.Equal(a.State.Heading, b.State.Heading);
    }

    [Fact]
    public void Advance_uses_fixed_steps_whatever_the_frame_rate()
    {
        var a = Boat(10, 60); var b = Boat(10, 60);
        for (int i = 0; i < 600; i++) a.Advance(1.0 / 60);
        for (int i = 0; i < 300; i++) b.Advance(1.0 / 30);
        Assert.Equal(a.State.Time, b.State.Time, 6);
        Assert.Equal(a.State.Position.X, b.State.Position.X, 6);
    }

    [Fact]
    public void Wind_is_stronger_higher_up()
    {
        var w = new SteadyWind(0, 6);
        Assert.True(w.WindAt(Vec2.Zero, 6, 0).Length > w.WindAt(Vec2.Zero, 1, 0).Length);
        Assert.Equal(6, w.WindAt(Vec2.Zero, 10, 0).Length, 6);
    }

    [Fact]
    public void Wind_from_north_blows_south()
    {
        var v = Wind.FromDirection(0, 5);
        Assert.Equal(0, v.X, 9);
        Assert.Equal(-5, v.Y, 9);
    }

    private sealed class Shoal : IDepthField
    {
        private readonly double _x;
        public Shoal(double eastOf) { _x = eastOf; }
        public double DepthAt(Vec2 p) => p.X > _x ? 0.3 : 10;
    }
}
