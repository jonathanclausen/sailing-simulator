# Sailing simulator (Tiller & Tide)

A realistic sailing simulator for iPhone: real wind, current and sail physics, sailed in your own local waters.
The plan is in the [system design doc](https://claude.ai/artifact/Mv8e5u6r6hkNXyS8oNWK7V) and a playable browser
mockup is at [Tiller & Tide mockup](https://claude.ai/artifact/67pweJHN3DccwFut88t4xa).

## What's here

| Path | What it is |
| --- | --- |
| `Packages/com.tillertide.physics/` | The physics core as a Unity package (plain C#, no Unity dependency) |
| `dotnet/TillerTide.Physics/` | Builds the same sources as a .NET library so they can be tested without Unity |
| `dotnet/TillerTide.Physics.Tests/` | xUnit tests: in irons, polar shape, planing, leeway, rudder, current, capsize, grounding, determinism |
| `dotnet/TillerTide.Polar/` | Prints the boat's polar table (speed at each true wind speed and angle) |
| `docs/physics.md` | How the model works and the current polar |

## Run it

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download) (works on macOS, Windows and Linux).

```sh
dotnet test SailingSimulator.sln
dotnet run -c Release --project dotnet/TillerTide.Polar
dotnet run -c Release --project dotnet/TillerTide.Polar -- --trace 14 90   # step-by-step log: wind 14 kn, 90°
```

## Next

Phase 1 adds the Unity app (iPhone, URP) that renders the ocean and boat and drives this package.
Building for iPhone needs a Mac with Xcode.
