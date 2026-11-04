# Tiller & Tide Physics

Engine-independent C# sailing physics (no `UnityEngine` reference), packaged so Unity can use it directly:
add it via *Package Manager → Add package from disk* and pick `package.json`, or keep it in the project's `Packages/` folder.

Create a `BoatSimulator`, set `Controls` each frame, call `Advance(Time.deltaTime)` and read `State` / `Telemetry`.
Map world X (east) to Unity X and world Y (north) to Unity Z; heading is a compass bearing in radians.
