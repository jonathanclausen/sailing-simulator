using System;
using System.Collections.Generic;
using TillerTide.Physics;
using TillerTide.Physics.Racing;
using TillerTide.Physics.Region;
using Xunit;

public class CourseTests
{
    private static readonly GeoProjection Proj = new GeoProjection(56.04, 12.62);

    private const string Json = @"{
  ""version"": 0.6,
  ""elements"": [
    { ""type"": ""node"", ""id"": 1, ""lat"": 56.0450, ""lon"": 12.6300,
      ""tags"": { ""seamark:type"": ""buoy_lateral"", ""seamark:buoy_lateral:category"": ""port"",
                  ""seamark:buoy_lateral:colour"": ""red"", ""seamark:buoy_lateral:shape"": ""can"" } },
    { ""type"": ""node"", ""id"": 2, ""lat"": 56.0452, ""lon"": 12.6320,
      ""tags"": { ""seamark:type"": ""buoy_lateral"", ""seamark:buoy_lateral:colour"": ""green"", ""seamark:buoy_lateral:shape"": ""conical"" } },
    { ""type"": ""node"", ""id"": 3, ""lat"": 56.0500, ""lon"": 12.6400,
      ""tags"": { ""seamark:type"": ""buoy_cardinal"", ""seamark:buoy_cardinal:category"": ""north"",
                  ""seamark:name"": ""Kronborg Grønne N"", ""seamark:buoy_cardinal:colour"": ""black;yellow"" } },
    { ""type"": ""node"", ""id"": 4, ""lat"": 56.0300, ""lon"": 12.6000,
      ""tags"": { ""seamark:type"": ""beacon_special_purpose"", ""note"": ""has, commas {and} braces"" } },
    { ""type"": ""node"", ""id"": 5, ""lat"": 56.0, ""lon"": 12.6, ""tags"": { ""amenity"": ""bench"" } }
  ]
}";

    [Fact]
    public void Reads_buoys_and_beacons_from_overpass()
    {
        var marks = Seamarks.FromOverpassJson(Json, Proj);
        Assert.Equal(4, marks.Count);
        Assert.Equal(SeamarkKind.Port, marks[0].Kind);
        Assert.Equal("can", marks[0].Shape);
        Assert.Equal(SeamarkKind.Starboard, marks[1].Kind); // from colour when the category is missing
        Assert.Equal(SeamarkKind.CardinalNorth, marks[2].Kind);
        Assert.Equal("Kronborg Grønne N", marks[2].Name);
        Assert.True(marks[3].IsBeacon);
        Assert.Equal(SeamarkKind.Special, marks[3].Kind);
        var p = Proj.ToLocal(56.0450, 12.6300);
        Assert.True(Vec2.Distance(p, marks[0].Position) < 0.01);
        Assert.Contains("seamark:type", Seamarks.OverpassQuery(Regions.Helsingor.Bounds));
    }

    [Fact]
    public void Windward_mark_is_upwind_and_snaps_to_a_nearby_buoy()
    {
        // Wind from the north: windward mark 600 m north of the start.
        var plain = PracticeCourse.WindwardLeeward(Vec2.Zero, 0, 600, laps: 2);
        Assert.Equal(4, plain.Marks.Count);
        Assert.True(plain.Marks[0].Position.Y > 590 && Math.Abs(plain.Marks[0].Position.X) < 1);
        Assert.False(plain.Marks[0].IsReal);

        var buoy = new Seamark { Position = new Vec2(90, 650), Kind = SeamarkKind.Special, Name = "Race buoy" };
        var snapped = PracticeCourse.WindwardLeeward(Vec2.Zero, 0, 600, buoys: new List<Seamark> { buoy });
        Assert.True(snapped.Marks[0].IsReal);
        Assert.Equal("Race buoy", snapped.Marks[0].Name);
        Assert.False(snapped.Marks[1].IsReal); // nothing near the bottom
    }

    [Fact]
    public void Buoy_tour_visits_nearest_first_and_returns()
    {
        var buoys = new List<Seamark>
        {
            new Seamark { Position = new Vec2(1000, 0), Kind = SeamarkKind.Port },
            new Seamark { Position = new Vec2(300, 0), Kind = SeamarkKind.Starboard },
            new Seamark { Position = new Vec2(320, 20), Kind = SeamarkKind.Starboard }, // a pair: only one is used
            new Seamark { Position = new Vec2(9000, 0), Kind = SeamarkKind.Port },      // out of range
            new Seamark { Position = new Vec2(600, 400), IsBeacon = true },            // beacons are not marks
        };
        var c = PracticeCourse.BuoyTour(Vec2.Zero, buoys, count: 4);
        Assert.Equal(3, c.Marks.Count);
        Assert.Equal(300, c.Marks[0].Position.X);
        Assert.Equal(1000, c.Marks[1].Position.X);
        Assert.Same(c.Marks[0], c.Marks[2]);
        Assert.True(c.Marks[0].IsReal);
    }

    [Fact]
    public void Runner_times_each_mark_and_finishes()
    {
        var c = new PracticeCourse("test");
        c.Marks.Add(new CourseMark(new Vec2(0, 100), "A", false));
        c.Marks.Add(new CourseMark(new Vec2(100, 100), "B", false));
        var r = new CourseRunner(c, startTime: 10);
        Assert.Equal(CourseEvent.None, r.Update(new Vec2(0, 50), 20));
        Assert.Equal(0, r.BearingToNext(new Vec2(0, 50)), 6);
        Assert.Equal(CourseEvent.MarkRounded, r.Update(new Vec2(5, 90), 30));
        Assert.Equal("B", r.Next.Name);
        Assert.Equal(CourseEvent.Finished, r.Update(new Vec2(95, 105), 55));
        Assert.True(r.Finished);
        Assert.Equal(new[] { 20.0, 45.0 }, r.Splits);
        Assert.Equal(45, r.Elapsed(1000));
    }

    [Fact]
    public void Autopilot_sails_a_reaching_course_round_the_marks()
    {
        // Wind from the north; marks to the east and back west are all reaches, so steering straight at them works.
        var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, 5));
        sim.State.Heading = 90 * Angles.Deg;
        sim.State.Surge = 2;
        var c = new PracticeCourse("reach");
        c.Marks.Add(new CourseMark(new Vec2(300, -40), "East", false));
        c.Marks.Add(new CourseMark(new Vec2(0, -80), "West", false));
        var runner = new CourseRunner(c, 0) { ZoneRadius = 25 };
        var pilot = new Autopilot(sim.State.Heading);
        for (int i = 0; i < 120 * 600 && !runner.Finished; i++)
        {
            if (runner.Next != null) pilot.TargetHeading = runner.BearingToNext(sim.State.Position);
            pilot.Apply(sim, BoatSimulator.FixedDt);
            sim.Step(BoatSimulator.FixedDt);
            runner.Update(sim.State.Position, sim.State.Time);
        }
        Assert.True(runner.Finished, $"stuck at mark {runner.NextIndex}, {runner.DistanceToNext(sim.State.Position):F0} m away");
        Assert.InRange(runner.FinishTime.Value, 60, 400);
    }
}
