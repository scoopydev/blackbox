using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// What travels along the wires. Each chapter is built on one medium, and every
    /// value in a circuit is a small whole number whose meaning the medium gives.
    /// </summary>
    public enum Medium
    {
        /// <summary>0 or 1.</summary>
        Binary,
        /// <summary>A digit, 0 to 9. Arithmetic wraps round: 9 + 1 is 0.</summary>
        Decimal,
        /// <summary>A form (circle, triangle, square, hexagon) that is hollow or solid.</summary>
        Shape,
        /// <summary>An analog wave: a sine, square or saw at one of 7 frequencies and 7 heights, shifted by 0 to 3 quarter cycles.</summary>
        Wave,
        /// <summary>A sampled wave: an analog wave read a few times a cycle and held between readings, so it rises and falls in steps.</summary>
        Sampled,
    }

    /// <summary>
    /// Shape values: <c>(colour * 4 + form) * 2 + fill</c>, 32 in all. A shape has
    /// a form (its corners), a fill and a colour. Shape blocks keep a shape's
    /// colour with its form; a light shows only the colour.
    /// </summary>
    public static class Shapes
    {
        public const int Forms = 4, Colours = 4;
        public const int Size = Forms * 2 * Colours;
        public const int Circle = 0, Triangle = 1, Square = 2, Hexagon = 3;
        public static readonly string[] FormNames = { "circle", "triangle", "square", "hexagon" };
        /// <summary>The colours. Cyan is the plain signal colour.</summary>
        public static readonly string[] ColourNames = { "cyan", "pink", "amber", "green" };
        /// <summary>Corners of each form. A circle has none.</summary>
        public static readonly int[] Corners = { 0, 3, 4, 6 };

        public static int Form(int v) => v / 2 % Forms;
        public static bool Solid(int v) => (v & 1) == 1;
        public static int Colour(int v) => v / (Forms * 2) % Colours;
        public static int Make(int form, bool solid, int colour = 0) =>
            (((colour % Colours + Colours) % Colours) * Forms + (form % Forms + Forms) % Forms) * 2 + (solid ? 1 : 0);
    }

    /// <summary>
    /// Analog wave values: <c>((form * 7 + frequency) * 7 + height) * 4 + phase</c>,
    /// 588 in all. Every step is counted from 0 here and shown to the player from 1.
    /// <list type="bullet">
    /// <item>Frequency: how many cycles fit in one loop of a run, <see cref="Cycles"/>. It is also the pitch the wave sounds at.</item>
    /// <item>Height (amplitude): the peak, 1 to 7 levels above the middle. It is also how loud the wave sounds.</item>
    /// <item>Phase: how many quarter cycles later the wave starts. A sine half a cycle late is the same sine upside down.</item>
    /// </list>
    /// </summary>
    public static class Waves
    {
        public const int Forms = 3, Frequencies = 7, Heights = 7, Phases = 4;
        public const int Size = Forms * Frequencies * Heights * Phases;
        public const int Sine = 0, SquareWave = 1, Saw = 2;
        public static readonly string[] FormNames = { "sine", "square", "saw" };

        /// <summary>
        /// Cycles in one loop for each frequency step. The steps are octaves and
        /// fifths of the slowest, so they sound in tune together, and every cycle,
        /// half cycle and quarter cycle lands exactly on a step of a <see cref="Trace"/>.
        /// </summary>
        public static readonly int[] Cycles = { 1, 2, 3, 4, 6, 8, 12 };

        /// <summary>How a phase reads after the frequency and height: "", " +1/4", " +1/2", " +3/4".</summary>
        public static readonly string[] PhaseMarks = { "", " +1/4", " +1/2", " +3/4" };

        public static int Form(int v) => v / (Frequencies * Heights * Phases);
        public static int Frequency(int v) => v / (Heights * Phases) % Frequencies;
        public static int Height(int v) => v / Phases % Heights;
        public static int Phase(int v) => v % Phases;

        /// <summary>A wave from its parts. Each part wraps round, so the step after the last is the first.</summary>
        public static int Make(int form, int frequency, int height, int phase = 0) =>
            ((Wrap(form, Forms) * Frequencies + Wrap(frequency, Frequencies)) * Heights + Wrap(height, Heights)) * Phases + Wrap(phase, Phases);

        public static int WithForm(int v, int form) => Make(form, Frequency(v), Height(v), Phase(v));
        public static int WithFrequency(int v, int frequency) => Make(Form(v), frequency, Height(v), Phase(v));
        public static int WithHeight(int v, int height) => Make(Form(v), Frequency(v), height, Phase(v));
        public static int WithPhase(int v, int phase) => Make(Form(v), Frequency(v), Height(v), phase);

        /// <summary>
        /// The basic form between -1 and 1, <paramref name="cycles"/> cycles in. A sine
        /// starts at the middle going up; a square is high for the first half of each
        /// cycle; a saw climbs steadily from the bottom to the top, then drops sheer.
        /// </summary>
        public static double Shape(int form, double cycles)
        {
            double f = cycles - Math.Floor(cycles);
            switch (form)
            {
                case SquareWave: return f < 0.5 ? 1.0 : -1.0;
                case Saw: return 2.0 * f - 1.0;
                default: return Math.Sin(f * 2.0 * Math.PI);
            }
        }

        /// <summary>The wave's level, in steps of height (-7 to 7), a fraction <paramref name="loop"/> of the way through a loop.</summary>
        public static double At(int v, double loop) =>
            (Height(v) + 1) * Shape(Form(v), Cycles[Frequency(v)] * loop - Phase(v) * 0.25);

        /// <summary>
        /// The level at step <paramref name="step"/> of a loop of <paramref name="steps"/>.
        /// Worked out so a step that falls exactly on an edge of a square or saw
        /// lands on the right side of it.
        /// </summary>
        public static double AtStep(int v, int step, int steps) =>
            (Height(v) + 1) * Shape(Form(v), (double)(Cycles[Frequency(v)] * step) / steps - Phase(v) * 0.25);

        /// <summary>"F3 A5", "F1 A7 +1/4": frequency and amplitude as the player reads them.</summary>
        public static string Settings(int v) => $"F{Frequency(v) + 1} A{Height(v) + 1}{PhaseMarks[Phase(v)]}";

        internal static int Wrap(int v, int n) => ((v % n) + n) % n;
    }

    /// <summary>
    /// Sampled wave values: <c>((form * 7 + frequency) * 7 + height) * 3 + rate</c>,
    /// 441 in all. A sampled wave is an analog one read <see cref="StepsPerCycle"/>
    /// times a cycle, each reading taken at the middle of its step, rounded to a
    /// whole level and held for the step, so it climbs and falls in steps. Frequency
    /// and height work as for <see cref="Waves"/>.
    /// </summary>
    public static class Sampled
    {
        public const int Forms = Waves.Forms, Frequencies = Waves.Frequencies, Heights = Waves.Heights, Rates = 3;
        public const int Size = Forms * Frequencies * Heights * Rates;
        public static readonly int[] StepsPerCycle = { 3, 6, 12 };

        public static int Form(int v) => v / (Frequencies * Heights * Rates);
        public static int Frequency(int v) => v / (Heights * Rates) % Frequencies;
        public static int Height(int v) => v / Rates % Heights;
        public static int Rate(int v) => v % Rates;

        public static int Make(int form, int frequency, int height, int rate) =>
            ((Waves.Wrap(form, Forms) * Frequencies + Waves.Wrap(frequency, Frequencies)) * Heights + Waves.Wrap(height, Heights)) * Rates + Waves.Wrap(rate, Rates);

        public static int WithForm(int v, int form) => Make(form, Frequency(v), Height(v), Rate(v));
        public static int WithFrequency(int v, int frequency) => Make(Form(v), frequency, Height(v), Rate(v));
        public static int WithHeight(int v, int height) => Make(Form(v), Frequency(v), height, Rate(v));
        public static int WithRate(int v, int rate) => Make(Form(v), Frequency(v), Height(v), rate);

        /// <summary>
        /// The level held at a point <paramref name="cycles"/> cycles in: the form read
        /// at the middle of the current step and rounded to a whole level.
        /// </summary>
        public static int Level(int form, int height, int steps, double cycles) =>
            Reading(form, height, steps, (long)Math.Floor(cycles * steps));

        /// <summary>
        /// The level held through the <paramref name="index"/>th step, counted from
        /// the start of the first cycle. A reading exactly halfway between two levels
        /// (7 × sin 30° is 3.5) rounds away from the middle; the nudge stops a sine
        /// that comes out a hair under 0.5 from rounding the other way.
        /// </summary>
        public static int Reading(int form, int height, int steps, long index)
        {
            return RoundLevel((height + 1) * Waves.Shape(form, (index + 0.5) / steps));
        }

        /// <summary>A reading rounded to a whole level, halves away from the middle (see <see cref="Reading"/>).</summary>
        public static int RoundLevel(double r) => (int)Math.Round(r + Math.Sign(r) * 1e-9, MidpointRounding.AwayFromZero);

        /// <summary>The level (-7 to 7) a fraction <paramref name="loop"/> of the way through a loop.</summary>
        public static int At(int v, double loop) =>
            Level(Form(v), Height(v), StepsPerCycle[Rate(v)], Waves.Cycles[Frequency(v)] * loop);

        /// <summary>The level at step <paramref name="step"/> of a loop of <paramref name="steps"/>, counted exactly.</summary>
        public static int AtStep(int v, int step, int steps)
        {
            int perCycle = StepsPerCycle[Rate(v)];
            long index = (long)Waves.Cycles[Frequency(v)] * perCycle * step / steps;
            return Reading(Form(v), Height(v), perCycle, index);
        }

        /// <summary>"F3 A5 S6": frequency, amplitude and steps a cycle.</summary>
        public static string Settings(int v) => $"F{Frequency(v) + 1} A{Height(v) + 1} S{StepsPerCycle[Rate(v)]}";
    }

    public static class Media
    {
        /// <summary>How many values the medium has. Values run from 0 to Size - 1.</summary>
        public static int Size(Medium m)
        {
            switch (m)
            {
                case Medium.Binary: return 2;
                case Medium.Decimal: return 10;
                case Medium.Shape: return Shapes.Size;
                case Medium.Wave: return Waves.Size;
                default: return Sampled.Size;
            }
        }

        public static bool IsValid(Medium m, int v) => v >= 0 && v < Size(m);

        /// <summary>
        /// True when two values are the same signal. Bits, digits and shapes are
        /// the same only when equal. Waves are the same when they trace the same
        /// line through a run: a square read 2 or 8 times a cycle is still the
        /// same square, so either meets a target that asks for the other.
        /// </summary>
        public static bool Same(Medium m, int a, int b)
        {
            if (a == b) return true;
            if (!IsWave(m) || !IsValid(m, a) || !IsValid(m, b)) return false;
            return TraceOf(m, a).Matches(TraceOf(m, b));
        }

        static readonly Dictionary<(Medium, int), Trace> s_Traces = new Dictionary<(Medium, int), Trace>();

        /// <summary>The value's trace, made once and kept. Traces never change.</summary>
        public static Trace TraceOf(Medium m, int v)
        {
            lock (s_Traces)
            {
                if (!s_Traces.TryGetValue((m, v), out var trace)) s_Traces[(m, v)] = trace = Trace.Of(m, v);
                return trace;
            }
        }

        /// <summary>True for the two kinds of wave, which are drawn as lines and heard as tones.</summary>
        public static bool IsWave(Medium m) => m == Medium.Wave || m == Medium.Sampled;

        /// <summary>The medium's name as puzzle files write it.</summary>
        public static string Key(Medium m) => m.ToString().ToLowerInvariant();

        public static bool TryParseMedium(string s, out Medium m)
        {
            foreach (Medium candidate in Enum.GetValues(typeof(Medium)))
            {
                if (string.Equals(Key(candidate), s?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    m = candidate;
                    return true;
                }
            }
            m = Medium.Binary;
            return false;
        }

        /// <summary>What one value is called: "a bit", "a digit", "a shape", "a wave", "a sampled wave".</summary>
        public static string Noun(Medium m)
        {
            switch (m)
            {
                case Medium.Binary: return "bit";
                case Medium.Decimal: return "digit";
                case Medium.Shape: return "shape";
                case Medium.Wave: return "wave";
                default: return "sampled wave";
            }
        }

        /// <summary>The verb an output uses for its value in goal sentences.</summary>
        public static string Verb(Medium m) => IsWave(m) ? "play" : "show";

        /// <summary>
        /// A value in words, for puzzle files, tests and the editor: "1", "7",
        /// "solid triangle", "sine f3 a5", "square f1 a7 p2" (two quarter cycles late),
        /// "saw f2 a4 s6" (sampled 6 times a cycle).
        /// </summary>
        public static string Describe(Medium m, int v)
        {
            if (!IsValid(m, v)) return "?";
            switch (m)
            {
                case Medium.Binary:
                case Medium.Decimal:
                    return v.ToString();
                case Medium.Shape:
                    return (Shapes.Solid(v) ? "solid " : "hollow ") + (Shapes.Colour(v) > 0 ? Shapes.ColourNames[Shapes.Colour(v)] + " " : "") + Shapes.FormNames[Shapes.Form(v)];
                case Medium.Wave:
                    return $"{Waves.FormNames[Waves.Form(v)]} f{Waves.Frequency(v) + 1} a{Waves.Height(v) + 1}" + (Waves.Phase(v) > 0 ? $" p{Waves.Phase(v)}" : "");
                default:
                    return $"{Waves.FormNames[Sampled.Form(v)]} f{Sampled.Frequency(v) + 1} a{Sampled.Height(v) + 1} s{Sampled.StepsPerCycle[Sampled.Rate(v)]}";
            }
        }

        /// <summary>
        /// Reads a value as puzzle files write it: a number for binary and decimal,
        /// words for shapes ("solid triangle"), and for waves the form followed by
        /// its settings in any order: f1 to f7 (frequency), a1 to a7 (amplitude),
        /// p0 to p3 for an analog wave (quarter cycles late, 0 if left out) and s3,
        /// s6 or s12 for a sampled one (steps a cycle). "sine f3 a5", "saw f1 a7 s6".
        /// </summary>
        public static bool TryParse(Medium m, string s, out int v)
        {
            v = -1;
            if (s == null) return false;
            s = s.Trim().ToLowerInvariant();
            if (int.TryParse(s, out int n))
            {
                v = n;
                return IsValid(m, n);
            }
            if (m == Medium.Shape)
            {
                // "solid triangle", "hollow pink square": fill, then an optional colour, then form.
                var words = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 2 || words.Length > 3) return false;
                bool solid = words[0] == "solid";
                if (!solid && words[0] != "hollow") return false;
                int colour = 0;
                if (words.Length == 3)
                {
                    colour = Array.IndexOf(Shapes.ColourNames, words[1]);
                    if (colour < 0) return false;
                }
                int f = Array.IndexOf(Shapes.FormNames, words[words.Length - 1]);
                if (f < 0) return false;
                v = Shapes.Make(f, solid, colour);
                return true;
            }
            if (IsWave(m)) return TryParseWave(m, s, out v);
            return false;
        }

        static bool TryParseWave(Medium m, string s, out int v)
        {
            v = -1;
            var parts = s.Replace(",", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;
            int form = Array.IndexOf(Waves.FormNames, parts[0]);
            if (form < 0) return false;
            int frequency = -1, height = -1, phase = -1, steps = -1;
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 2 || !int.TryParse(part.Substring(1), out int n)) return false;
                switch (part[0])
                {
                    case 'f': if (frequency >= 0 || n < 1 || n > Waves.Frequencies) return false; frequency = n - 1; break;
                    case 'a': if (height >= 0 || n < 1 || n > Waves.Heights) return false; height = n - 1; break;
                    case 'p': if (m != Medium.Wave || phase >= 0 || n < 0 || n >= Waves.Phases) return false; phase = n; break;
                    case 's':
                        if (m != Medium.Sampled || steps >= 0) return false;
                        steps = Array.IndexOf(Sampled.StepsPerCycle, n);
                        if (steps < 0) return false;
                        break;
                    default: return false;
                }
            }
            if (frequency < 0 || height < 0) return false;
            if (m == Medium.Wave)
            {
                v = Waves.Make(form, frequency, height, Math.Max(0, phase));
                return true;
            }
            if (steps < 0) return false;
            v = Sampled.Make(form, frequency, height, steps);
            return true;
        }

        /// <summary>Every value of the medium in order, for tests and tools.</summary>
        public static IEnumerable<int> All(Medium m)
        {
            int n = Size(m);
            for (int i = 0; i < n; i++) yield return i;
        }
    }
}
