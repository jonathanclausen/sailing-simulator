namespace TillerTide.Physics
{
    /// <summary>
    /// Physical description of a boat. All lengths in metres, boat frame (X forward of the centre of gravity,
    /// heights above the waterline / roll axis). Defaults describe a generic 4.2 m single-handed dinghy.
    /// </summary>
    public sealed class BoatConfig
    {
        public string Name { get; set; } = "Generic 4.2 m dinghy";

        // Mass and inertia
        public double HullMass { get; set; } = 80;            // hull + rig, kg
        public double CrewMass { get; set; } = 80;            // kg
        public double Displacement => HullMass + CrewMass;
        public double SurgeAddedMassFactor { get; set; } = 0.05;
        public double SwayAddedMassFactor { get; set; } = 0.6;
        public double YawInertia { get; set; } = 260;         // kg·m², incl. added mass
        public double RollInertia { get; set; } = 110;        // kg·m², incl. crew and added mass

        // Hull
        public double WaterlineLength { get; set; } = 3.9;
        public double WettedArea { get; set; } = 3.2;         // m²
        public double HullLateralArea { get; set; } = 0.35;   // m², canoe body side area
        /// <summary>Residuary resistance as a fraction of weight, against Froude number. Includes the planing hump.</summary>
        public Table ResiduaryResistance { get; set; } = new Table(
            (0.0, 0.0), (0.2, 0.0015), (0.3, 0.006), (0.35, 0.012), (0.4, 0.022), (0.45, 0.04),
            (0.5, 0.058), (0.55, 0.07), (0.6, 0.076), (0.7, 0.074), (0.9, 0.07), (1.2, 0.076), (1.5, 0.09));
        /// <summary>Hull righting lever GZ (m) against heel angle (deg), crew on the centreline.</summary>
        public Table RightingLever { get; set; } = new Table(
            (0, 0.0), (10, 0.05), (20, 0.09), (30, 0.11), (45, 0.10), (60, 0.06), (75, 0.0), (90, -0.06));
        public double CapsizeHeelDeg { get; set; } = 70;
        public double Draft { get; set; } = 0.9;              // board down

        // Rig (single triangular mainsail)
        public SailPolar Sail { get; set; } = SailPolar.Mainsail();
        public double LuffLength { get; set; } = 5.6;
        public double FootLength { get; set; } = 2.6;
        public double SailArea => 0.5 * LuffLength * FootLength;
        public double BoomHeight { get; set; } = 0.9;         // above waterline
        public double MastX { get; set; } = 0.9;              // mast position forward of CG
        public double MinBoomAngleDeg { get; set; } = 3;      // sheet fully in
        public double MaxBoomAngleDeg { get; set; } = 85;     // sheet fully out (shrouds)
        public int SailStrips { get; set; } = 6;
        /// <summary>How far by the lee (deg past dead downwind) before the boom gybes on its own.</summary>
        public double ByTheLeeDeg { get; set; } = 12;
        public double MaxBoomRateDeg { get; set; } = 120;     // boom swing speed in a gybe, deg/s
        /// <summary>A strip whose wind is on the wrong face by less than this (deg) flaps instead of backwinding.</summary>
        public double LuffBandDeg { get; set; } = 30;
        /// <summary>Extra twist at the head (deg) with the sheet fully in / fully out.</summary>
        public double TwistInDeg { get; set; } = 4;
        public double TwistOutDeg { get; set; } = 18;
        /// <summary>Each reef shortens the mainsail's luff and foot by these fractions.</summary>
        public double ReefLuffFactor { get; set; } = 0.17;
        public double ReefFootFactor { get; set; } = 0.07;
        public int MaxReefs { get; set; } = 0;

        // Headsail (jib or genoa) on the forestay. Zero size = no headsail.
        public SailPolar JibSail { get; set; } = SailPolar.Mainsail();
        public double JibLuffLength { get; set; } = 0;
        public double JibFootLength { get; set; } = 0;
        public double JibArea => 0.5 * JibLuffLength * JibFootLength;
        public bool HasJib => JibArea > 0.01;
        public double JibTackX { get; set; } = 0;           // forestay tack, forward of CG
        public double JibTackHeight { get; set; } = 0;      // above the waterline
        public double MinJibAngleDeg { get; set; } = 10;    // sheet fully in (the sheeting angle)
        public double MaxJibAngleDeg { get; set; } = 70;    // sheet fully out
        public double JibTwistInDeg { get; set; } = 5;
        public double JibTwistOutDeg { get; set; } = 20;
        public int JibStrips { get; set; } = 6;

        // Foils
        public double BoardArea { get; set; } = 0.28;
        public double BoardX { get; set; } = 0.1;
        public double BoardDepth { get; set; } = 0.55;        // centre of effort below roll axis
        public FoilPolar BoardPolar { get; set; } = new FoilPolar(3.5);
        public double RudderArea { get; set; } = 0.12;
        public double RudderX { get; set; } = -2.0;
        public double RudderDepth { get; set; } = 0.45;
        public FoilPolar RudderPolar { get; set; } = new FoilPolar(3.0);
        public double MaxRudderDeg { get; set; } = 30;

        // Crew
        public double CrewMaxArm { get; set; } = 1.0;         // crew centre of mass off the centreline when fully hiked
        /// <summary>How far to leeward the crew can sit in light air, as a fraction of CrewMaxArm.</summary>
        public double CrewLeewardLimit { get; set; } = 0.4;
        public double TargetHeelDeg { get; set; } = 6;        // auto-hike aims for this

        // Damping the foils and sails don't capture (hull, waves, wind on the rig): yaw and roll.
        public double YawDampingLinear { get; set; } = 40;
        public double YawDampingQuadratic { get; set; } = 220;
        public double RollDampingLinear { get; set; } = 120;
        public double RollDampingQuadratic { get; set; } = 250;

        /// <summary>A generic modern 40 ft (12.2 m) cruising sloop: fin keel with ballast, spade rudder, wheel,
        /// fractional rig with a 105% genoa. Numbers are typical of current production cruisers.</summary>
        public static BoatConfig Cruiser40() => new BoatConfig
        {
            Name = "Cruiser 40 (12.2 m sloop)",
            HullMass = 8300, CrewMass = 300,
            SurgeAddedMassFactor = 0.06, SwayAddedMassFactor = 0.5,
            YawInertia = 120000, RollInertia = 48000,
            WaterlineLength = 11.2, WettedArea = 31, HullLateralArea = 3.5,
            // Displacement hull: resistance climbs steeply towards hull speed (Froude ~0.4, about 8 knots) and it
            // doesn't plane.
            ResiduaryResistance = new Table(
                (0.0, 0.0), (0.15, 0.0003), (0.2, 0.0009), (0.25, 0.0022), (0.3, 0.0048), (0.35, 0.0105),
                (0.4, 0.023), (0.45, 0.042), (0.5, 0.062), (0.55, 0.078), (0.6, 0.088), (0.7, 0.1)),
            // Ballast keel: stiff, and the righting lever keeps growing past 60° (it comes back up from a knockdown).
            RightingLever = new Table(
                (0, 0.0), (10, 0.19), (20, 0.40), (30, 0.58), (45, 0.76), (60, 0.82), (75, 0.72), (90, 0.52)),
            CapsizeHeelDeg = 1000,
            Draft = 2.0,
            LuffLength = 14.0, FootLength = 5.0, BoomHeight = 3.0, MastX = 0.9,
            MinBoomAngleDeg = 4, MaxBoomAngleDeg = 80, SailStrips = 8, ByTheLeeDeg = 10, MaxBoomRateDeg = 70,
            LuffBandDeg = 25, TwistInDeg = 3, TwistOutDeg = 14, MaxReefs = 2,
            JibLuffLength = 13.6, JibFootLength = 5.4, JibTackX = 5.4, JibTackHeight = 1.4,
            MinJibAngleDeg = 12, MaxJibAngleDeg = 70, JibStrips = 6,
            BoardArea = 2.3, BoardX = 2.4, BoardDepth = 1.25, BoardPolar = new FoilPolar(2.2, 14, 0.010),
            RudderArea = 0.65, RudderX = -4.7, RudderDepth = 0.85, RudderPolar = new FoilPolar(3.0), MaxRudderDeg = 35,
            CrewMaxArm = 1.7, CrewLeewardLimit = 0.5, TargetHeelDeg = 14,
            YawDampingLinear = 18000, YawDampingQuadratic = 100000,
            RollDampingLinear = 25000, RollDampingQuadratic = 60000,
        };
    }
}
