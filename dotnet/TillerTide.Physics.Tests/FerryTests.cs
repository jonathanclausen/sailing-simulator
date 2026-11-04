using System;
using TillerTide.Physics;
using TillerTide.Physics.Region;
using TillerTide.Physics.Traffic;
using Xunit;

public class FerryTests
{
    private static Ferry Make() => new Ferry("Test", new Vec2(0, 0), new Vec2(4000, 0)) { DwellSeconds = 300, CrossingSeconds = 1000 };

    [Fact]
    public void Ferry_waits_crosses_and_comes_back()
    {
        var f = Make();
        f.Update(100);
        Assert.Equal(0, f.Position.X, 3);
        Assert.Equal(0, f.Speed, 3);
        f.Update(800); // half way across
        Assert.Equal(2000, f.Position.X, 0);
        Assert.InRange(f.Speed, 7, 8); // peak 1.875 x average 4 m/s ≈ 14.6 kn, like the real ferries
        f.Update(1450);
        Assert.Equal(4000, f.Position.X, 3);
        f.Update(1600 + 500);
        Assert.Equal(2000, f.Position.X, 0);
        Assert.True(f.Speed < -7);
        Assert.Equal(90 * Angles.Deg, f.Heading, 6); // double-ended: never turns
    }

    [Fact]
    public void Hull_and_path_checks()
    {
        var f = Make();
        f.Update(800);
        Assert.True(f.Hits(new Vec2(2040, 10)));
        Assert.False(f.Hits(new Vec2(2040, 20)));
        Assert.True(f.IsInPath(new Vec2(2200, 5)));     // ahead of the bow
        Assert.False(f.IsInPath(new Vec2(1800, 5)));    // behind it
        Assert.False(f.IsInPath(new Vec2(2200, 120)));  // well off to the side
        f.Update(100);
        Assert.False(f.IsInPath(new Vec2(100, 0)));     // at the berth
    }

    [Fact]
    public void Helsingor_route_crosses_the_sound_with_staggered_ferries()
    {
        var proj = new GeoProjection(56.04, 12.65);
        var ferries = Ferry.HelsingorHelsingborg(proj);
        Assert.Equal(3, ferries.Count);
        double len = Vec2.Distance(ferries[0].A, ferries[0].B);
        Assert.InRange(len, 3800, 4600);
        foreach (var f in ferries) f.Update(0);
        Assert.NotEqual(ferries[0].Position, ferries[1].Position);
    }
}
