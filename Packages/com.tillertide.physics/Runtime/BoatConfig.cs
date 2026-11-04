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
    }
}
