using System;
using System.Collections.Generic;
using System.Text;

namespace Blackbox.Components
{
    /// <summary>
    /// A signal over time: its level at each of the <see cref="Steps"/> steps of one
    /// loop of a run. A bit, digit or shape that never changes is a flat trace; a
    /// wave rises and falls. Traces are what wires will carry once circuits mix
    /// signal types, and how a signal is drawn and matched over a whole run.
    ///
    /// Levels by medium:
    /// <list type="bullet">
    /// <item>Binary: 0 or 1.</item>
    /// <item>Decimal: 0 to 9.</item>
    /// <item>Shape: the shape's value (<see cref="Shapes"/>).</item>
    /// <item>Wave: a height between -7 and 7, any fraction.</item>
    /// <item>Sampled: a whole height between -7 and 7.</item>
    /// </list>
    /// </summary>
    public sealed class Trace
    {
        /// <summary>
        /// Steps in one loop. Every frequency's cycles, half cycles and quarter
        /// cycles, and every sampled wave's steps, fall exactly on a step.
        /// </summary>
        public const int Steps = 288;

        /// <summary>The highest a wave goes above (or below) the middle.</summary>
        public const int Peak = 7;

        /// <summary>How far apart two analog levels may be and still count as equal.</summary>
        public const float Tolerance = 0.05f;

        readonly float[] _levels;

        public Medium Medium { get; }

        public Trace(Medium medium, float[] levels)
        {
            if (levels == null || levels.Length != Steps) throw new ArgumentException($"A trace has {Steps} levels.", nameof(levels));
            Medium = medium;
            _levels = levels;
        }

        public float this[int step] => _levels[step];

        /// <summary>A copy of the levels.</summary>
        public float[] Levels => (float[])_levels.Clone();

        /// <summary>The trace of a value: flat for bits, digits and shapes, the wave itself for waves.</summary>
        public static Trace Of(Medium m, int value)
        {
            if (!Media.IsValid(m, value)) throw new ArgumentOutOfRangeException(nameof(value), $"{value} isn't a {Media.Key(m)} value.");
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++)
            {
                switch (m)
                {
                    case Medium.Wave: levels[k] = (float)Waves.AtStep(value, k, Steps); break;
                    case Medium.Sampled: levels[k] = Sampled.AtStep(value, k, Steps); break;
                    default: levels[k] = value; break;
                }
            }
            return new Trace(m, levels);
        }

        /// <summary>The same level all the way through.</summary>
        public static Trace Flat(Medium m, float level)
        {
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = level;
            return new Trace(m, levels);
        }

        /// <summary>True when the level never changes.</summary>
        public bool IsFlat
        {
            get
            {
                for (int k = 1; k < Steps; k++)
                    if (Math.Abs(_levels[k] - _levels[0]) > Tolerance) return false;
                return true;
            }
        }

        /// <summary>
        /// True when both are the same medium and level at every step: exactly for
        /// everything but analog waves, which may differ by <see cref="Tolerance"/>.
        /// </summary>
        public bool Matches(Trace other)
        {
            if (other == null || other.Medium != Medium) return false;
            float slack = Medium == Medium.Wave ? Tolerance : 0.001f;
            for (int k = 0; k < Steps; k++)
                if (Math.Abs(_levels[k] - other._levels[k]) > slack) return false;
            return true;
        }

