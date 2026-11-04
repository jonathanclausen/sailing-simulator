using System;

namespace TillerTide.Physics
{
    /// <summary>
    /// Sailing dinghy dynamics: surge, sway, yaw and heel (4 degrees of freedom), stepped at a fixed rate.
    ///
    /// Forces:
    /// - Sail: split into horizontal strips. Each strip sees its own apparent wind (gradient, heel and roll
    ///   motion included), sits at its own angle (boom + twist) and produces lift and drag from a sail polar.
    /// - Centreboard and rudder: finite-aspect-ratio foils with stall. Leeway, weather helm and the need for
    ///   flow over the rudder come out of these.
    /// - Hull: ITTC-57 friction plus residuary (wave) resistance with a planing hump; lateral and yaw damping.
    /// - Heel: sail and foil moments against hull form stability and crew hiking.
    ///
    /// Boat frame: X forward, Y starboard, heights positive up. World: X east, Y north.
    /// Pitch and heave are left to the renderer (wave following) for now.
    /// </summary>
    public sealed class BoatSimulator
    {
        public const double FixedDt = 1.0 / 120.0;
        public const double Gravity = 9.81;
        public const double AirDensity = 1.225;
        public const double WaterDensity = 1025;
        public const double WaterViscosity = 1.19e-6;

        public BoatConfig Config { get; }
        public IWindField Wind { get; set; }
        public ICurrentField Current { get; set; }
        public IDepthField Depth { get; set; }

        public BoatState State;
        public BoatControls Controls = BoatControls.Default;
        public BoatTelemetry Telemetry;

        private double _accumulator;

        public BoatSimulator(BoatConfig config, IWindField wind, ICurrentField current = null, IDepthField depth = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Wind = wind ?? throw new ArgumentNullException(nameof(wind));
            Current = current ?? UniformCurrent.None;
            Depth = depth;
        }

        /// <summary>Advance by a variable frame time using fixed sub-steps. Returns the number of steps taken.</summary>
        public int Advance(double frameDt)
        {
            _accumulator += Math.Min(frameDt, 0.25);
            int steps = 0;
            while (_accumulator >= FixedDt)
            {
                Step(FixedDt);
                _accumulator -= FixedDt;
                steps++;
            }
            return steps;
        }

        /// <summary>Put a capsized boat back upright and stopped.</summary>
        public void Recover()
        {
            State.Capsized = false;
            State.Heel = 0; State.HeelRate = 0;
            State.Surge = 0; State.Sway = 0; State.YawRate = 0;
        }

        public void Step(double dt)
        {
            var c = Config;
            var s = State;
            double phi = s.Heel, cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);
            Vec2 fwd = Vec2.FromBearing(s.Heading);
            Vec2 stb = Vec2.FromBearing(s.Heading + Math.PI / 2);
            Vec2 current = Current.CurrentAt(s.Position, s.Time);
            Vec2 vWater = fwd * s.Surge + stb * s.Sway;
            Vec2 vGround = vWater + current;

            double X = 0, Y = 0, N = 0, K = 0; // surge force, sway force, yaw moment, heel moment

            // ---------- Sail ----------
            double sheet = Angles.Clamp(Controls.Sheet, 0, 1);
            double sheetMax = (c.MinBoomAngleDeg + (c.MaxBoomAngleDeg - c.MinBoomAngleDeg) * sheet) * Angles.Deg;
            double hRef = c.BoomHeight + 0.35 * c.LuffLength;
            ApparentAt(hRef, fwd, stb, vGround, s, cosPhi, false, out double afRef, out double arRef);
            double awaRef = Math.Atan2(-arRef, -afRef);
            int windSide = awaRef >= 0 ? 1 : -1;        // +1 = wind from starboard

