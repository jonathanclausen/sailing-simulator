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
| `Packages/com.tillertide.physics/Runtime/Region/` | Real maps: OpenStreetMap coastline → land/depth grid, OpenSeaMap buoys, EMODnet depths (Helsingør first) |
| `Packages/com.tillertide.physics/Runtime/Racing/` | Practice courses round the marks, with timing |
| `Packages/com.tillertide.physics/Runtime/Traffic/` | The Helsingør–Helsingborg ferries on their timetable |
| `App/` | The Unity iPhone app: ocean, boat, sail, Helsingør coastline and Kronborg, live wind, touch controls |
| `dotnet/TillerTide.App.CompileCheck/` | Compiles the app's scripts against Unity's reference assemblies so CI catches C# errors |
| `docs/physics.md` | How the model works and the current polar |

## Run it

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download) (works on macOS, Windows and Linux).

```sh
dotnet test SailingSimulator.sln
dotnet run -c Release --project dotnet/TillerTide.Polar
dotnet run -c Release --project dotnet/TillerTide.Polar -- --trace 14 90   # step-by-step log: wind 14 kn, 90°
```

## Run the app on your Mac and iPhone

1. Install [Unity Hub](https://unity.com/download), then from Hub install **Unity 6 LTS (6000.0.x)** with the
   **iOS Build Support** module. A free Personal licence is fine.
2. Install **Xcode** from the Mac App Store and open it once to accept the licence.
3. Clone this repo. In Unity Hub choose **Add > Add project from disk** and pick the `App` folder. Open it with
   Unity 6 (accept if it says the project has no version yet).
4. The first open sets everything up by itself (render pipeline, scene, iOS settings). If anything looks pink or
   empty, run the menu **Tiller & Tide > Set up project**. If the console asks you to restart Unity, do so once.
5. Press **Play** to sail on the Mac. You sail a 40 ft cruising sloop. Controls: drag the bottom pad (or A/D,
   arrows) to steer, drag the right-hand bars to trim the mainsheet (W/S) and the jib sheet (Q/E), Reef (F) to
   reef the main, drag elsewhere to look around, scroll to zoom, R to reset.
   **Course** cycles between free sailing, a windward–leeward course (two laps, using real buoys when one is
   close to where the mark would go) and a tour of the real buoys nearest you. Sail within 20 m of each mark;
   your best time for each is saved. Tap the little map (top right) for the full chart. Watch out for the
   Helsingør–Helsingborg ferries: they sound five blasts if you're in their way, and they don't stop.
6. For the iPhone: **File > Build Profiles** (or Build Settings) > **iOS** > **Switch Platform**, then **Build**
   into a folder such as `App/Builds/iOS`. Open the generated `Unity-iPhone.xcodeproj` in Xcode, select the
   **Unity-iPhone** target > **Signing & Capabilities**, tick *Automatically manage signing* and choose your
   Apple ID as the team. Plug in the iPhone, pick it as the run destination and press **Run**. The first time,
   allow the developer on the phone under *Settings > General > VPN & Device Management*.

On first launch the app downloads the Helsingør coastline and buoys from OpenStreetMap / OpenSeaMap and the
water depths from EMODnet (about a minute, then saved on the phone), and the current wind at Helsingør from
Open-Meteo. Without a network it falls back to open sea and a
typical westerly.

## Next

Forecast wind fields across the Sound, real Øresund currents (DMI), a jib, more
boats, and more regions.
