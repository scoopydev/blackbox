using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    /// <summary>
    /// The goal of a circuit in plain sentences, written from its targets:
    /// "Make X show 1 and Y show 0. End with A at 1." The circuit screen shows it
    /// above the board, so the player always knows what finished looks like.
    /// </summary>
    public static class GoalText
    {
        /// <summary>Above this many targets the sentence counts them instead of listing them.</summary>
        public const int MaxListed = 6;

        public static string For(PuzzleDef p)
        {
            if (!string.IsNullOrWhiteSpace(p.Goal)) return p.Goal.Trim();
            if (p.Wiring) return ForWiring(p);
            if (p.Targets.Count == 0) return "";

            var outputs = new List<TargetDef>();
            var blocks = new List<TargetDef>();
            var inputs = new List<TargetDef>();
            foreach (var t in p.Targets)
            {
                if (t.Node.Role == NodeRole.Output) outputs.Add(t);
                else if (t.Node.Role == NodeRole.Block) blocks.Add(t);
                else inputs.Add(t);
            }

            if (p.Targets.Count > MaxListed)
            {
                var kinds = new List<string>();
                if (outputs.Count > 0) kinds.Add(Count(outputs.Count, "output"));
                if (blocks.Count > 0) kinds.Add(Count(blocks.Count, "block"));
                if (inputs.Count > 0) kinds.Add(Count(inputs.Count, "input"));
                return $"Match all {p.Targets.Count} targets: {JoinAnd(kinds)}.";
            }

            var clauses = new List<string>();
            string verb = Media.Verb(p.Medium);
            if (outputs.Count > 0)
            {
                var parts = new List<string>();
                foreach (var t in outputs) parts.Add($"{t.Node.Id} {verb} {Phrase(p.Medium, t.Want)}");
                clauses.Add("make " + JoinAnd(parts));
            }
            if (blocks.Count > 0)
            {
                var parts = new List<string>();
                foreach (var t in blocks) parts.Add($"block {t.Node.Id} send {Phrase(p.Medium, t.Want)}");
                clauses.Add("make " + JoinAnd(parts));
            }
            if (inputs.Count > 0)
            {
                var keep = new List<string>();
                var set = new List<string>();
                foreach (var t in inputs)
                {
                    var input = (InputDef)t.Node;
                    string part = $"{input.Id} {(input.Value == t.Want ? "at" : "to")} {Phrase(p.Medium, t.Want)}";
                    if (input.Value == t.Want) keep.Add(part);
                    else set.Add(part);
                }
                if (set.Count > 0) clauses.Add("set " + JoinAnd(set));
                if (keep.Count > 0) clauses.Add("end with " + JoinAnd(keep));
            }

            var text = new StringBuilder();
            foreach (var clause in clauses)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(char.ToUpperInvariant(clause[0])).Append(clause, 1, clause.Length - 1).Append('.');
            }
            return text.ToString();
        }

        /// <summary>A value as it reads in a sentence: "1", "7", "a solid triangle", "a square wave at F3 A5", "a stepped saw at F2 A7 S6".</summary>
        public static string Phrase(Medium m, int v)
        {
            if (!Media.IsValid(m, v)) return "?";
            switch (m)
            {
                case Medium.Shape:
                    return "a " + Media.Describe(m, v);
                case Medium.Wave:
                    return $"a {Waves.FormNames[Waves.Form(v)]} wave at {Waves.Settings(v)}";
                case Medium.Sampled:
                    return $"a stepped {Waves.FormNames[Sampled.Form(v)]} at {Sampled.Settings(v)}";
                default:
                    return Media.Describe(m, v);
            }
        }

        /// <summary>
        /// Every rule a run broke, naming each block that broke it, then the rules
        /// those break: "Block D1 isn't used: its signal doesn't reach an output.
        /// Every block with a solid outline has to lead to an output." Null when the
        /// rules are kept. Dotted inputs and blocks may go unused; solid ones may not.
        /// </summary>
        public static string BrokenRules(PuzzleDef p, WiringReport r)
        {
            var said = new List<string>();
            string Names(List<int> list, Func<int, string> name)
            {
                var names = list.Select(name).ToList();
                return names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
            }
            if (r.EmptyBlocks.Count > 0)
                said.Add(r.EmptyBlocks.Count == 1
                    ? $"Block {Names(r.EmptyBlocks, b => p.Blocks[b].Label)} has an input with no wire, so it sends nothing."
                    : $"Blocks {Names(r.EmptyBlocks, b => p.Blocks[b].Label)} have inputs with no wire, so they send nothing.");
            if (r.UnusedBlocks.Count > 0)
                said.Add(r.UnusedBlocks.Count == 1
                    ? $"Block {Names(r.UnusedBlocks, b => p.Blocks[b].Label)} isn't used: its signal doesn't reach an output."
                    : $"Blocks {Names(r.UnusedBlocks, b => p.Blocks[b].Label)} aren't used: their signals don't reach an output.");
            if (r.UnusedInputs.Count > 0)
                said.Add(r.UnusedInputs.Count == 1
                    ? $"Input {Names(r.UnusedInputs, i => p.Inputs[i].Id)} isn't used, and its solid outline means it has to be."
                    : $"Inputs {Names(r.UnusedInputs, i => p.Inputs[i].Id)} aren't used, and their solid outlines mean they have to be.");
            if (said.Count == 0) return null;
            if (r.UnusedBlocks.Count > 0) said.Add("Every block with a solid outline has to lead to an output.");
            if (r.EmptyBlocks.Count > 0) said.Add("Every block input needs a wire.");
            return string.Join(" ", said);
        }

        /// <summary>The rules of every wired circuit, shown under its goal.</summary>
        public const string WiringRules = "MATCH ALL OUTPUTS. USE EVERYTHING WITH A SOLID OUTLINE.";

        /// <summary>
        /// "Make light X and display Y do what their targets show", or "Make displays
        /// X and Y ..." when every output is the same device. The rules go under it:
        /// <see cref="WiringRules"/>.
        /// </summary>
        static string ForWiring(PuzzleDef p)
        {
            var names = new List<string>();
            bool alike = p.Outputs.Count > 1 && p.Outputs.TrueForAll(o => o.Device == p.Outputs[0].Device);
            foreach (var o in p.Outputs) names.Add(alike ? o.Id : $"{Devices.Noun(o.Device)} {o.Id}");
            string who = p.Outputs.Count > MaxListed ? $"all {p.Outputs.Count} outputs"
                : alike ? $"{Devices.Noun(p.Outputs[0].Device)}s {JoinAnd(names)}"
                : JoinAnd(names);
            string verb = p.Outputs.Count == 1 ? "do what its target shows" : "do what their targets show";
            return $"Make {who} {verb}.";
        }

        static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

        static string JoinAnd(List<string> parts)
        {
            if (parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(i == parts.Count - 1 ? " and " : ", ");
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }
    }
}
