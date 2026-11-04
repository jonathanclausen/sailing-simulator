using System;

namespace TillerTide.Physics.Region
{
    /// <summary>
    /// Wind coming off the land is slower and shiftier: trees, houses and the rise of the shore leave a wind shadow
    /// that recovers over several hundred metres of open water. Off Helsingør this is very real in a westerly.
    /// Wraps another wind field and looks upwind for land using the coastline's distance field.
    /// </summary>
    public sealed class LandShadowWind : IWindField
    {
        public IWindField Inner { get; }
        public RegionGrid Grid { get; }
        /// <summary>Fraction of the wind lost right at the beach.</summary>
        public double Strength { get; set; } = 0.5;
        /// <summary>Distance over which the wind recovers (m).</summary>
        public double Recovery { get; set; } = 350;
        public double MaxLookUpwind { get; set; } = 1500;

        public LandShadowWind(IWindField inner, RegionGrid grid) { Inner = inner; Grid = grid; }

        /// <summary>Distance upwind to the nearest land along the wind, or null if there is none within range.</summary>
        public double? FetchToLand(Vec2 p, double windFromBearing)
        {
            var dir = Vec2.FromBearing(windFromBearing);
            double travelled = 0;
            for (int i = 0; i < 64 && travelled <= MaxLookUpwind; i++)
            {
                double sd = Grid.SignedDistanceAt(p + dir * travelled);
                if (sd >= 0) return travelled;
                travelled += Math.Max(Grid.CellSize, -sd); // the shore is at least |sd| away in every direction
            }
            return null;
        }

        public Vec2 WindAt(Vec2 position, double heightMetres, double time)
        {
            var w = Inner.WindAt(position, heightMetres, time);
            if (w.LengthSquared < 1e-6) return w;
            double from = (-w).Bearing;
            double? fetch = FetchToLand(position, from);
            if (fetch == null) return w;
            double loss = Strength * Math.Exp(-fetch.Value / Recovery);
            // Higher up the shadow is weaker.
            loss *= 1 / (1 + heightMetres / 20);
            return w * (1 - loss);
        }
    }
}
