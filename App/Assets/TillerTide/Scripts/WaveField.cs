using UnityEngine;

namespace TillerTide.App
{
    /// <summary>
    /// Four Gerstner waves driven by the wind. The ocean shader draws exactly these waves and the boat samples
    /// the same heights on the CPU, so the hull rides the waves you see.
    /// Units: metres and seconds. Unity X = east, Z = north.
    /// </summary>
    public sealed class WaveField
    {
        public const int Count = 4;
        private readonly Vector4[] _waves = new Vector4[Count]; // xy = direction (to), z = wavelength, w = amplitude
        private float _steepness = 0.55f;
        private static readonly int WavesId = Shader.PropertyToID("_TT_Waves");
        private static readonly int ParamsId = Shader.PropertyToID("_TT_WaveParams");

        /// <summary>
        /// Set the sea state from the wind. Øresund is fetch-limited, so waves stay short and steep: in 10 m/s the
        /// dominant wavelength here is a few tens of metres and the height well under a metre.
        /// </summary>
        public void SetFromWind(float windFromDeg, float windSpeed)
        {
            float toRad = (windFromDeg + 180f) * Mathf.Deg2Rad;
            float u = Mathf.Max(0.5f, windSpeed);
            float mainLength = Mathf.Clamp(1.6f * u * u * 0.6f, 3f, 40f);
            float mainAmp = Mathf.Clamp(0.012f * u * u, 0.02f, 0.6f);
            float[] spread = { 0f, 28f, -34f, 61f };
            float[] lengthFactor = { 1f, 0.62f, 0.41f, 0.23f };
            float[] ampFactor = { 1f, 0.55f, 0.38f, 0.2f };
            for (int i = 0; i < Count; i++)
            {
                float a = toRad + spread[i] * Mathf.Deg2Rad;
                _waves[i] = new Vector4(Mathf.Sin(a), Mathf.Cos(a), mainLength * lengthFactor[i], mainAmp * ampFactor[i]);
            }
            _steepness = Mathf.Lerp(0.35f, 0.7f, Mathf.InverseLerp(2f, 12f, u));
        }

        public void PushToShaders(float time)
        {
            Shader.SetGlobalVectorArray(WavesId, _waves);
            Shader.SetGlobalVector(ParamsId, new Vector4(time, _steepness, 0, 0));
        }

        /// <summary>Surface height at a horizontal position (x east, z north).</summary>
        public float HeightAt(float x, float z, float t)
        {
            // Gerstner points move sideways too; one correction step finds the crest over the point.
            float px = x, pz = z;
            for (int iter = 0; iter < 2; iter++)
            {
                Displace(px, pz, t, out float dx, out _, out float dz);
                px = x - dx; pz = z - dz;
            }
            Displace(px, pz, t, out _, out float y, out _);
            return y;
        }

        public Vector3 NormalAt(float x, float z, float t)
        {
            const float e = 0.6f;
            float hL = HeightAt(x - e, z, t), hR = HeightAt(x + e, z, t);
            float hD = HeightAt(x, z - e, t), hU = HeightAt(x, z + e, t);
            return new Vector3(hL - hR, 2 * e, hD - hU).normalized;
        }

        private void Displace(float x, float z, float t, out float dx, out float dy, out float dz)
        {
            dx = dy = dz = 0;
            for (int i = 0; i < Count; i++)
            {
                var w = _waves[i];
                float k = 2 * Mathf.PI / w.z;
                float c = Mathf.Sqrt(9.81f / k);
                float f = k * (w.x * x + w.y * z - c * t);
                float q = _steepness / (k * w.w * Count);
                dx += q * w.w * w.x * Mathf.Cos(f);
                dz += q * w.w * w.y * Mathf.Cos(f);
                dy += w.w * Mathf.Sin(f);
            }
        }
    }
}
