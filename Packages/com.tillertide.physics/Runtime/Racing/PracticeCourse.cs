using System;
using System.Collections.Generic;
using TillerTide.Physics.Region;

namespace TillerTide.Physics.Racing
{
    public sealed class CourseMark
    {
        public Vec2 Position;
        public string Name;
        /// <summary>True when this is a real buoy from the chart, false for a laid practice mark.</summary>
        public bool IsReal;
        public CourseMark(Vec2 position, string name, bool isReal) { Position = position; Name = name; IsReal = isReal; }
    }

    /// <summary>A sequence of marks to sail round, in order.</summary>
    public sealed class PracticeCourse
    {
        public string Name { get; }
        public List<CourseMark> Marks { get; } = new List<CourseMark>();
        public PracticeCourse(string name) { Name = name; }

        /// <summary>How close to a real buoy a laid mark may be moved so you race round the real thing.</summary>
        public const double SnapToBuoy = 180;

        /// <summary>
        /// Classic windward–leeward: beat up to a mark straight upwind, run back down, and repeat. Uses a real buoy
        /// when one is close to where the windward or leeward mark would be laid.
        /// </summary>
        public static PracticeCourse WindwardLeeward(Vec2 start, double windFromBearing, double legLength = 600, int laps = 2,
            IReadOnlyList<Seamark> buoys = null, Func<Vec2, Vec2> toWater = null)
        {
            toWater = toWater ?? (p => p);
            var top = Pick(toWater(start + Vec2.FromBearing(windFromBearing) * legLength), "Windward mark", buoys);
            var bottom = Pick(toWater(start + Vec2.FromBearing(windFromBearing) * 60), "Leeward mark", buoys, maxSnap: 80);
            var c = new PracticeCourse($"Windward–leeward, {laps} laps");
            for (int i = 0; i < laps; i++) { c.Marks.Add(top); c.Marks.Add(bottom); }
            return c;
        }

        /// <summary>
        /// Round the real buoys near you: the closest few buoys in navigable water, visited nearest-first,
        /// finishing back at the first one.
        /// </summary>
        public static PracticeCourse BuoyTour(Vec2 start, IReadOnlyList<Seamark> buoys, int count = 4, double maxRange = 2500,
            IDepthField depth = null)
        {
            var c = new PracticeCourse("Buoy tour");
            if (buoys == null) return c;
            var pool = new List<Seamark>();
            foreach (var b in buoys)
                if (!b.IsBeacon && Vec2.Distance(b.Position, start) <= maxRange && (depth == null || depth.DepthAt(b.Position) >= 2))
                    pool.Add(b);
            var here = start;
            while (c.Marks.Count < count && pool.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < pool.Count; i++)
                    if (Vec2.Distance(pool[i].Position, here) < Vec2.Distance(pool[best].Position, here)) best = i;
                var b = pool[best];
                pool.RemoveAt(best);
                if (c.Marks.Count > 0 && Vec2.Distance(b.Position, here) < 120) continue; // skip buoys in a tight cluster
                c.Marks.Add(new CourseMark(b.Position, Label(b), true));
                here = b.Position;
            }
            if (c.Marks.Count >= 2) c.Marks.Add(c.Marks[0]);
            return c;
        }

        private static CourseMark Pick(Vec2 target, string fallbackName, IReadOnlyList<Seamark> buoys, double maxSnap = SnapToBuoy)
        {
            Seamark best = null;
            double bestD = maxSnap;
            if (buoys != null)
                foreach (var b in buoys)
                {
                    if (b.IsBeacon) continue;
                    double d = Vec2.Distance(b.Position, target);
                    if (d < bestD) { bestD = d; best = b; }
                }
            return best != null ? new CourseMark(best.Position, Label(best), true) : new CourseMark(target, fallbackName, false);
        }

        private static string Label(Seamark b)
        {
            if (!string.IsNullOrEmpty(b.Name)) return b.Name;
            switch (b.Kind)
            {
                case SeamarkKind.Port: return "Red buoy";
                case SeamarkKind.Starboard: return "Green buoy";
                case SeamarkKind.CardinalNorth: return "North cardinal";
                case SeamarkKind.CardinalEast: return "East cardinal";
                case SeamarkKind.CardinalSouth: return "South cardinal";
                case SeamarkKind.CardinalWest: return "West cardinal";
                case SeamarkKind.IsolatedDanger: return "Danger buoy";
                case SeamarkKind.SafeWater: return "Safe-water buoy";
                case SeamarkKind.Special: return "Yellow buoy";
                default: return "Buoy";
            }
        }
    }

    public enum CourseEvent { None, MarkRounded, Finished }

    /// <summary>Times a run round a course. A mark counts once you sail within <see cref="ZoneRadius"/> of it.</summary>
    public sealed class CourseRunner
    {
        public PracticeCourse Course { get; }
        public double ZoneRadius { get; set; } = 20;
        public int NextIndex { get; private set; }
        public double StartTime { get; }
        public double? FinishTime { get; private set; }
        public List<double> Splits { get; } = new List<double>();

        public CourseRunner(PracticeCourse course, double startTime)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            StartTime = startTime;
        }

        public bool Finished => FinishTime.HasValue;
        public CourseMark Next => Finished || NextIndex >= Course.Marks.Count ? null : Course.Marks[NextIndex];
        public double Elapsed(double now) => (FinishTime ?? now) - StartTime;

        public double DistanceToNext(Vec2 boat) => Next == null ? 0 : Vec2.Distance(boat, Next.Position);
        public double BearingToNext(Vec2 boat) => Next == null ? 0 : (Next.Position - boat).Bearing;

        public CourseEvent Update(Vec2 boat, double now)
        {
            var next = Next;
            if (next == null || Vec2.Distance(boat, next.Position) > ZoneRadius) return CourseEvent.None;
            Splits.Add(now - StartTime);
            NextIndex++;
            if (NextIndex >= Course.Marks.Count) { FinishTime = now; return CourseEvent.Finished; }
            return CourseEvent.MarkRounded;
        }
    }
}
