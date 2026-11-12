using System;

namespace Blackbox.Components
{
    /// <summary>
    /// How every signal sounds, so each medium can be heard as well as seen. Pure
    /// maths with no engine code, so the sounds can be tested; the audio system
    /// turns the samples into clips.
    /// <list type="bullet">
    /// <item>Bits: a 1 beeps. A 0 is only a soft, dull tap, so the player knows it played.</item>
    /// <item>Digits: one short tone, higher the bigger the digit, climbing a major scale from C4 (0) to E5 (9).</item>
    /// <item>Shapes: one click per corner (a circle is one smooth note), low and warm when solid, high and thin when hollow.</item>
    /// <item>Analog waves: the wave itself. Each frequency step is a pitch (D3 up to A6, in octaves and fifths) and each height step is louder. Phase can't be heard.</item>
    /// <item>Sampled waves: the same pitches, but the wave is held in steps as it is drawn, so it sounds buzzy and stepped, and louder the taller its steps. Fewer steps sound harsher.</item>
    /// </list>
    /// </summary>
    public static class SignalSound
    {
        public const int SampleRate = 44100;

        /// <summary>The pitch of each digit, in Hz: a major scale, do (0) to mi an octave up (9), so counting up sounds like climbing.</summary>
        public static readonly float[] DigitHz = { 261.63f, 293.66f, 329.63f, 349.23f, 392f, 440f, 493.88f, 523.25f, 587.33f, 659.25f };
        /// <summary>The time between a shape's corner clicks, in seconds.</summary>
        public const float CornerSpacing = 0.11f;

        const float Tau = (float)(Math.PI * 2.0);
        const float BeepHz = 880f;
        const float SolidHz = 261.63f;
        const float HollowHz = 783.99f;
        /// <summary>The pitch of the slowest wave. Each frequency step multiplies it by <see cref="Waves.Cycles"/>, so step 3 is A4.</summary>
        public const float WaveBaseHz = 440f / 3f;

        /// <summary>How long a value's sound lasts, in seconds.</summary>
        public static float Duration(Medium m, int v)
        {
            switch (m)
            {
                case Medium.Binary:
                    return 0.26f;
                case Medium.Decimal:
                    return 0.36f;
                case Medium.Shape:
                {
                    int corners = Shapes.Corners[Shapes.Form(Clamp(v, 0, Shapes.Size - 1))];
                    return corners == 0 ? 0.5f : corners * CornerSpacing + 0.16f;
                }
                default:
                    return WaveLength;
            }
        }

        const float WaveLength = 0.8f;

        /// <summary>The pitch of a wave's frequency step, in Hz.</summary>
        public static float WaveHz(int frequency) => WaveBaseHz * Waves.Cycles[Clamp(frequency, 0, Waves.Frequencies - 1)];

        /// <summary>How loud a height step is, from 1 (the tallest) down by 2.5 dB a step.</summary>
        public static float HeightGain(int height) => (float)Math.Pow(10.0, -2.5 * (Waves.Heights - 1 - Clamp(height, 0, Waves.Heights - 1)) / 20.0);

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

        /// <summary>
        /// How long a whole signal plays for when it changes through the loop. The
        /// same as one loop of the signals on screen, so the playhead sweeps each
        /// value as it sounds.
        /// </summary>
        public const float PatternLength = 2.4f;

        /// <summary>How long a signal over a loop sounds, in seconds.</summary>
        public static float Duration(Trace t)
        {
            if (Media.IsWave(t.Medium)) return WaveLength;
            if (t.IsFlat) return Duration(t.Medium, (int)Math.Round(t.First));
            return PatternLength;
        }