        /// <summary>
        /// A pattern: each value held for an equal share of the loop, in turn. The
        /// number of values must divide <see cref="Steps"/> (1, 2, 3, 4, 6, 8, 9, 12, 16, ...).
        /// </summary>
        public static Trace Pattern(Medium m, int[] values)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("A pattern needs at least one value.", nameof(values));
            if (Steps % values.Length != 0) throw new ArgumentException($"A pattern of {values.Length} values doesn't divide a loop of {Steps} steps evenly.", nameof(values));
            if (values.Length == 1) return Of(m, values[0]);
            foreach (int v in values)
                if (!Media.IsValid(m, v) || Media.IsWave(m)) throw new ArgumentOutOfRangeException(nameof(values), $"{v} can't be part of a {Media.Key(m)} pattern.");
            var levels = new float[Steps];
            int each = Steps / values.Length;
            for (int k = 0; k < Steps; k++) levels[k] = values[k / each];
            return new Trace(m, levels);
        }

        /// <summary>True when a pattern of this many values fits the loop evenly.</summary>
        public static bool FitsLoop(int count) => count > 0 && Steps % count == 0;

        /// <summary>The steady level of a flat trace (its first level).</summary>
        public float First => _levels[0];

        /// <summary>
        /// The trace as runs of one level: (first step, length, level), in order.
        /// A wave has a run for almost every step; bits, digits and shapes few.
        /// </summary>
        public List<(int start, int length, float level)> Runs()
        {
            var runs = new List<(int, int, float)>();
            int start = 0;
            for (int k = 1; k <= Steps; k++)
            {
                if (k < Steps && Math.Abs(_levels[k] - _levels[start]) <= 0.001f) continue;
                runs.Add((start, k - start, _levels[start]));
                start = k;
            }
            return runs;
        }

        /// <summary>The lowest and highest levels.</summary>
        public void Range(out float low, out float high)
        {
            low = high = _levels[0];
            foreach (float v in _levels)
            {
                if (v < low) low = v;
                if (v > high) high = v;
            }
        }

        /// <summary>The same signal, <paramref name="steps"/> later (it wraps round the loop).</summary>
        public Trace Later(int steps)
        {
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = _levels[((k - steps) % Steps + Steps) % Steps];
            return new Trace(Medium, levels);
        }

        /// <summary>The same signal played <paramref name="times"/> times as fast, so it repeats that many times a loop.</summary>
        public Trace Faster(int times)
        {
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = _levels[(k * times) % Steps];
            return new Trace(Medium, levels);
        }

        /// <summary>Each level changed by <paramref name="f"/>, as a trace of <paramref name="medium"/>.</summary>
        public Trace Map(Medium medium, Func<float, float> f)
        {
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = f(_levels[k]);
            return new Trace(medium, levels);
        }

        /// <summary>Two traces combined step by step, as a trace of <paramref name="medium"/>.</summary>
        public static Trace Zip(Medium medium, Trace a, Trace b, Func<float, float, float> f)
        {
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = f(a._levels[k], b._levels[k]);
            return new Trace(medium, levels);
        }

        /// <summary>A key equal for traces that <see cref="Matches"/> would call the same (for caching and counting).</summary>
        public string Key
        {
            get
            {
                if (_key != null) return _key;
                var sb = new StringBuilder(Steps * 3 + 4);
                sb.Append((int)Medium).Append(':');
                // Waves to the nearest tenth: close enough to tell traces apart, and
                // coarse enough that rounding noise doesn't split the same trace.
                float scale = Medium == Medium.Wave ? 10f : 1f;
                int last = int.MinValue, run = 0;
                for (int k = 0; k <= Steps; k++)
                {
                    int q = k < Steps ? (int)Math.Round(_levels[k] * scale) : int.MinValue + 1;
                    if (q == last) { run++; continue; }
                    if (run > 0) sb.Append(last).Append('x').Append(run).Append(' ');
                    last = q;
                    run = 1;
                }
                return _key = sb.ToString();
            }
        }

        string _key;

        // ------------------------------------------------------------------ mixing

        /// <summary>
        /// The trace read as another medium, step by step. This is how a block
        /// reads a signal of a type it doesn't work on. The rules, to be tuned when
        /// the mixing tutorial is built:
        /// <list type="bullet">
        /// <item>As a bit: a digit is 1 unless it's 0; a shape is 1 when solid; a wave is 1 while it's above the middle.</item>
        /// <item>As a digit: a bit is 0 or 1; a shape is its number of corners; a wave is its height on a scale of 0 (bottom) to 9 (top).</item>
        /// <item>As a shape: a circle, solid while the signal reads as 1.</item>
        /// <item>As an analog wave: a bit, or a shape's fill, is a full-height square (1 at the top, 0 at the bottom); a digit is a flat level from the bottom (0) to the top (9).</item>
        /// <item>As a sampled wave: the wave reading, rounded to a whole level at every step.</item>
        /// </list>
        /// </summary>
        public Trace As(Medium to)
        {
            if (to == Medium) return this;
            var levels = new float[Steps];
            for (int k = 0; k < Steps; k++) levels[k] = Convert(Medium, _levels[k], to);
            return new Trace(to, levels);
        }

        static float Convert(Medium from, float level, Medium to)
        {
            switch (to)
            {
                case Medium.Binary: return AsBit(from, level);
                case Medium.Decimal:
                    switch (from)
                    {
                        case Medium.Binary: return level;
                        case Medium.Shape: return Shapes.Corners[Shapes.Form((int)level)];
                        default: return (float)Math.Round((level + Peak) / (2.0 * Peak) * 9.0, MidpointRounding.AwayFromZero);
                    }
                case Medium.Shape: return Shapes.Make(Shapes.Circle, AsBit(from, level) == 1f);
                case Medium.Wave: return AsWave(from, level);
                default: return (float)Math.Round(AsWave(from, level), MidpointRounding.AwayFromZero);
            }
        }

        static float AsBit(Medium from, float level)
        {
            switch (from)
            {
                case Medium.Binary: return level;
                case Medium.Decimal: return level != 0f ? 1f : 0f;
                case Medium.Shape: return Shapes.Solid((int)level) ? 1f : 0f;
                default: return level > Tolerance ? 1f : 0f;
            }
        }

        static float AsWave(Medium from, float level)
        {
            switch (from)
            {
                case Medium.Decimal: return level / 9f * 2f * Peak - Peak;
                case Medium.Binary:
                case Medium.Shape: return AsBit(from, level) == 1f ? Peak : -Peak;
                default: return level;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Media.Key(Medium)).Append(':');
            for (int k = 0; k < Steps; k += Steps / 16) sb.Append(' ').Append(Math.Round(_levels[k], 2));
            return sb.ToString();
        }
    }
}
