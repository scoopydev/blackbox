using System;

namespace Blackbox.Components
{
    /// <summary>
    /// How every signal sounds, so each medium can be heard as well as seen. Pure
    /// maths with no engine code, so the sounds can be tested; the audio system
    /// turns the samples into clips.
    /// <list type="bullet">
    /// <item>Bits: a 1 beeps. A 0 is only a soft, dull tap, so the player knows it played.</item>
    /// <item>Digits: one steady tone, longer the bigger the digit. 0 is the dull tap.</item>
    /// <item>Shapes: one click per corner (a circle is one smooth note), low and warm when solid, high and thin when hollow.</item>
    /// <item>Waves: the wave itself, a sine, square or saw at A3, E4 or A4.</item>
    /// </list>
    /// </summary>
    public static class SignalSound
    {
        public const int SampleRate = 44100;

        /// <summary>How much longer a digit's tone gets for each step up, in seconds: 1 is this long, 9 nine times as long.</summary>
        public const float DigitStep = 0.15f;
        /// <summary>The time between a shape's corner clicks, in seconds.</summary>
        public const float CornerSpacing = 0.11f;

        const float Tau = (float)(Math.PI * 2.0);
        const float BeepHz = 880f;
        const float DigitHz = 523.25f;
        const float SolidHz = 261.63f;
        const float HollowHz = 783.99f;
        static readonly float[] WaveHz = { 220f, 329.63f, 440f };

        /// <summary>How long a value's sound lasts, in seconds.</summary>
        public static float Duration(Medium m, int v)
        {
            switch (m)
            {
                case Medium.Binary:
                    return 0.26f;
                case Medium.Decimal:
                    return v <= 0 ? 0.26f : v * DigitStep + 0.1f;
                case Medium.Shape:
                {
                    int corners = Shapes.Corners[Shapes.Form(Clamp(v, 0, 7))];
                    return corners == 0 ? 0.5f : corners * CornerSpacing + 0.16f;
                }
                default:
                    return 0.8f;
            }
        }

        /// <summary>The sound of one value, as mono samples between -1 and 1.</summary>
        public static float[] Render(Medium m, int v, int rate = SampleRate)
        {
            int n = (int)Math.Ceiling(Duration(m, v) * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = Sample(m, v, i / (float)rate);
                data[i] = s < -1f ? -1f : s > 1f ? 1f : s;
            }
            // A few milliseconds of fade at the end so nothing clicks.
            int fade = Math.Min(n, rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            return data;
        }

        static float Sample(Medium m, int v, float t)
        {
            switch (m)
            {
                case Medium.Binary:
                    return v == 1 ? Beep(t) : Tap(t);
                case Medium.Decimal:
                    return v <= 0 ? Tap(t) : Digit(v, t);
                case Medium.Shape:
                    return ShapeSound(Shapes.Form(v), Shapes.Solid(v), t);
                default:
                    return WaveSound(Waves.Form(v), Waves.Pitch(v), t);
            }
        }

        // ------------------------------------------------------------------ the sounds

        /// <summary>Nothing much: a soft, low tap.</summary>
        static float Tap(float t) => Sin(150f, t) * Decay(t, 0.002f, 0.03f) * 0.08f;

        static float Beep(float t)
        {
            float tone = Sin(BeepHz, t) + 0.18f * Sin(BeepHz * 2f, t);
            return tone * Gate(t, 0.005f, 0.17f, 0.025f) * 0.2f;
        }

        /// <summary>One unbroken tone, held for as long as the digit is big.</summary>
        static float Digit(int v, float t)
        {
            float tone = Sin(DigitHz, t) + 0.2f * Sin(DigitHz * 2f, t) + 0.05f * Sin(DigitHz * 3f, t);
            return tone * Gate(t, 0.006f, v * DigitStep, 0.02f) * 0.17f;
        }

        static float ShapeSound(int form, bool solid, float t)
        {
            float tone = solid
                ? Sin(SolidHz, t) + 0.5f * Sin(SolidHz * 2f, t) + 0.3f * Sin(SolidHz * 3f, t) + 0.12f * Sin(SolidHz * 4f, t)
                : Sin(HollowHz, t) + 0.08f * Sin(HollowHz * 2f, t);
            float gain = solid ? 0.17f : 0.15f;
            int corners = Shapes.Corners[form];
            if (corners == 0)
            {
                // No corners: one smooth, round note.
                return tone * Gate(t, 0.04f, 0.32f, 0.045f) * gain;
            }
            // A sharp click for each corner.
            float env = 0f;
            for (int k = 0; k < corners; k++) env += Decay(t - k * CornerSpacing, 0.002f, solid ? 0.05f : 0.04f);
            return tone * env * gain;
        }

        /// <summary>A sine, square or saw, built from its harmonics up to about 7 kHz so the edges stay soft and nothing aliases.</summary>
        static float WaveSound(int form, int pitch, float t)
        {
            float hz = WaveHz[Clamp(pitch, 0, WaveHz.Length - 1)];
            float w = Tau * hz * t;
            float v = 0f;
            if (form == Waves.Sine) v = (float)Math.Sin(w);
            else
            {
                int harmonics = Math.Max(1, (int)Math.Floor(7000f / hz));
                if (form == Waves.SquareWave)
                {
                    for (int k = 1; k <= harmonics; k += 2) v += (float)Math.Sin(k * w) / k;
                    v *= (float)(4.0 / Math.PI);
                }
                else
                {
                    for (int k = 1; k <= harmonics; k++) v += ((k & 1) == 1 ? 1f : -1f) * (float)Math.Sin(k * w) / k;
                    v *= (float)(2.0 / Math.PI);
                }
            }
            // Each form at roughly the same loudness.
            float gain = form == Waves.Sine ? 0.26f : form == Waves.SquareWave ? 0.15f : 0.17f;
            return v * Gate(t, 0.02f, 0.45f, 0.11f) * gain;
        }

        // ------------------------------------------------------------------ helpers

        static float Sin(float hz, float t) => (float)Math.Sin(Tau * hz * t);

        /// <summary>Linear attack, then exponential decay with time constant <paramref name="tau"/>. Zero before t = 0.</summary>
        static float Decay(float t, float attack, float tau)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return (float)Math.Exp(-(t - attack) / tau);
        }

        /// <summary>Linear attack, held until <paramref name="end"/>, then an exponential release.</summary>
        static float Gate(float t, float attack, float end, float release)
        {
            if (t < 0f) return 0f;
            float a = t < attack ? t / attack : 1f;
            return t <= end ? a : a * (float)Math.Exp(-(t - end) / release);
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