        /// <summary>
        /// The sound of a signal over one loop. A steady bit, digit or shape sounds
        /// as its value. One that changes plays each value in turn, each for its
        /// share of <see cref="PatternLength"/>, cut short as the next begins. A wave
        /// (or anything a block turned into one) is played as a sound wave: the loop
        /// repeats at <see cref="WaveBaseHz"/>, so it has the same pitch as the
        /// waves of chapter IV, and two waves that cancel out are silent.
        /// </summary>
        public static float[] Render(Trace trace, int rate = SampleRate)
        {
            if (!Media.IsWave(trace.Medium) && trace.IsFlat) return Render(trace.Medium, (int)Math.Round(trace.First), rate);
            int n = (int)Math.Ceiling(Duration(trace) * rate);
            var data = new float[n];
            if (Media.IsWave(trace.Medium))
            {
                bool held = trace.Medium == Medium.Sampled;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    double phase = t * (double)WaveBaseHz;
                    double at = (phase - Math.Floor(phase)) * Trace.Steps;
                    int k = (int)at;
                    float level = held ? trace[k % Trace.Steps] : Lerp(trace[k % Trace.Steps], trace[(k + 1) % Trace.Steps], (float)(at - k));
                    data[i] = level / Trace.Peak * Gate(t, 0.02f, 0.45f, 0.11f) * 0.2f;
                }
            }
            else
            {
                foreach (var (start, length, level) in trace.Runs())
                {
                    int from = (int)((long)start * n / Trace.Steps), to = (int)((long)(start + length) * n / Trace.Steps);
                    int fade = Math.Min(rate / 200, (to - from) / 4);
                    int v = (int)Math.Round(level);
                    for (int i = from; i < to && i < n; i++)
                    {
                        float s = Sample(trace.Medium, v, (i - from) / (float)rate);
                        int left = to - 1 - i;
                        if (left < fade) s *= left / (float)Math.Max(1, fade);
                        data[i] = s;
                    }
                }
            }
            for (int i = 0; i < n; i++) data[i] = data[i] < -1f ? -1f : data[i] > 1f ? 1f : data[i];
            int end = Math.Min(n, rate / 200);
            for (int i = 0; i < end; i++) data[n - 1 - i] *= i / (float)end;
            return data;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Sample(Medium m, int v, float t)
        {
            switch (m)
            {
                case Medium.Binary:
                    return v == 1 ? Beep(t) : Tap(t);
                case Medium.Decimal:
                    return Digit(v, t);
                case Medium.Shape:
                    return ShapeSound(Shapes.Form(v), Shapes.Solid(v), Shapes.Colour(v), t);
                case Medium.Wave:
                    return WaveSound(Waves.Form(v), Waves.Frequency(v), t) * HeightGain(Waves.Height(v));
                default:
                    return SampledSound(v, t);
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

        /// <summary>One short tone, the same length for every digit, pitched by the digit.</summary>
        static float Digit(int v, float t)
        {
            float hz = DigitHz[Clamp(v, 0, DigitHz.Length - 1)];
            float tone = Sin(hz, t) + 0.2f * Sin(hz * 2f, t) + 0.05f * Sin(hz * 3f, t);
            return tone * Gate(t, 0.006f, 0.22f, 0.04f) * 0.17f;
        }

        /// <summary>How much each shape colour raises a shape's clicks: cyan, then a third, a fifth and a sixth above.</summary>
        public static readonly float[] ColourPitch = { 1f, 1.26f, 1.5f, 1.68f };

        static float ShapeSound(int form, bool solid, int colour, float t)
        {
            float lift = ColourPitch[Clamp(colour, 0, ColourPitch.Length - 1)];
            float solidHz = SolidHz * lift, hollowHz = HollowHz * lift;
            float tone = solid
                ? Sin(solidHz, t) + 0.5f * Sin(solidHz * 2f, t) + 0.3f * Sin(solidHz * 3f, t) + 0.12f * Sin(solidHz * 4f, t)
                : Sin(hollowHz, t) + 0.08f * Sin(hollowHz * 2f, t);
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
        static float WaveSound(int form, int frequency, float t)
        {
            float hz = WaveHz(frequency);
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

        /// <summary>
        /// The wave held in steps at audio speed, just as it is drawn: read a few
        /// times a cycle, rounded to a whole level and held. The held steps add a
        /// buzz of their own; the fewer the steps, the buzzier. The sound follows
        /// the levels alone, so two sampled waves that trace the same steps sound
        /// the same, whatever form they were read from.
        /// </summary>
        static float SampledSound(int v, float t)
        {
            int form = Sampled.Form(v), height = Sampled.Height(v), steps = Sampled.StepsPerCycle[Sampled.Rate(v)];
            float hz = WaveHz(Sampled.Frequency(v));
            float level = Sampled.Level(form, height, steps, hz * t) / (float)Trace.Peak;
            return level * Gate(t, 0.02f, 0.45f, 0.11f) * 0.18f;
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

        /// <summary>
        /// True when the two can't be told apart by ear, so a circuit played by ear
        /// can't ask the player to hear the difference. A bit, digit or shape plays
        /// its values in turn from the start of the loop, so it sounds alike only
        /// when it looks alike. A wave is heard as a tone repeating every loop, so
        /// where it starts, which way up it is and how far it sits off the middle
        /// can't be heard: a wave sounds like the same wave started later, turned
        /// upside down, or raised, and every flat wave is silence.
        /// </summary>
        public static bool SoundsAlike(Trace a, Trace b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.LooksLike(b)) return true;
            if (a.Medium != b.Medium || !Media.IsWave(a.Medium)) return false;
            int n = Trace.Steps;
            double meanA = 0, meanB = 0;
            for (int k = 0; k < n; k++) { meanA += a[k]; meanB += b[k]; }
            meanA /= n;
            meanB /= n;
            const float slack = 0.1f;
            for (int shift = 0; shift < n; shift++)
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    int off = 0;
                    for (int k = 0; k < n && off <= Trace.Blip; k++)
                        if (Math.Abs((a[k] - meanA) - sign * (b[(k + shift) % n] - meanB)) > slack) off++;
                    if (off <= Trace.Blip) return true;
                }
            return false;
        }
    }
}
