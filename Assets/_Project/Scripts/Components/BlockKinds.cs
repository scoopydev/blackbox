using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// A kind of block: one fixed rule, the same in every circuit, which is what
    /// lets the player carry what they learn from one circuit to the next. A block
    /// has no settings. The player sees only its label (K1, K2 ...) and its ports,
    /// never its kind or its rule, until its circuit is solved.
    ///
    /// Every rule is meant to be simple enough to say in a few words and to work
    /// out by experiment, with no exceptions: no ties to break, no wrapping past
    /// 9, no signal a block refuses.
    /// </summary>
    public sealed class BlockKind
    {
        readonly Func<int[], int> _rule;
        readonly Func<Trace[], Trace> _traceRule;

        /// <summary>The kind's id, used by puzzle files and tests. Never shown to the player.</summary>
        public string Id { get; }

        /// <summary>
        /// The kind of signal the block sends. Bit, digit, shape and wave blocks
        /// also read every input as this kind. A mixing block reads its inputs its
        /// own way (see <see cref="Mixing"/>).
        /// </summary>
        public Medium Medium { get; }

        public int InputCount { get; }

        /// <summary>
        /// A mixing block (GATE, COUNT, COMPARE): taught in Blend, it works on
        /// whole signals of any kind, as its rule says.
        /// </summary>
        public bool Mixing { get; }

        /// <summary>The whole rule, in a sentence, shown when the player points at a block in a solved circuit.</summary>
        public string Rule { get; }

        /// <summary>The rule in a few words (at most 30 characters), written under a block once its circuit is solved.</summary>
        public string ShortRule { get; }

        /// <summary>
        /// Set aside for now: the kind still works, but no circuit may use it.
        /// See <see cref="KindCatalog.Shelved"/>.
        /// </summary>
        public bool Shelved => KindCatalog.Shelved.Contains(Id);

        /// <summary>
        /// Always 1: a block has one fixed rule. Kept for the old settings screens
        /// (the title screen's demo), which never find a block to change.
        /// </summary>
        public int StateCount => 1;

        /// <summary>A block that works one value at a time: every input is read as <paramref name="medium"/>.</summary>
        public BlockKind(string id, Medium medium, int inputCount, string rule, string shortRule, Func<int[], int> valueRule)
        {
            Id = id;
            Medium = medium;
            InputCount = inputCount;
            Rule = rule;
            ShortRule = shortRule;
            _rule = valueRule ?? throw new ArgumentNullException(nameof(valueRule));
        }

        /// <summary>A block that works on whole signals over the loop.</summary>
        public BlockKind(string id, Medium medium, int inputCount, bool mixing, string rule, string shortRule, Func<Trace[], Trace> traceRule)
        {
            Id = id;
            Medium = medium;
            InputCount = inputCount;
            Mixing = mixing;
            Rule = rule;
            ShortRule = shortRule;
            _traceRule = traceRule ?? throw new ArgumentNullException(nameof(traceRule));
        }

        /// <summary>The block's output for these single values, for blocks that work one value at a time.</summary>
        public int Apply(int[] inputs)
        {
            if (_rule == null) throw new InvalidOperationException($"{Id} works on whole signals, not single values.");
            return _rule(inputs);
        }

        /// <summary>Kept for the old settings screens: a block's one rule, whatever the state.</summary>
        public int Apply(int[] inputs, int state) => Apply(inputs);

        /// <summary>
        /// The block's output, over a whole loop, for these input signals. A bit,
        /// digit or shape block reads each input as its own kind, step by step
        /// (see <see cref="Trace.As"/>), and applies its rule at every step. Wave
        /// and mixing blocks work on the whole signal. Every block sends something
        /// for any inputs.
        /// </summary>
        public Trace Apply(Trace[] inputs)
        {
            if (inputs == null || inputs.Length != InputCount) throw new ArgumentException($"{Id} takes {InputCount} input(s).", nameof(inputs));
            if (_traceRule != null) return _traceRule(inputs);

            var read = new Trace[InputCount];
            bool flat = true;
            for (int p = 0; p < InputCount; p++)
            {
                read[p] = inputs[p].As(Medium);
                flat &= read[p].IsFlat;
            }
            var values = new int[InputCount];
            if (flat)
            {
                for (int p = 0; p < InputCount; p++) values[p] = (int)read[p].First;
                return Trace.Flat(Medium, _rule(values));
            }
            var levels = new float[Trace.Steps];
            for (int k = 0; k < Trace.Steps; k++)
            {
                for (int p = 0; p < InputCount; p++) values[p] = (int)Math.Round(read[p][k]);
                levels[k] = _rule(values);
            }
            return new Trace(Medium, levels);
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Every block kind in the game. Kinds are code, not data, so their behaviour is
    /// type checked and unit tested; puzzle files refer to them by id.
    ///
    /// Each kind of signal has four blocks, and each set starts with a block that
    /// flips the signal (NOT, MIRROR, FILL, FLIP), so a player who has learned one
    /// set has a head start on the next. The three mixing blocks come in Blend.
    /// </summary>
    public static class KindCatalog
    {
        static readonly Dictionary<string, BlockKind> s_Kinds = new Dictionary<string, BlockKind>();
        static readonly List<BlockKind> s_Ordered = new List<BlockKind>();

        static KindCatalog()
        {
            // ---- Bits: 0 and 1 ------------------------------------------------------

            Value("NOT", Medium.Binary, 1, "flips a bit: 1 becomes 0 and 0 becomes 1", "flips 0 and 1",
                x => 1 - x[0]);
            Value("AND", Medium.Binary, 2, "1 only while both inputs are 1", "1 when both are 1",
                x => x[0] & x[1]);
            Value("OR", Medium.Binary, 2, "1 while either input is 1", "1 when either is 1",
                x => x[0] | x[1]);
            Value("XOR", Medium.Binary, 2, "1 while the two inputs are different", "1 when they differ",
                x => x[0] ^ x[1]);

            // ---- Digits: 0 to 9. No rule can go below 0 or past 9 ------------------

            Value("MIRROR", Medium.Decimal, 1, "turns a digit around: 9 minus the digit, so 0 and 9 swap, 1 and 8, 2 and 7", "9 minus the digit",
                x => 9 - x[0]);
            Value("MAX", Medium.Decimal, 2, "the larger of the two digits", "the larger digit",
                x => Math.Max(x[0], x[1]));
            Value("MIN", Medium.Decimal, 2, "the smaller of the two digits", "the smaller digit",
                x => Math.Min(x[0], x[1]));
            Value("DIFF", Medium.Decimal, 2, "how far apart the two digits are: the larger minus the smaller", "how far apart they are",
                x => Math.Abs(x[0] - x[1]));

            // ---- Shapes: a form, a fill and a colour ---------------------------------

            Value("FILL", Medium.Shape, 1, "flips a shape's fill: hollow becomes solid and solid becomes hollow", "flips the fill",
                x => x[0] ^ 1);
            Value("NEXT", Medium.Shape, 1, "the next form round: circle, triangle, square, hexagon, then circle again, keeping the fill and colour", "the next form",
                x => Shapes.Make(Shapes.Form(x[0]) + 1, Shapes.Solid(x[0]), Shapes.Colour(x[0])));
            Value("TAKEFILL", Medium.Shape, 2, "the top shape, with the bottom shape's fill", "top shape, bottom's fill",
                x => Shapes.Make(Shapes.Form(x[0]), Shapes.Solid(x[1]), Shapes.Colour(x[0])));
            Value("TAKECOLOUR", Medium.Shape, 2, "the top shape, in the bottom shape's colour", "top shape, bottom's colour",
                x => Shapes.Make(Shapes.Form(x[0]), Shapes.Solid(x[0]), Shapes.Colour(x[1])));

            // ---- Waves: every input read as a wave, a wave sent ----------------------

            Whole("FLIP", Medium.Wave, 1, false, "turns a wave upside down", "upside down",
                x => x[0].As(Medium.Wave).Map(Medium.Wave, v => v == 0f ? 0f : -v));
            Whole("FASTER", Medium.Wave, 1, false, "plays the wave twice as fast, so it repeats twice a loop", "twice as fast",
                x => x[0].As(Medium.Wave).Faster(2));
            Whole("LATER", Medium.Wave, 1, false, "plays the wave a quarter of the loop later", "a quarter loop later",
                x => x[0].As(Medium.Wave).Later(Trace.Steps / 4));
            Whole("MIX", Medium.Wave, 2, false, "halfway between the two waves at every moment, so a wave and its upside-down twin mix to a flat line", "halfway between them",
                x => Trace.Zip(Medium.Wave, x[0].As(Medium.Wave), x[1].As(Medium.Wave), (a, b) => (a + b) * 0.5f));

            // ---- Mixing: whole signals of any kind ------------------------------------

            Whole("GATE", Medium.Wave, 2, true, "lets the top signal through while the bottom one is on, and sends nothing while it's off (0, a flat line or an empty circle)", "top, while bottom is on",
                x =>
                {
                    var gate = x[1].As(Medium.Binary);
                    var top = x[0];
                    float off = Off(top.Medium);
                    var levels = new float[Trace.Steps];
                    for (int k = 0; k < Trace.Steps; k++) levels[k] = gate[k] == 1f ? top[k] : off;
                    return new Trace(top.Medium, levels);
                });

            Whole("COUNT", Medium.Decimal, 1, true, "counts how many times its input has switched on so far this loop, up to 9", "counts the switch-ons",
                x =>
                {
                    var bits = x[0].As(Medium.Binary);
                    var levels = new float[Trace.Steps];
                    int count = 0;
                    for (int k = 0; k < Trace.Steps; k++)
                    {
                        bool rise = bits[k] == 1f && (k == 0 || bits[k - 1] == 0f);
                        if (rise && count < 9) count++;
                        levels[k] = count;
                    }
                    return new Trace(Medium.Decimal, levels);
                });

            Whole("COMPARE", Medium.Binary, 2, true, "1 while the top signal is higher than the bottom one, both read as waves; otherwise 0", "1 while top is higher",
                x => Trace.Zip(Medium.Binary, x[0].As(Medium.Wave), x[1].As(Medium.Wave), (a, b) => a > b + Trace.Tolerance ? 1f : 0f));

            // ---- Set aside: stepped waves ---------------------------------------------

            Whole("STEPS", Medium.Sampled, 1, true, "reads it as a wave 12 times a loop and holds each reading, rounded (a stepped wave)", "12 held steps a loop",
                x => SampleOf(x[0], 12));
        }

        /// <summary>What a signal of this kind is while it's off: 0 for numbers and waves, a hollow circle for shapes.</summary>
        static float Off(Medium m) => m == Medium.Shape ? Shapes.Make(Shapes.Circle, false) : 0f;

        /// <summary>The signal read as a wave <paramref name="times"/> times a loop, at the middle of each stretch, each reading rounded and held.</summary>
        static Trace SampleOf(Trace t, int times)
        {
            var wave = t.As(Medium.Wave);
            int each = Trace.Steps / times;
            var levels = new float[Trace.Steps];
            for (int j = 0; j < times; j++)
            {
                float reading = Sampled.RoundLevel(wave[j * each + each / 2]);
                for (int k = j * each; k < (j + 1) * each; k++) levels[k] = reading;
            }
            return new Trace(Medium.Sampled, levels);
        }

        static void Value(string id, Medium medium, int inputs, string rule, string shortRule, Func<int[], int> f) =>
            Register(new BlockKind(id, medium, inputs, rule, shortRule, f));

        static void Whole(string id, Medium medium, int inputs, bool mixing, string rule, string shortRule, Func<Trace[], Trace> f) =>
            Register(new BlockKind(id, medium, inputs, mixing, rule, shortRule, f));

        static void Register(BlockKind kind)
        {
            s_Kinds.Add(kind.Id, kind);
            s_Ordered.Add(kind);
        }

        public static IReadOnlyList<BlockKind> All => s_Ordered;

        /// <summary>
        /// Kinds set aside for now (11 Nov 2026): STEPS, which turns any signal into
        /// a stepped wave. Its code and tests stay. The old stepped-wave blocks went
        /// when every block got one fixed rule, so Grain (kept in Puzzles/Shelved)
        /// needs new blocks before it can come back.
        /// </summary>
        public static readonly HashSet<string> Shelved = new HashSet<string> { "STEPS" };

        /// <summary>Signal kinds set aside for now, with <see cref="Shelved"/>: stepped (sampled) waves.</summary>
        public static bool MediumShelved(Medium m) => m == Medium.Sampled;

        public static bool TryGet(string id, out BlockKind kind) => s_Kinds.TryGetValue(id ?? string.Empty, out kind);

        public static BlockKind Get(string id)
        {
            if (TryGet(id, out var kind)) return kind;
            throw new KeyNotFoundException($"Unknown block kind '{id}'.");
        }
    }
}
