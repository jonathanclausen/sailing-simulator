namespace TillerTide.Physics
{
    /// <summary>What the sailor is doing with the controls.</summary>
    public struct BoatControls
    {
        /// <summary>-1 (full port) .. +1 (full starboard). Positive turns the bow to starboard.</summary>
        public double Steer;
        /// <summary>0 = sheeted hard in .. 1 = fully eased.</summary>
        public double Sheet;
        /// <summary>Crew position: negative = to leeward, 0 = centreline, 1 = fully hiked to windward. Ignored when AutoHike is on.</summary>
        public double Hike;
        public bool AutoHike;
        /// <summary>Headsail sheet: 0 = in hard .. 1 = fully eased.</summary>
        public double JibSheet;
        /// <summary>Number of reefs in the mainsail (0 = full sail).</summary>
        public int Reef;

        public static BoatControls Default => new BoatControls { Steer = 0, Sheet = 0.2, Hike = 0, AutoHike = true, JibSheet = 0.2 };
    }

    public enum SailFlow { Luffing, Attached, Stalled, Running }

    /// <summary>Integrated state of the boat. Angles in radians.</summary>
    public struct BoatState
    {
        public Vec2 Position;     // world, metres (X east, Y north)
        public double Heading;    // compass bearing of the bow
        public double Surge;      // forward speed through the water, m/s
        public double Sway;       // sideways speed through the water (starboard +), m/s
        public double YawRate;    // rad/s, positive = turning to starboard
        public double Heel;       // rad, positive = starboard rail down
        public double HeelRate;   // rad/s
        public double Hike;       // current crew position, -0.4 (leeward) .. 1 (fully hiked to windward)
        public double CrewOffset; // crew position across the boat, -1 (full port) .. +1 (full starboard)
        public double Boom;       // boom angle, rad, + = out to starboard
        public double Jib;        // headsail clew angle, rad, + = out to starboard
        public bool Capsized;
        public bool Aground;
        public double Time;       // seconds
    }

    /// <summary>Derived values for instruments, audio and visuals. Updated every step.</summary>
    public struct BoatTelemetry
    {
        public double TrueWindSpeed;      // m/s at 10 m
        public double TrueWindDirection;  // compass bearing wind comes FROM
        public double TrueWindAngle;      // signed, + = wind from starboard
        public double ApparentWindSpeed;  // m/s at mid-sail height
        public double ApparentWindAngle;  // signed, + = wind from starboard
        public double SpeedThroughWater;  // m/s
        public double SpeedOverGround;    // m/s
        public double CourseOverGround;   // compass bearing
        public double Leeway;             // rad, angle between heading and track through the water
        public double VelocityMadeGood;   // m/s toward the true wind (negative downwind)
        public double BoomAngle;          // rad, magnitude from the centreline
        public int BoomSide;              // +1 starboard, -1 port
        public double SailAngleOfAttack;  // rad, mid-height strip
        public SailFlow SailFlow;
        public double LuffingFraction;    // 0..1 share of sail area that is luffing
        public double DriveForce;         // N, sail force along the boat
        public double SideForce;          // N, sail force across the boat (horizontal)
        public double HeelingMoment;      // N·m from the sail
        public double RudderAngle;        // rad
        public double RudderStalled;      // 0..1
        public double HullResistance;     // N
        public double JibAngle;           // rad, signed (+ = starboard)
        public double JibAngleOfAttack;   // rad, mid-height strip
        public double JibLuffingFraction; // 0..1
        public SailFlow JibFlow;
    }
}
