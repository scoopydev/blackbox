using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// A family of blocks that share one hidden rule. Every block of a kind behaves
    /// the same way in every circuit, which is what lets the player carry what they
    /// learn from one circuit to the next. The player sees the kind's code (such as
    /// "B3"), its ports and its state dots, never the rule.
    /// </summary>
    public sealed class BlockKind
    {
        readonly Func<int[], int, int> _rule;

        public string Id { get; }
        public Medium Medium { get; }
        public int InputCount { get; }
        public int StateCount { get; }

        /// <summary>
        /// What each state does, for designers, tests and the editor validator.
        /// Never shown in game: working out the rule is the puzzle.
        /// </summary>
        public IReadOnlyList<string> Spoilers { get; }

        public BlockKind(string id, Medium medium, int inputCount, string[] spoilers, Func<int[], int, int> rule)
        {
            Id = id;
            Medium = medium;
            InputCount = inputCount;
            StateCount = spoilers.Length;
            Spoilers = spoilers;
            _rule = rule;
        }

        /// <summary>The block's output for these inputs in this state.</summary>
        public int Apply(int[] inputs, int state)
        {
            if (state < 0 || state >= StateCount)
                throw new ArgumentOutOfRangeException(nameof(state), $"{Id} has {StateCount} states, not {state + 1}.");
            return _rule(inputs, state);
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Every block kind in the game. Kinds are code, not data, so their behaviour is
    /// type checked and unit tested; puzzle files refer to them by id.
    ///
    /// The bit, digit and shape kinds follow one pattern, so a player who has learned
    /// the binary chapter has a head start on the others: a one-input block that
    /// changes its signal, one that replaces part of it, a two-input block that
    /// combines, and one that compares. The wave kinds each work one setting of a
    /// wave (frequency, form, height, phase; steps and height for sampled waves).
    /// States are listed in the order a block cycles through them.
    /// </summary>
    public static class KindCatalog
    {
        static readonly Dictionary<string, BlockKind> s_Kinds = new Dictionary<string, BlockKind>();
        static readonly List<BlockKind> s_Ordered = new List<BlockKind>();

        static KindCatalog()
        {
            // ---- Binary: 0 and 1 ----------------------------------------------------

            Add("B1", Medium.Binary, 1, new[] { "invert", "pass" }, (x, s) => s == 0 ? 1 - x[0] : x[0]);

            Add("B2", Medium.Binary, 2, new[] { "and (1 only when both are 1)", "or (1 when either is 1)" },
                (x, s) => s == 0 ? x[0] & x[1] : x[0] | x[1]);

            Add("B3", Medium.Binary, 1, new[] { "always 1", "invert", "always 0", "pass" },
                (x, s) => s == 0 ? 1 : s == 1 ? 1 - x[0] : s == 2 ? 0 : x[0]);

            Add("B4", Medium.Binary, 2, new[] { "xor (1 when they differ)", "and", "top and not bottom", "or" },
                (x, s) =>
                {
                    int a = x[0], b = x[1];
                    switch (s)
                    {
                        case 0: return a ^ b;
                        case 1: return a & b;
                        case 2: return a & (1 - b);
                        default: return a | b;
                    }
                });

            // ---- Decimal: digits that wrap round ------------------------------------

            Add("D1", Medium.Decimal, 1, new[] { "add 1", "add 2", "add 3" }, (x, s) => Wrap(x[0] + s + 1));

            Add("D2", Medium.Decimal, 2, new[] { "sum (top + bottom)", "difference (top - bottom)" },
                (x, s) => s == 0 ? Wrap(x[0] + x[1]) : Wrap(x[0] - x[1]));

            Add("D3", Medium.Decimal, 1, new[] { "double", "halve, rounding down", "mirror (9 - x)" },
                (x, s) => s == 0 ? Wrap(x[0] * 2) : s == 1 ? x[0] / 2 : 9 - x[0]);

            Add("D4", Medium.Decimal, 2, new[] { "the larger", "the smaller" },
                (x, s) => s == 0 ? Math.Max(x[0], x[1]) : Math.Min(x[0], x[1]));

            // ---- Shapes: four forms, hollow or solid --------------------------------

            Add("S1", Medium.Shape, 1, new[] { "flip fill", "pass" },
                (x, s) => s == 0 ? x[0] ^ 1 : x[0]);

            Add("S2", Medium.Shape, 1, new[] { "next form (circle, triangle, square, hexagon, circle)", "previous form" },
                (x, s) => Shapes.Make(Shapes.Form(x[0]) + (s == 0 ? 1 : -1), Shapes.Solid(x[0])));

            Add("S3", Medium.Shape, 2, new[] { "form of top, fill of bottom", "form of bottom, fill of top" },
                (x, s) => s == 0
                    ? Shapes.Make(Shapes.Form(x[0]), Shapes.Solid(x[1]))
                    : Shapes.Make(Shapes.Form(x[1]), Shapes.Solid(x[0])));

            Add("S4", Medium.Shape, 2, new[] { "the one with more corners (top on a tie)", "the one with fewer corners (top on a tie)" },
                (x, s) =>
                {
                    int a = Shapes.Corners[Shapes.Form(x[0])], b = Shapes.Corners[Shapes.Form(x[1])];
                    if (a == b) return x[0];
                    return (s == 0) == (a > b) ? x[0] : x[1];
                });

            // ---- Analog waves: form, frequency, height and phase ----------------------

            Add("W1", Medium.Wave, 1, new[] { "one step faster (the fastest wraps to the slowest)", "one step slower (the slowest wraps to the fastest)" },
                (x, s) => Waves.WithFrequency(x[0], Waves.Frequency(x[0]) + (s == 0 ? 1 : -1)));

            Add("W2", Medium.Wave, 1, new[] { "make it a square wave", "make it a sine wave", "make it a saw wave" },
                (x, s) => Waves.WithForm(x[0], s == 0 ? Waves.SquareWave : s == 1 ? Waves.Sine : Waves.Saw));

            Add("W3", Medium.Wave, 2, new[] { "top's wave at bottom's height", "bottom's wave at top's height" },
                (x, s) => s == 0
                    ? Waves.WithHeight(x[0], Waves.Height(x[1]))
                    : Waves.WithHeight(x[1], Waves.Height(x[0])));

            Add("W4", Medium.Wave, 1, new[] { "a quarter cycle later", "half a cycle later" },
                (x, s) => Waves.WithPhase(x[0], Waves.Phase(x[0]) + (s == 0 ? 1 : 2)));

            // ---- Sampled waves: an analog wave read a few times a cycle -------------

            Add("Q1", Medium.Sampled, 1, new[] { "more steps a cycle (3, 6, 12, then 3 again)", "fewer steps a cycle (12, 6, 3, then 12 again)" },
                (x, s) => Sampled.WithRate(x[0], Sampled.Rate(x[0]) + (s == 0 ? 1 : -1)));

            Add("Q2", Medium.Sampled, 1, new[] { "one level taller (7 wraps to 1)", "one level shorter (1 wraps to 7)" },
                (x, s) => Sampled.WithHeight(x[0], Sampled.Height(x[0]) + (s == 0 ? 1 : -1)));

            Add("Q3", Medium.Sampled, 2, new[] { "top's wave with bottom's steps", "bottom's wave with top's steps" },
                (x, s) => s == 0
                    ? Sampled.WithRate(x[0], Sampled.Rate(x[1]))
                    : Sampled.WithRate(x[1], Sampled.Rate(x[0])));

            Add("Q4", Medium.Sampled, 2, new[] { "the taller (top on a tie)", "the shorter (top on a tie)" },
                (x, s) =>
                {
                    int a = Sampled.Height(x[0]), b = Sampled.Height(x[1]);
                    if (a == b) return x[0];
                    return (s == 0) == (a > b) ? x[0] : x[1];
                });
        }

        static int Wrap(int v) => ((v % 10) + 10) % 10;

        static void Add(string id, Medium medium, int inputs, string[] spoilers, Func<int[], int, int> rule)
        {
            var kind = new BlockKind(id, medium, inputs, spoilers, rule);
            s_Kinds.Add(kind.Id, kind);
            s_Ordered.Add(kind);
        }

        public static IReadOnlyList<BlockKind> All => s_Ordered;

        public static bool TryGet(string id, out BlockKind kind) => s_Kinds.TryGetValue(id ?? string.Empty, out kind);

        public static BlockKind Get(string id)
        {
            if (TryGet(id, out var kind)) return kind;
            throw new KeyNotFoundException($"Unknown block kind '{id}'.");
        }
    }
}
