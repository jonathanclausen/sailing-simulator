using UnityEngine;

namespace TillerTide.App
{
    /// <summary>Shared colours and materials. Shaders live in Resources so they are always in the build.</summary>
    public static class Look
    {
        public static readonly Color SkyHorizon = new Color(0.80f, 0.86f, 0.89f);
        public static readonly Color SkyZenith = new Color(0.33f, 0.56f, 0.78f);
        public static readonly Color Sand = new Color(0.85f, 0.79f, 0.62f);
        public static readonly Color Grass = new Color(0.42f, 0.55f, 0.32f);
        public static readonly Color Woods = new Color(0.27f, 0.39f, 0.23f);
        public static readonly Color HullOrange = new Color(1.0f, 0.42f, 0.24f);

        public static Material Lit(Color color, bool doubleSided = false)
        {
            var m = new Material(Shader.Find("TillerTide/SimpleLit")) { color = color };
            if (doubleSided)
            {
                m.SetFloat("_DoubleSided", 1);
                m.SetFloat("_Cull", 0);
            }
            return m;
        }

        public static Material Ocean() => new Material(Shader.Find("TillerTide/Ocean"));
        public static Material Sky() => new Material(Shader.Find("TillerTide/Sky"));
        public static Material Foam() => new Material(Shader.Find("TillerTide/Foam"));

        public static void PushSkyColours()
        {
            Shader.SetGlobalVector("_TT_SkyHorizon", SkyHorizon);
            Shader.SetGlobalVector("_TT_SkyZenith", SkyZenith);
        }

        /// <summary>Unity position for a world point (X east, Y north) at a height.</summary>
        public static Vector3 ToUnity(Physics.Vec2 p, float y = 0) => new Vector3((float)p.X, y, (float)p.Y);
    }
}