            // Boom: blown out to leeward until the sheet stops it. It only gybes once the wind is clearly
            // on the other side (a little sailing by the lee is allowed), and it swings with a finite rate.
            int boomSide = s.Boom > 0 ? 1 : (s.Boom < 0 ? -1 : -windSide);
            bool byTheLee = boomSide == windSide;
            if (byTheLee && Math.Abs(awaRef) < Math.PI - c.ByTheLeeDeg * Angles.Deg) boomSide = -windSide;
            byTheLee = boomSide == windSide;
            double boomTarget = boomSide * (byTheLee ? sheetMax : Math.Min(sheetMax, Math.Abs(awaRef)));
            double boomRate = Angles.Clamp((boomTarget - s.Boom) * 8, -c.MaxBoomRateDeg * Angles.Deg, c.MaxBoomRateDeg * Angles.Deg);
            double boomNew = s.Boom + boomRate * dt;
            double boom = Math.Abs(boomNew);
            boomSide = boomNew >= 0 ? 1 : -1;
            double twistHead = (c.TwistInDeg + (c.TwistOutDeg - c.TwistInDeg) * sheet) * Angles.Deg;

            double sailX = 0, sailYh = 0, sailK = 0, luffArea = 0, midAlpha = 0, awsMid = 0, awaMid = 0;
            int n = Math.Max(1, c.SailStrips);
            for (int k = 0; k < n; k++)
            {
                double f = (k + 0.5) / n;
                double hk = c.BoomHeight + f * c.LuffLength;
                double chord = c.FootLength * (1 - f);
                double area = chord * c.LuffLength / n;
                ApparentAt(hk, fwd, stb, vGround, s, cosPhi, true, out double af, out double ar);
                double aws = Math.Sqrt(af * af + ar * ar);
                if (aws < 1e-6) continue;
                double awa = Math.Atan2(-ar, -af);
                double sailAngle = Math.Min(boom + twistHead * f, c.MaxBoomAngleDeg * Angles.Deg + twistHead);
                // Chord from mast to leech, and the direction the air flows; the angle between them is the angle of attack.
                double dx = -Math.Cos(sailAngle), dy = boomSide * Math.Sin(sailAngle);
                double fx0 = af / aws, fy0 = ar / aws;
                double alpha = Math.Atan2(dx * fy0 - dy * fx0, dx * fx0 + dy * fy0);
                double a = Math.Abs(alpha);
                double cl, cd;
                if (alpha * boomSide > 0 && a < c.LuffBandDeg * Angles.Deg)
                {
                    // Wind on the wrong face but only just: the sail is flapping.
                    cl = 0; cd = c.Sail.LuffDrag; luffArea += area;
                }
                else { cl = c.Sail.Lift[a / Angles.Deg]; cd = c.Sail.Drag[a / Angles.Deg]; }
                if (a > Math.PI / 2) cl = 0; // flow from the leech: drag only
                double q = 0.5 * AirDensity * aws * aws * area;
                double sgn = alpha >= 0 ? 1 : -1;
                double fx = q * (cl * sgn * -fy0 + cd * fx0);
                double fyh = q * (cl * sgn * fx0 + cd * fy0);  // perpendicular to the mast
                sailX += fx; sailYh += fyh; sailK += fyh * hk;
                double ceX = c.MastX - 0.4 * chord * Math.Cos(sailAngle);
                double ceY = boomSide * 0.4 * chord * Math.Sin(sailAngle) + hk * sinPhi;
                N += ceX * fyh * cosPhi - ceY * fx;
                if (k == n / 2) { midAlpha = alpha * -boomSide; awsMid = aws; awaMid = awa; }
            }
            if (s.Capsized) { sailX = 0; sailYh = 0; sailK = 0; N = 0; }
            X += sailX; Y += sailYh * cosPhi; K += sailK;

            // ---------- Foils ----------
            double rudderAngle = Angles.Clamp(Controls.Steer, -1, 1) * c.MaxRudderDeg * Angles.Deg;
            Foil(c.BoardArea, c.BoardX, c.BoardDepth, c.BoardPolar, 0, s, cosPhi, ref X, ref Y, ref N, ref K, out _);
            Foil(c.RudderArea, c.RudderX, c.RudderDepth, c.RudderPolar, rudderAngle, s, cosPhi, ref X, ref Y, ref N, ref K, out double rudderStall);

