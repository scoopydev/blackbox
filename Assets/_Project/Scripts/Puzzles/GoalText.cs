using System.Collections.Generic;
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

        /// <summary>"Wire the circuit so light X and display Y do what their targets show. Use every block."</summary>
        static string ForWiring(PuzzleDef p)
        {
            var names = new List<string>();
            foreach (var o in p.Outputs) names.Add($"{Devices.Noun(o.Device)} {o.Id}");
            string who = p.Outputs.Count > MaxListed ? $"all {p.Outputs.Count} outputs" : JoinAnd(names);
            string verb = p.Outputs.Count == 1 ? "does what its target shows" : "do what their targets show";
            return $"Wire the circuit so {who} {verb}. Use every block, with every block input connected.";
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
