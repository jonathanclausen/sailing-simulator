using System;
using System.Globalization;
using TillerTide.Physics;

// Prints a polar table: settled boat speed (knots) for each true wind speed and angle.
// Usage: dotnet run --project dotnet/TillerTide.Polar [--detail]
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
bool detail = args.Length > 0 && args[0] == "--detail";
if (args.Length == 3 && args[0] == "--trace")
{
    double w = double.Parse(args[1]), a = double.Parse(args[2]);
    var sim = new BoatSimulator(new BoatConfig(), new SteadyWind(0, Units.FromKnots(w)));
    sim.State.Heading = a * Angles.Deg; sim.State.Surge = 1; sim.Controls = BoatControls.Default; sim.Controls.Sheet = a / 180;
    var p = new Autopilot(a * Angles.Deg);
    for (int i = 0; i < 120 * 20; i++)
    {
        p.Apply(sim, BoatSimulator.FixedDt); sim.Step(BoatSimulator.FixedDt);
        if (i % 30 == 0) { var s = sim.State; var t = sim.Telemetry;
            Console.WriteLine($"t {s.Time:0.0} hdg {s.Heading/Angles.Deg:0} u {s.Surge:0.00} v {s.Sway:0.00} heel {s.Heel/Angles.Deg:0.0} awa {t.ApparentWindAngle/Angles.Deg:0} boom {t.BoomAngle/Angles.Deg*t.BoomSide:0} aoa {t.SailAngleOfAttack/Angles.Deg:0} side {t.SideForce:0} drive {t.DriveForce:0} K {t.HeelingMoment:0} hike {s.Hike:0.00} steer {sim.Controls.Steer:0.00} sheet {sim.Controls.Sheet:0.00}"); }
    }
    return;
}
var config = new BoatConfig();
double[] tws = { 6, 10, 14, 18, 22 };
double[] twa = { 30, 40, 45, 52, 60, 75, 90, 110, 135, 150, 165, 180 };

Console.WriteLine($"{config.Name}: boat speed in knots (sail area {config.SailArea:0.0} m², {config.Displacement:0} kg)");
Console.Write("TWA \\ TWS");
foreach (var w in tws) Console.Write($"{w,8:0} kn");
Console.WriteLine();
foreach (var a in twa)
{
    Console.Write($"{a,6:0}°   ");
    foreach (var w in tws)
    {
        var r = PolarSweep.Sail(config, w, a);
        Console.Write(r.Capsized ? "   capsz  " : $"{r.BoatSpeedKnots,8:0.00}  ");
        if (detail)
            Console.Error.WriteLine($"tws {w} twa {a}: {r.BoatSpeedKnots:0.00} kn heel {r.HeelDeg:0.0}° leeway {r.LeewayDeg:0.0}° sheet {r.Sheet:0.00} hike {r.Hike:0.00} vmg {r.VmgKnots:0.00}");
    }
    Console.WriteLine();
}
