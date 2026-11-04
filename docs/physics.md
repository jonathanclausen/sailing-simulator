# Physics model

The boat is simulated in four degrees of freedom (surge, sway, yaw, heel) at a fixed 120 Hz.
Pitch and heave will come from the ocean surface in the Unity app. Everything is deterministic: the same
inputs give the same result on every device, which keeps replays and races fair.

## Forces

- **Apparent wind.** Wind at each height on the mast (power-law gradient, 1/7) minus the boat's velocity over
  ground. Heel tilts the sail plane, and rolling moves the rig through the air (this damps rolling).
  Current moves the water, not the air.
- **Sail.** Six horizontal strips. Each has its own chord, angle (boom plus twist, more twist with the sheet
  eased), angle of attack and lift/drag from a soft-sail polar. A strip luffs when the wind is on the wrong
  face, stalls past about 25°, and only drags once flow comes from the leech (running).
- **Boom.** Blown out to leeward until the sheet stops it. It swings at a finite rate, allows about 12° by the
  lee before gybing on its own, and gybes across.
- **Centreboard and rudder.** Finite-aspect-ratio foils (lifting-line slope, stall at 13°, induced and
  separation drag). Leeway, weather helm and "no steering without flow" come out of these.
- **Hull.** ITTC-57 friction plus residuary resistance as a fraction of weight against Froude number, with
  the planing hump near Fn 0.6. Extra drag when heeled. Lateral and yaw damping.
- **Heel.** Sail and foil moments against hull form stability (GZ curve) and crew weight. Auto-hike moves the
  crew to hold a small leeward heel, flat on a run. Past 70° the boat capsizes.

## Polar (generic 4.2 m dinghy, 7.3 m², 160 kg with crew)

Boat speed in knots, settled with the autopilot steering and trimming. Regenerate with
`dotnet run -c Release --project dotnet/TillerTide.Polar`.

```
Generic 4.2 m dinghy: boat speed in knots (sail area 7.3 m², 160 kg)
TWA \ TWS       6 kn      10 kn      14 kn      18 kn      22 kn
    30°       2.92      4.46      4.80      4.79      4.65  
    40°       3.34      4.87      5.05      4.95      4.75  
    45°       3.53      5.00      5.19      5.00      4.77  
    52°       3.70      5.15      5.56      5.23      5.06  
    60°       3.82      5.28      6.05      5.70      5.51  
    75°       3.91      5.39      7.22      7.01      6.78  
    90°       3.84      5.33      7.51      9.75      9.49  
   110°       3.52      5.03      6.45     10.47     13.69  
   135°       2.81      4.36      5.48      6.96      9.90  
   150°       2.47      3.93      5.04      6.11      7.95  
   165°       2.21      3.62      4.74      5.67      6.93  
   180°       2.15      3.53      4.65      5.58      6.74  
```

The shape matches a single-handed dinghy: about 5 kn upwind in a breeze, planing at 10 kn or more on a reach
in 18 kn or more, and slower dead downwind. The numbers are not yet calibrated against a specific class; that
comes once we pick the first boat.

## Known limits

- No pitch or heave yet; waves don't affect speed.
- One sail (no jib or spinnaker) and no vang or cunningham, so depowering in strong wind is by easing only.
- The autopilot's sheet trimmer is simple and limits upwind speed in heavy air.

## The real place

- **Coastline** from OpenStreetMap becomes a signed-distance grid (25 m cells): positive on land, negative at sea.
- **Depth** comes from EMODnet's digital bathymetry (about 115 m cells; the app asks for ~230 m). Within 300 m of
  the beach the coastline's shelf (8 cm of depth per metre out) limits it, so beaches stay beaches even where the
  soundings are coarse, and real shoals still show. Without soundings the shelf is used everywhere, capped at 30 m.
- **Wind shadow**: wind blowing off the land loses up to half its speed at the beach and recovers over ~350 m of
  open water (less aloft). The distance to land upwind is found by sphere-tracing the distance field.
- **Ferries** shuttle between Helsingør and Helsingborg on a 50-minute cycle (5 min at each berth, 20 min crossing,
  peak ~14 kn), three on the route. Being inside a hull knocks you flat.