            // ---------- Hull ----------
            double u = s.Surge, v = s.Sway, r = s.YawRate;
            double re = Math.Max(1e5, Math.Abs(u) * c.WaterlineLength / WaterViscosity);
            double cf = 0.075 / Math.Pow(Math.Log10(re) - 2, 2);
            double rf = 0.5 * WaterDensity * c.WettedArea * cf * u * Math.Abs(u);
            double fn = Math.Abs(u) / Math.Sqrt(Gravity * c.WaterlineLength);
            double rr = Math.Sign(u) * c.Displacement * Gravity * c.ResiduaryResistance[fn] * (1 + 0.5 * sinPhi * sinPhi);
            double hullRes = rf + rr;
            X -= hullRes;
            Y -= 0.5 * WaterDensity * c.HullLateralArea * 1.0 * v * Math.Abs(v);
            N -= 40 * r + 220 * r * Math.Abs(r);

            // ---------- Heel ----------
            double phiDeg = Math.Abs(phi) / Angles.Deg;
            K -= c.Displacement * Gravity * Math.Sign(phi) * c.RightingLever[phiDeg];
            // Crew position across the boat (starboard +), as a fraction of the full hiking arm.
            double crew;
            if (Controls.AutoHike)
            {
                // Sail with a little leeward heel upwind and reaching, flat on a run.
                double target = c.TargetHeelDeg * Angles.Deg * Angles.Clamp((150 * Angles.Deg - Math.Abs(awaRef)) / (30 * Angles.Deg), 0, 1);
                target *= Angles.Clamp(Math.Sqrt(afRef * afRef + arRef * arRef) / 3.0, 0, 1);   // no heel in a calm
                double wantedHeel = -windSide * target;        // leeward is opposite the wind side
                double err = (phi - wantedHeel + 0.6 * s.HeelRate) / (10 * Angles.Deg);
                crew = Angles.Clamp(s.CrewOffset - dt * 2.5 * err, -1, 1);
            }
            else crew = windSide * Controls.Hike;
            // The crew can hike fully to windward but only lean a little to leeward.
            // On a dead run there is no real windward side, so the crew may sit on either side.
            double leewardLimit = c.CrewLeewardLimit + (1 - c.CrewLeewardLimit) * Angles.Clamp((Math.Abs(awaRef) - 150 * Angles.Deg) / (20 * Angles.Deg), 0, 1);
            if (windSide * crew < -leewardLimit) crew = -windSide * leewardLimit;
            if (!s.Capsized)
            {
                // Crew weight shifted off the centreline (hull GZ already includes the crew sitting centrally).
                K += c.CrewMass * Gravity * crew * c.CrewMaxArm * cosPhi;
            }
            K -= 120 * s.HeelRate + 250 * s.HeelRate * Math.Abs(s.HeelRate);

            // ---------- Integrate (semi-implicit Euler) ----------
            double mx = c.Displacement * (1 + c.SurgeAddedMassFactor);
            double my = c.Displacement * (1 + c.SwayAddedMassFactor);
            s.Surge += (X + my * v * r) / mx * dt;
            s.Sway += (Y - mx * u * r) / my * dt;
            s.YawRate += N / c.YawInertia * dt;
            s.HeelRate += K / c.RollInertia * dt;
            s.Heel = Angles.Clamp(s.Heel + s.HeelRate * dt, -Math.PI / 2, Math.PI / 2);
            if (Math.Abs(s.Heel) >= Math.PI / 2) s.HeelRate = 0;
            s.Heading = Angles.Wrap2Pi(s.Heading + s.YawRate * dt);
            s.CrewOffset = crew;
            s.Boom = boomNew;
            s.Hike = windSide * crew;
            if (Math.Abs(s.Heel) > c.CapsizeHeelDeg * Angles.Deg) s.Capsized = true;

            Vec2 fwdN = Vec2.FromBearing(s.Heading), stbN = Vec2.FromBearing(s.Heading + Math.PI / 2);
            Vec2 vGroundN = fwdN * s.Surge + stbN * s.Sway + current;
            Vec2 next = s.Position + vGroundN * dt;
            if (Depth != null && Depth.DepthAt(next) < c.Draft)
            {
                s.Aground = true;
                s.Surge = 0; s.Sway = 0; s.YawRate *= 0.5;
            }
            else
            {
                s.Aground = false;
                s.Position = next;
            }
            s.Time += dt;
            State = s;

