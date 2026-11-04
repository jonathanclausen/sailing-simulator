using System;
using TillerTide.Physics;
using TillerTide.Physics.Region;
using Xunit;

public class RegionTests
{
    // A square island walked counter-clockwise (land on the left), as OpenStreetMap stores closed coastlines.
    private static Coastline Island(double cx, double cy, double half)
    {
        var c = new Coastline();
        c.Lines.Add(new[]
        {
            new Vec2(cx - half, cy - half), new Vec2(cx + half, cy - half), new Vec2(cx + half, cy + half),
            new Vec2(cx - half, cy + half), new Vec2(cx - half, cy - half),
        });
        return c;
    }

    [Fact]
    public void Island_is_land_inside_and_sea_outside()
    {
        var grid = RegionGrid.Build(Island(0, 0, 200), new Vec2(-1000, -1000), new Vec2(1000, 1000));
        Assert.True(grid.IsLand(new Vec2(0, 0)));
        Assert.True(grid.IsLand(new Vec2(180, 180)));   // near a convex corner, inside
        Assert.False(grid.IsLand(new Vec2(230, 230)));  // near the corner, outside
        Assert.False(grid.IsLand(new Vec2(-800, 600)));
        Assert.True(grid.DepthAt(new Vec2(-800, 600)) > 20);
        Assert.True(grid.DepthAt(new Vec2(210, 0)) < 1.5); // shallow right off the beach
    }

    [Fact]
    public void Open_coastline_puts_land_on_the_left()
    {
        // Walking north along x = 300: west of it is land, east of it is the sound.
        var c = new Coastline();
        c.Lines.Add(new[] { new Vec2(300, -3000), new Vec2(320, 0), new Vec2(300, 3000) });
        var grid = RegionGrid.Build(c, new Vec2(-2000, -2000), new Vec2(2000, 2000), cellSize: 50, searchRadius: 800);
        Assert.True(grid.IsLand(new Vec2(-100, 0)));
        Assert.True(grid.IsLand(new Vec2(-1900, 1500)));  // beyond the search radius, filled from neighbours
        Assert.False(grid.IsLand(new Vec2(600, 0)));
        Assert.False(grid.IsLand(new Vec2(1900, -1500)));
        Assert.True(grid.TerrainHeightAt(new Vec2(-1500, 0)) > 5);
    }

    [Fact]
    public void Boat_runs_aground_on_a_real_shoreline()
    {
        var grid = RegionGrid.Build(Island(0, 300, 150), new Vec2(-1000, -1000), new Vec2(1000, 1000));
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(Math.PI / 2, 5), depth: grid);
        sim.State.Heading = 0; sim.State.Surge = 2; // sailing north toward the island on a beam reach
        sim.Controls.Sheet = 0.45;
        for (int i = 0; i < 120 * 120 && !sim.State.Aground; i++) sim.Step(BoatSimulator.FixedDt);
        Assert.True(sim.State.Aground);
        Assert.InRange(sim.State.Position.Y, 100, 160);
    }

    [Fact]
    public void Nearest_water_moves_a_start_point_off_the_land()
    {
        var grid = RegionGrid.Build(Island(0, 0, 200), new Vec2(-1000, -1000), new Vec2(1000, 1000));
        var p = grid.NearestWater(new Vec2(0, 0), minDepth: 3);
        Assert.False(grid.IsLand(p));
        Assert.True(grid.DepthAt(p) >= 3);
    }

    [Fact]
    public void Parses_overpass_coastline_json()
    {
        const string json = @"{""version"":0.6,""elements"":[
          {""type"":""way"",""id"":1,""bounds"":{""minlat"":56.0},""geometry"":[{""lat"":56.0300,""lon"":12.6100},{""lat"":56.0400,""lon"":12.6200}],""tags"":{""natural"":""coastline""}},
          {""type"":""way"",""id"":2,""geometry"":[{""lat"":56.05,""lon"":12.6},{""lat"":56.06,""lon"":12.61},{""lat"":56.07,""lon"":12.6}]}]}";
        var proj = new GeoProjection(56.03, 12.61);
        var coast = Coastline.FromOverpassJson(json, proj);
        Assert.Equal(2, coast.Lines.Count);
        Assert.Equal(3, coast.SegmentCount);
        Assert.Equal(0, coast.Lines[0][0].X, 6);
        Assert.Equal(0, coast.Lines[0][0].Y, 6);
        // 0.01° of latitude is about 1112 m; 0.01° of longitude at 56° N is about 622 m.
        Assert.InRange(coast.Lines[0][1].Y, 1105, 1118);
        Assert.InRange(coast.Lines[0][1].X, 615, 628);
    }

    [Fact]
    public void Projection_round_trips()
    {
        var proj = new GeoProjection(56.04, 12.62);
        var p = proj.ToLocal(56.05, 12.65);
        var (lat, lon) = proj.ToGeo(p);
        Assert.Equal(56.05, lat, 9);
        Assert.Equal(12.65, lon, 9);
    }

    [Fact]
    public void Helsingor_query_asks_for_coastline_in_its_box()
    {
        var q = Regions.Helsingor.OverpassQuery;
        Assert.Contains("natural\"=\"coastline", q);
        Assert.Contains("55.985,12.54,56.095,12.76", q);
    }
}

public class LandShadowTests
{
    [Fact]
    public void Wind_off_the_land_is_weaker_near_the_beach()
    {
        // Land to the west of x = 0, wind from the west (270°).
        var c = new Coastline();
        c.Lines.Add(new[] { new Vec2(0, -5000), new Vec2(0, 5000) });
        var grid = RegionGrid.Build(c, new Vec2(-3000, -3000), new Vec2(3000, 3000), cellSize: 50, searchRadius: 2000);
        var wind = new LandShadowWind(new SteadyWind(270 * Angles.Deg, 8), grid);
        double near = wind.WindAt(new Vec2(60, 0), 3, 0).Length;
        double mid = wind.WindAt(new Vec2(500, 0), 3, 0).Length;
        double far = wind.WindAt(new Vec2(2800, 0), 3, 0).Length;
        double open = new SteadyWind(270 * Angles.Deg, 8).WindAt(new Vec2(2800, 0), 3, 0).Length;
        Assert.True(near < mid && mid < far, $"{near:F2} {mid:F2} {far:F2}");
        Assert.InRange(near / open, 0.45, 0.7);
        Assert.InRange(far / open, 0.97, 1.0);
        Assert.InRange(wind.FetchToLand(new Vec2(500, 0), 270 * Angles.Deg).Value, 450, 560);

        // An onshore breeze (from the east) has nothing upwind: no shadow.
        var onshore = new LandShadowWind(new SteadyWind(90 * Angles.Deg, 8), grid);
        Assert.Equal(new SteadyWind(90 * Angles.Deg, 8).WindAt(new Vec2(60, 0), 3, 0).Length, onshore.WindAt(new Vec2(60, 0), 3, 0).Length, 6);
    }
}
