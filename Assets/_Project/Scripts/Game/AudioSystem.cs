using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackbox.Game
{
    public enum Sfx
    {
        Hover,
        Click,
        Dial,
        Run,
        Arrive,
        Match,
        Miss,
        Solved,
        Open,
        Close,
        Unlock,
        Name,
        Tick,
    }

    /// <summary>
    /// All sound is synthesised when the game starts: soft sine blips, filtered
    /// noise and a slow drone, so the game needs no audio files. Quiet and sparse,
    /// to match the Void look.
    /// </summary>
    public sealed class AudioSystem : MonoBehaviour
    {
        const int Rate = 44100;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly List<AudioSource> _voices = new List<AudioSource>();
        AudioSource _music;
        int _next;
        float _effects = 0.8f, _musicVolume = 0.5f, _master = 0.8f;
        float _musicFade = 1f;
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();

        public void Init()
        {
            for (int i = 0; i < 8; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices.Add(src);
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;

            _clips[Sfx.Hover] = Make("hover", 0.05f, (t, s) => Sine(2400f, t) * Env(t, 0.002f, 0.035f) * 0.10f);
            _clips[Sfx.Click] = Make("click", 0.12f, (t, s) => (Sine(Glide(760f, 1140f, t, 0.04f), t) * 0.8f + Sine(2280f, t) * 0.12f) * Env(t, 0.002f, 0.07f) * 0.22f);
            _clips[Sfx.Dial] = Make("dial", 0.09f, (t, s) => (s.Noise() * 0.5f * Env(t, 0.0005f, 0.006f) + Sine(1320f, t) * Env(t, 0.001f, 0.03f)) * 0.18f);
            _clips[Sfx.Run] = Make("run", 0.7f, (t, s) => (Sine(Glide(180f, 520f, t, 0.5f), t) * 0.6f + Sine(Glide(360f, 1040f, t, 0.5f), t) * 0.2f + s.Lowpass(s.Noise(), 0.05f) * 0.5f) * Env(t, 0.03f, 0.5f) * 0.16f);
            _clips[Sfx.Arrive] = Make("arrive", 0.6f, (t, s) => (Sine(880f, t) + Sine(1760f, t) * 0.25f) * Env(t, 0.004f, 0.4f) * 0.13f);
            _clips[Sfx.Match] = Make("match", 0.9f, (t, s) => (Sine(1046.5f, t) * 0.7f + Sine(1568f, t) * 0.3f * Env(t - 0.06f, 0.004f, 0.5f)) * Env(t, 0.004f, 0.6f) * 0.12f);
            _clips[Sfx.Miss] = Make("miss", 0.5f, (t, s) => (Sine(196f, t) * Env(t, 0.004f, 0.12f) + Sine(185f, t) * Env(t - 0.14f, 0.004f, 0.18f)) * 0.22f);
            _clips[Sfx.Solved] = Make("solved", 3.4f, Chord);
            _clips[Sfx.Open] = Make("open", 0.5f, (t, s) => s.Lowpass(s.Noise(), Mathf.Lerp(0.02f, 0.2f, t / 0.5f)) * Env(t, 0.12f, 0.25f) * 0.35f);
            _clips[Sfx.Close] = Make("close", 0.45f, (t, s) => s.Lowpass(s.Noise(), Mathf.Lerp(0.2f, 0.02f, t / 0.45f)) * Env(t, 0.02f, 0.3f) * 0.3f);
            _clips[Sfx.Unlock] = Make("unlock", 2.2f, (t, s) =>
            {
                float v = 0f;
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < notes.Length; i++) v += Sine(notes[i], t) * Env(t - i * 0.12f, 0.01f, 1.2f);
                return v * 0.09f;
            });
            _clips[Sfx.Name] = Make("name", 0.4f, (t, s) => (Sine(1318.5f, t) * 0.6f + Sine(1975.5f, t) * 0.3f) * Env(t, 0.003f, 0.25f) * 0.12f);
            _clips[Sfx.Tick] = Make("tick", 0.06f, (t, s) => Sine(1600f, t) * Env(t, 0.001f, 0.025f) * 0.08f);

            _music.clip = Drone();
        }

        public void SetVolumes(float master, float music, float effects)
        {
            _master = master;
            _musicVolume = music;
            _effects = effects;
            ApplyMusicVolume();
        }

        void ApplyMusicVolume()
        {
            if (_music != null) _music.volume = _master * _musicVolume * _musicFade * 0.55f;
        }

        public void StartMusic()
        {
            if (_music == null || _music.clip == null || _music.isPlaying) return;
            _musicFade = 0f;
            ApplyMusicVolume();
            _music.Play();
            Tweener.Run(this, "music", 4f, v => { _musicFade = v; ApplyMusicVolume(); }, Ease.InOutCubic);
        }

        /// <summary>Plays a sound. Repeats of the same sound within a few milliseconds are dropped.</summary>
        public void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (!_clips.TryGetValue(sfx, out var clip) || _voices.Count == 0) return;
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) && now - last < 0.03f) return;
            _lastPlayed[sfx] = now;
            var voice = _voices[_next];
            _next = (_next + 1) % _voices.Count;
            voice.pitch = pitch;
            voice.volume = _master * _effects * volume;
            voice.clip = clip;
            voice.Play();
        }

        // ------------------------------------------------------------------ synthesis

        sealed class SynthState
        {
            uint _seed = 22222;
            float _lp;

            public float Noise()
            {
                _seed ^= _seed << 13;
                _seed ^= _seed >> 17;
                _seed ^= _seed << 5;
                return (_seed / (float)uint.MaxValue) * 2f - 1f;
            }

            public float Lowpass(float x, float k)
            {
                _lp += (x - _lp) * Mathf.Clamp01(k);
                return _lp;
            }
        }

        static AudioClip Make(string name, float seconds, Func<float, SynthState, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var state = new SynthState();
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, state), -1f, 1f);
            // A few milliseconds of fade at the end so nothing clicks.
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create("bb_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        /// <summary>A frequency that slides from a to b over the first `over` seconds, as phase-continuous as needed for blips.</summary>
        static float Glide(float a, float b, float t, float over) => Mathf.Lerp(a, b, Mathf.Clamp01(t / over) * 0.5f);

        /// <summary>Linear attack, exponential decay. Zero before t = 0.</summary>
        static float Env(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay) * 3f);
        }

        static float Chord(float t, SynthState s)
        {
            // A soft major-ninth arpeggio, each note a sine with a little detuned twin.
            float[] notes = { 261.63f, 329.63f, 392.00f, 493.88f, 587.33f };
            float v = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.085f;
                float e = Env(t - start, 0.02f, 2.2f);
                v += (Sine(notes[i], t) + Sine(notes[i] * 1.003f, t) * 0.6f + Sine(notes[i] * 2f, t) * 0.12f) * e;
            }
            return v * 0.075f;
        }

        /// <summary>A sixteen second drone that loops seamlessly: every partial completes whole cycles.</summary>
        static AudioClip Drone()
        {
            const int rate = 22050;
            const float seconds = 16f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            float[] partials = { 55f, 82.5f, 110f, 165f, 220f, 247.5f };
            float[] levels = { 0.5f, 0.32f, 0.22f, 0.12f, 0.05f, 0.035f };
            float[] lfos = { 1f / 16f, 1f / 8f, 3f / 16f, 1f / 4f, 1f / 16f, 3f / 16f };
            var state = new SynthState();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * lfos[p] * t + p);
                    v += Sine(partials[p], t) * levels[p] * swell;
                }
                v += state.Lowpass(state.Noise(), 0.02f) * 0.25f;
                data[i] = v * 0.3f;
            }
            // The noise doesn't repeat, so crossfade the last second into the first.
            int fade = rate;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[n - fade + i] * (1f - k);
            }
            var trimmed = new float[n - fade];
            Array.Copy(data, 0, trimmed, 0, trimmed.Length);
            var clip = AudioClip.Create("bb_drone", trimmed.Length, 1, rate, false);
            clip.SetData(trimmed, 0);
            return clip;
        }
    }
}
