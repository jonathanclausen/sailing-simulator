using UnityEngine;

namespace TillerTide.App
{
    /// <summary>
    /// All the sound is synthesised, so there are no audio files: wind in your ears (rises and brightens with the
    /// apparent wind), water rushing past the hull, the sail flogging when it luffs, a chime at each mark, and the
    /// ferry's horn (five short blasts: "I don't understand your intentions", the danger signal).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundScape : MonoBehaviour
    {
        // Set from the main thread each frame, read on the audio thread.
        public volatile float ApparentWind;   // m/s
        public volatile float BoatSpeed;      // m/s
        public volatile float Luffing;        // 0..1
        public volatile float HornLevel;      // 0..1 by distance

        private volatile int _hornRequests, _chimeRequests;
        private int _sampleRate = 48000;
        private uint _rng = 2463534242u;
        private float _windLp, _windLp2, _waterLp, _waterHp, _flogLp, _flogPhase, _flogEnv;
        private float _wind, _water, _luff;
        private double _hornT = -1, _chimeT = -1;
        private int _hornBlasts;

        private void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.playOnAwake = true;
            src.loop = true;
            src.spatialBlend = 0;
            src.volume = 0.8f;
            src.Play();
        }

        public void Horn() => _hornRequests++;
        public void Chime() => _chimeRequests++;

        private float Noise()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x7FFFFF - 1f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float dt = 1f / _sampleRate;
            if (_hornRequests > 0) { _hornRequests = 0; _hornT = 0; _hornBlasts = 5; }
            if (_chimeRequests > 0) { _chimeRequests = 0; _chimeT = 0; }

            float targetWind = Mathf.Clamp01(ApparentWind / 14f);
            float targetWater = Mathf.Clamp01(BoatSpeed / 5f);
            float targetLuff = Mathf.Clamp01(Luffing) * Mathf.Clamp01(ApparentWind / 6f);
            float windCut = Mathf.Lerp(0.004f, 0.05f, targetWind);
            float hornLevel = HornLevel;

            for (int i = 0; i < data.Length; i += channels)
            {
                _wind += (targetWind - _wind) * 0.0002f;
                _water += (targetWater - _water) * 0.0002f;
                _luff += (targetLuff - _luff) * 0.0005f;

                // Wind: brown-ish noise through two low-pass stages, gusting slowly.
                float n = Noise();
                _windLp += (n - _windLp) * windCut;
                _windLp2 += (_windLp - _windLp2) * windCut;
                float wind = _windLp2 * 3.2f * _wind * _wind;

                // Water: band-passed hiss that grows with speed.
                float n2 = Noise();
                _waterLp += (n2 - _waterLp) * 0.12f;
                _waterHp += (_waterLp - _waterHp) * 0.01f;
                float water = (_waterLp - _waterHp) * 0.55f * _water * _water;

                // Flogging sail: noise bursts at 6-9 Hz, sharp attack, quick decay.
                _flogPhase += dt * (6f + 3f * _wind);
                if (_flogPhase >= 1f) { _flogPhase -= 1f; _flogEnv = 0.6f + 0.4f * Mathf.Abs(Noise()); }
                _flogEnv *= 0.9992f;
                _flogLp += (Noise() - _flogLp) * 0.3f;
                float flog = _flogLp * _flogEnv * _flogEnv * 0.5f * _luff;

                float s = wind + water + flog;

                if (_chimeT >= 0)
                {
                    float ct = (float)_chimeT;
                    s += 0.18f * Mathf.Exp(-ct * 3f) * (Mathf.Sin(2 * Mathf.PI * 880f * ct) + 0.5f * Mathf.Sin(2 * Mathf.PI * 1320f * ct));
                    _chimeT += dt;
                    if (_chimeT > 2) _chimeT = -1;
                }

                if (_hornT >= 0)
                {
                    // Five short blasts: 1 s on, 1 s off.
                    float ht = (float)(_hornT % 2.0);
                    if (ht < 1f)
                    {
                        float env = Mathf.Clamp01(ht * 20f) * Mathf.Clamp01((1f - ht) * 20f);
                        float ph = (float)_hornT;
                        float tone = Mathf.Sin(2 * Mathf.PI * 98f * ph) + 0.6f * Mathf.Sin(2 * Mathf.PI * 196f * ph) + 0.35f * Mathf.Sin(2 * Mathf.PI * 294f * ph);
                        s += 0.16f * hornLevel * env * tone;
                    }
                    _hornT += dt;
                    if (_hornT > _hornBlasts * 2.0) _hornT = -1;
                }

                s = s / (1f + Mathf.Abs(s)); // soft clip
                for (int c = 0; c < channels; c++) data[i + c] = s;
            }
        }
    }
}
