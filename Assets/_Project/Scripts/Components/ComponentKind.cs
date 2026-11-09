using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// A family of boxes that share one hidden rule. Every box of a kind behaves the
    /// same way in every circuit, which is what lets the player carry knowledge from
    /// one puzzle to the next. The player only ever sees the kind's code (for example
    /// "K4"), its ports and its setting dots, never the rule itself.
    /// </summary>
    public sealed class ComponentKind
    {
        readonly Func<int, IBoxLogic> _factory;

        public string Id { get; }
        public int InputCount { get; }
        public int SettingCount { get; }

        /// <summary>
        /// What each setting does, for designers, tests and the editor validator.
        /// Never shown in game: understanding the rule is the puzzle.
        /// </summary>
        public IReadOnlyList<string> Spoilers { get; }

        public ComponentKind(string id, int inputCount, string[] spoilers, Func<int, IBoxLogic> factory)
        {
            Id = id;
            InputCount = inputCount;
            SettingCount = spoilers.Length;
            Spoilers = spoilers;
            _factory = factory;
        }

        public IBoxLogic CreateLogic(int setting)
        {
            if (setting < 0 || setting >= SettingCount)
                throw new ArgumentOutOfRangeException(nameof(setting), $"{Id} has {SettingCount} settings, not {setting + 1}.");
            return _factory(setting);
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Every component kind in the game. Kinds are code, not data, so their behaviour
    /// is type-checked and unit tested. Puzzle files refer to them by id.
    ///
    /// Settings are listed in the order the player cycles through them. Where a kind
    /// has a natural order (a delay of 1, 2, 3 ticks) the dots follow it, so a careful
    /// player can notice the pattern. Where it doesn't, the order is deliberately mixed.
    /// </summary>
    public static class KindCatalog
    {
        static readonly Dictionary<string, ComponentKind> s_Kinds = new Dictionary<string, ComponentKind>();
        static readonly List<ComponentKind> s_Ordered = new List<ComponentKind>();

        static KindCatalog()
        {
            // ---- Chapter I · First Light: logic, one tick at a time -------------------

            // One input, two settings: invert, pass.
            Table("K1", 1, ("10", "invert"), ("01", "pass"));

            // One input, four settings: always on, invert, always off, pass.
            Table("K2", 1, ("11", "always 1"), ("10", "invert"), ("00", "always 0"), ("01", "pass"));

            // Two inputs, two settings: or, and.
            Table("K3", 2, ("0111", "or"), ("0001", "and"));

            // Two inputs, four settings: xor, and, top-and-not-bottom, or.
            Table("K4", 2, ("0110", "xor"), ("0001", "and"), ("0010", "top and not bottom"), ("0111", "or"));

            // Three inputs: on when at least N inputs are on, N = setting + 1.
            Add(new ComponentKind("K5", 3, new[] { "at least 1 on", "at least 2 on", "all 3 on" },
                s => new ThresholdLogic(s + 1)));

            // ---- Chapter II · Afterglow: boxes that remember -------------------------

            // Repeats its input N ticks later, N = setting + 1. The only registered kind,
            // so it is the one part that may close a loop.
            Add(new ComponentKind("K6", 1, new[] { "delay 1 tick", "delay 2 ticks", "delay 3 ticks" },
                s => new DelayLogic(s + 1)));

            // Compares now with the tick before: any change, rising, falling.
            Add(new ComponentKind("K7", 1, new[] { "on when input changes", "on when input rises", "on when input falls" },
                s => new EdgeLogic(s == 0 ? EdgeMode.Change : s == 1 ? EdgeMode.Rising : EdgeMode.Falling)));

            // A bit that flips: on every tick the input is on, or only when the input rises.
            Add(new ComponentKind("K8", 1, new[] { "flips every tick the input is on", "flips when the input rises" },
                s => new ToggleLogic(s == 0 ? ToggleMode.WhileOn : ToggleMode.OnRise)));

            // A bit that is set by one port and cleared by the other.
            Add(new ComponentKind("K9", 2, new[] { "top sets, bottom clears", "top clears, bottom sets" },
                s => new LatchLogic(s == 1)));
        }

        static void Table(string id, int inputs, params (string table, string spoiler)[] settings)
        {
            var spoilers = new string[settings.Length];
            var tables = new string[settings.Length];
            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i].table.Length != 1 << inputs)
                    throw new InvalidOperationException($"{id}: truth table '{settings[i].table}' needs {1 << inputs} entries.");
                tables[i] = settings[i].table;
                spoilers[i] = settings[i].spoiler;
            }
            Add(new ComponentKind(id, inputs, spoilers, s => new TruthTableLogic(tables[s])));
        }

        static void Add(ComponentKind kind)
        {
            s_Kinds.Add(kind.Id, kind);
            s_Ordered.Add(kind);
        }

        public static IReadOnlyList<ComponentKind> All => s_Ordered;

        public static bool TryGet(string id, out ComponentKind kind) => s_Kinds.TryGetValue(id ?? string.Empty, out kind);

        public static ComponentKind Get(string id)
        {
            if (TryGet(id, out var kind)) return kind;
            throw new KeyNotFoundException($"Unknown component kind '{id}'.");
        }
    }
}