            // ---------- Telemetry ----------
            Vec2 tw = Wind.WindAt(s.Position, 10, s.Time);
            double twFrom = (-tw).Bearing;
            var t = new BoatTelemetry
            {
                TrueWindSpeed = tw.Length,
                TrueWindDirection = twFrom,
                TrueWindAngle = Angles.WrapPi(twFrom - s.Heading),
                ApparentWindSpeed = awsMid,
                ApparentWindAngle = awaMid,
                SpeedThroughWater = Math.Sqrt(s.Surge * s.Surge + s.Sway * s.Sway),
                SpeedOverGround = vGroundN.Length,
                CourseOverGround = vGroundN.Bearing,
                Leeway = Math.Abs(s.Surge) > 0.05 ? Math.Atan2(s.Sway, s.Surge) : 0,
                VelocityMadeGood = tw.Length > 1e-6 ? Vec2.Dot(fwdN * s.Surge + stbN * s.Sway, -tw.Normalized) : 0,
                BoomAngle = boom,
                BoomSide = boomSide,
                SailAngleOfAttack = midAlpha,
                LuffingFraction = luffArea / c.SailArea,
                DriveForce = sailX,
                SideForce = sailYh * cosPhi,
                HeelingMoment = sailK,
                RudderAngle = rudderAngle,
                RudderStalled = rudderStall,
                HullResistance = hullRes,
            };
            if (t.LuffingFraction > 0.5) t.SailFlow = SailFlow.Luffing;
            else if (midAlpha > SailPolar.StallAngleDeg * Angles.Deg)
                t.SailFlow = boom >= sheetMax - 1e-9 && sheet > 0.95 || Math.Abs(awaRef) > 110 * Angles.Deg && boom >= sheetMax - 1e-9 && boom > 75 * Angles.Deg
                    ? SailFlow.Running : SailFlow.Stalled;
            else t.SailFlow = SailFlow.Attached;
            Telemetry = t;
        }

        /// <summary>Apparent wind at a height on the mast, in the heeled sail plane (forward, across-mast).</summary>
        private void ApparentAt(double heightOnMast, Vec2 fwd, Vec2 stb, Vec2 vGround, in BoatState s, double cosPhi, bool includeRoll,
                                out double af, out double ar)
        {
            Vec2 air = Wind.WindAt(s.Position, Math.Max(0.3, heightOnMast * cosPhi), s.Time) - vGround;
            af = Vec2.Dot(air, fwd);
            double across = Vec2.Dot(air, stb) * cosPhi;
            ar = includeRoll ? across - s.HeelRate * heightOnMast : across;   // rolling the rig moves it through the air
        }

        private static void Foil(double area, double x, double depth, FoilPolar polar, double deflection, in BoatState s,
                                 double cosPhi, ref double X, ref double Y, ref double N, ref double K, out double stalled)
        {
            stalled = 0;
            double u = s.Surge;
            double vl = (s.Sway + s.YawRate * x) * cosPhi - s.HeelRate * depth;
            double v2 = u * u + vl * vl;
            if (v2 < 1e-8) return;
            double speed = Math.Sqrt(v2);
            double alpha = Math.Atan2(vl, Math.Abs(u)) + (u >= 0 ? deflection : -deflection);
            double cl = polar.Lift(alpha), cd = polar.Drag(alpha);
            stalled = Math.Abs(alpha) > polar.StallAngle ? 1 : 0;
            double q = 0.5 * WaterDensity * v2 * area;
            double px = u >= 0 ? vl / speed : -vl / speed;
            double py = u >= 0 ? -u / speed : u / speed;
            double fx = q * (cl * px - cd * u / speed);
            double fy = q * (cl * py - cd * vl / speed);   // perpendicular to the foil plane
            X += fx;
            Y += fy * cosPhi;
            N += x * fy * cosPhi;
            K += fy * -depth;
        }
    }
}
