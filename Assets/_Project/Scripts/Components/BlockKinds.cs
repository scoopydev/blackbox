using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// A kind of block: one fixed rule, the same in every circuit, which is what
    /// lets the player carry what they learn from one circuit to the next. A block
    /// has no settings, and sends one kind of signal, always (<see cref="Medium"/>).
    /// The player sees only its label, which says that kind (B1 for a bit block,
    /// D1 for a digit block, W1 for a wave block), and its ports, never its kind
    /// or its rule, until its circuit is solved.
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

        /// <summary>The rule in a few plain words (at most 45 characters, two short lines), written under a block once its circuit is solved.</summary>
        public string ShortRule { get; }

        /// <summary>
        /// Set aside for now: the kind still works, but no circuit may use it.
        /// See <see cref="KindCatalog.Shelved"/>.
        /// </summary>
        public bool Shelved => KindCatalog.Shelved.Contains(Id);

        /// <summary>
        /// The first input from which the block treats its inputs alike, so swapping
        /// two of those wires changes nothing: 0 for AND, MAX, MIX ..., 1 for BETWEEN,
        /// whose two bounds can come either way round; -1 when every input has its own
        /// job. The solver counts such a swap as the same answer.
        /// </summary>
        public int AlikeFrom => KindCatalog.AlikeFrom.TryGetValue(Id, out int from) ? from : -1;

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

        Medium?[] _reads;

        /// <summary>
        /// The kind of signal input <paramref name="port"/> is read as, or null if
        /// the block takes it as it is (GATE's top input). Bit, digit, shape and
        /// wave blocks read every input as their own kind.
        /// </summary>
        public Medium? Reads(int port)
        {
            if (_reads != null) return port >= 0 && port < _reads.Length ? _reads[port] : null;
            return Mixing ? (Medium?)null : Medium;
        }

        internal BlockKind ReadingAs(params Medium?[] reads)
        {
            _reads = reads;
            return this;
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
    /// Four blocks with three inputs (MAJORITY, MIDDLE, BETWEEN, SWITCH) come late
    /// in Part Three.
    /// </summary>
    public static class KindCatalog
    {
        static readonly Dictionary<string, BlockKind> s_Kinds = new Dictionary<string, BlockKind>();
        static readonly List<BlockKind> s_Ordered = new List<BlockKind>();

        static KindCatalog()
        {
            // ---- Bits: 0 and 1 ------------------------------------------------------

            Value("NOT", Medium.Binary, 1, "flips a bit: 1 becomes 0 and 0 becomes 1", "swaps 0 and 1",
                x => 1 - x[0]);
            Value("AND", Medium.Binary, 2, "1 only while both inputs are 1", "1 only when both inputs are 1",
                x => x[0] & x[1]);
            Value("OR", Medium.Binary, 2, "1 while either input is 1", "1 when either input is 1",
                x => x[0] | x[1]);
            Value("XOR", Medium.Binary, 2, "1 while the two inputs are different", "1 when the two inputs differ",
                x => x[0] ^ x[1]);

            // ---- Digits: 0 to 9. No rule can go below 0 or past 9 ------------------

            Value("MIRROR", Medium.Decimal, 1, "turns a digit around: 9 minus the digit, so 0 and 9 swap, 1 and 8, 2 and 7", "9 minus the digit: 2 becomes 7",
                x => 9 - x[0]);
            Value("MAX", Medium.Decimal, 2, "the larger of the two digits", "the larger of the two digits",
                x => Math.Max(x[0], x[1]));
            Value("MIN", Medium.Decimal, 2, "the smaller of the two digits", "the smaller of the two digits",
                x => Math.Min(x[0], x[1]));
            Value("DIFF", Medium.Decimal, 2, "how far apart the two digits are: the larger minus the smaller", "the larger digit minus the smaller",
                x => Math.Abs(x[0] - x[1]));

            // ---- Shapes: a form, a fill and a colour ---------------------------------

            Value("FILL", Medium.Shape, 1, "flips a shape's fill: hollow becomes solid and solid becomes hollow", "swaps solid and hollow",
                x => x[0] ^ 1);
            Value("NEXT", Medium.Shape, 1, "the next shape along: a circle becomes a triangle, a triangle a square, a square a hexagon, and a hexagon a circle again; the fill and colour stay the same", "next shape: circle, triangle, square, hexagon",
                x => Shapes.Make(Shapes.Form(x[0]) + 1, Shapes.Solid(x[0]), Shapes.Colour(x[0])));
            Value("TAKEFILL", Medium.Shape, 2, "the top shape, solid or hollow like the bottom shape", "top shape, filled like the bottom one",
                x => Shapes.Make(Shapes.Form(x[0]), Shapes.Solid(x[1]), Shapes.Colour(x[0])));
            Value("TAKECOLOUR", Medium.Shape, 2, "the top shape, in the bottom shape's colour", "top shape, coloured like the bottom one",
                x => Shapes.Make(Shapes.Form(x[0]), Shapes.Solid(x[0]), Shapes.Colour(x[1])));

            // ---- Waves: every input read as a wave, a wave sent ----------------------

            Whole("FLIP", Medium.Wave, 1, false, "turns a wave upside down", "turns the wave upside down",
                x => x[0].As(Medium.Wave).Map(Medium.Wave, v => v == 0f ? 0f : -v));
            Whole("FASTER", Medium.Wave, 1, false, "plays the wave twice as fast, so it repeats twice as often", "plays the wave twice as fast",
                x => x[0].As(Medium.Wave).Faster(2));
            Whole("LATER", Medium.Wave, 1, false, "plays the wave a quarter of the loop later", "delays the wave by a quarter loop",
                x => x[0].As(Medium.Wave).Later(Trace.Steps / 4));
            Whole("MIX", Medium.Wave, 2, false, "halfway between the two waves at every moment, so a wave and its upside-down twin mix to a flat line", "halfway between the two waves",
                x => Trace.Zip(Medium.Wave, x[0].As(Medium.Wave), x[1].As(Medium.Wave), (a, b) => (a + b) * 0.5f));

            // ---- Mixing: whole signals of any kind ------------------------------------

            // GATE and SWITCH are wave blocks: whatever comes in, a wave comes out.
            Whole("GATE", Medium.Wave, 2, true, "lets the top signal through, read as a wave, while the bottom one is on, and sends a flat line while it's off", "lets the top through while the bottom is on",
                x =>
                {
                    var gate = x[1].As(Medium.Binary);
                    var top = x[0].As(Medium.Wave);
                    var levels = new float[Trace.Steps];
                    for (int k = 0; k < Trace.Steps; k++) levels[k] = gate[k] == 1f ? top[k] : 0f;
                    return new Trace(Medium.Wave, levels);
                });

            s_Kinds["GATE"].ReadingAs(Medium.Wave, Medium.Binary);

            Whole("COUNT", Medium.Decimal, 1, true, "counts how many times its input has switched on so far this loop, up to 9", "counts each switch-on, up to 9",
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

            s_Kinds["COUNT"].ReadingAs(Medium.Binary);

            Whole("COMPARE", Medium.Binary, 2, true, "1 while the top signal is higher than the bottom one, both read as waves; otherwise 0", "1 while the top is higher than the bottom",
                x => Trace.Zip(Medium.Binary, x[0].As(Medium.Wave), x[1].As(Medium.Wave), (a, b) => a > b + Trace.Tolerance ? 1f : 0f));

            s_Kinds["COMPARE"].ReadingAs(Medium.Wave, Medium.Wave);

            // ---- Three inputs: met late in Part Three ---------------------------------

            Value("MAJORITY", Medium.Binary, 3, "1 while at least two of its three inputs are 1", "1 when at least two inputs are 1",
                x => x[0] + x[1] + x[2] >= 2 ? 1 : 0);
            Value("MIDDLE", Medium.Decimal, 3, "the middle one of the three digits: neither the largest nor the smallest", "the middle one of the three digits",
                x => Math.Max(Math.Min(x[0], x[1]), Math.Min(Math.Max(x[0], x[1]), x[2])));

            Whole("BETWEEN", Medium.Binary, 3, true, "1 while the top signal is between the other two, all read as waves; otherwise 0", "1 while the top is between the other two",
                x =>
                {
                    var a = x[0].As(Medium.Wave);
                    var b = x[1].As(Medium.Wave);
                    var c = x[2].As(Medium.Wave);
                    var levels = new float[Trace.Steps];
                    for (int k = 0; k < Trace.Steps; k++)
                    {
                        float lo = Math.Min(b[k], c[k]), hi = Math.Max(b[k], c[k]);
                        levels[k] = a[k] > lo + Trace.Tolerance && a[k] < hi - Trace.Tolerance ? 1f : 0f;
                    }
                    return new Trace(Medium.Binary, levels);
                });

            s_Kinds["BETWEEN"].ReadingAs(Medium.Wave, Medium.Wave, Medium.Wave);

            Whole("SWITCH", Medium.Wave, 3, true, "sends the top signal while the bottom one is on, and the middle one while it's off, both read as waves", "top while the bottom is on, else the middle",
                x =>
                {
                    var top = x[0].As(Medium.Wave);
                    var middle = x[1].As(Medium.Wave);
                    var on = x[2].As(Medium.Binary);
                    var levels = new float[Trace.Steps];
                    for (int k = 0; k < Trace.Steps; k++) levels[k] = on[k] == 1f ? top[k] : middle[k];
                    return new Trace(Medium.Wave, levels);
                });

            s_Kinds["SWITCH"].ReadingAs(Medium.Wave, Medium.Wave, Medium.Binary);

            // ---- Set aside: stepped waves ---------------------------------------------

            Whole("STEPS", Medium.Sampled, 1, true, "reads it as a wave 12 times a loop and holds each reading, rounded (a stepped wave)", "12 held steps a loop",
                x => SampleOf(x[0], 12));
            s_Kinds["STEPS"].ReadingAs(Medium.Wave);
        }

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

        /// <summary>Kinds whose inputs, from the given one on, can be swapped without changing what they send.</summary>
        public static readonly Dictionary<string, int> AlikeFrom = new Dictionary<string, int>
        {
            ["AND"] = 0, ["OR"] = 0, ["XOR"] = 0, ["MAX"] = 0, ["MIN"] = 0, ["DIFF"] = 0, ["MIX"] = 0,
            ["MAJORITY"] = 0, ["MIDDLE"] = 0, ["BETWEEN"] = 1,
        };

        /// <summary>
        /// Kinds set aside for now. Their code and tests stay, and the loader
        /// refuses a circuit that uses one.
        /// <list type="bullet">
        /// <item>STEPS, which turns any signal into a stepped wave (11 Nov 2026,
        /// with stepped waves). The old stepped-wave blocks went when every block
        /// got one fixed rule, so Grain (kept in Puzzles/Shelved) needs new blocks
        /// before it can come back.</item>
        /// <item>The shape blocks (11 Nov 2026, when shapes were set aside and every
        /// signal became a waveform: bits, digits and waves).</item>
        /// </list>
        /// </summary>
        public static readonly HashSet<string> Shelved = new HashSet<string>
        {
            "STEPS",
            "FILL", "NEXT", "TAKEFILL", "TAKECOLOUR",
        };

        /// <summary>
        /// Signal kinds set aside for now, with <see cref="Shelved"/>: stepped
        /// (sampled) waves and shapes. The game is bits, digits and waves.
        /// </summary>
        public static bool MediumShelved(Medium m) => m == Medium.Sampled || m == Medium.Shape;

        public static bool TryGet(string id, out BlockKind kind) => s_Kinds.TryGetValue(id ?? string.Empty, out kind);

        public static BlockKind Get(string id)
        {
            if (TryGet(id, out var kind)) return kind;
            throw new KeyNotFoundException($"Unknown block kind '{id}'.");
        }
    }
}
