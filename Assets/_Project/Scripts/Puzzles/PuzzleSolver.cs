using System;
using System.Collections.Generic;
using System.Linq;
using Blackbox.Circuits;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    /// <summary>One way to solve a circuit: every input value and block state.</summary>
    public sealed class Solution
    {
        public int[] Inputs;
        public int[] States;
        /// <summary>How many inputs and blocks differ from where the circuit starts.</summary>
        public int Changes;
    }

    public sealed class PuzzleReport
    {
        public PuzzleDef Puzzle;
        /// <summary>How many combinations of input values and block states the player could try.</summary>
        public long SearchSpace;
        public int SolutionCount;
        public readonly List<Solution> Solutions = new List<Solution>();
        /// <summary>The fewest inputs and blocks that must change from the start to solve it.</summary>
        public int FewestChanges = -1;
        /// <summary>Targets already matching when the circuit opens.</summary>
        public int StartMatched;
        /// <summary>Problems that make the puzzle broken.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Things worth a second look, but not necessarily wrong.</summary>
        public readonly List<string> Warnings = new List<string>();

        public bool Ok => Errors.Count == 0;

        public string Describe(Solution s)
        {
            var p = Puzzle;
            var parts = new List<string>();
            for (int i = 0; i < p.Inputs.Count; i++)
                if (p.Inputs[i].Adjustable) parts.Add($"{p.Inputs[i].Id}={Media.Describe(p.Medium, s.Inputs[i])}");
            for (int b = 0; b < p.Blocks.Count; b++)
            {
                var block = p.Blocks[b];
                parts.Add($"{block.Id}={block.Kind.Spoilers[s.States[b]]}{(block.Locked ? " (locked)" : "")}");
            }
            return string.Join(", ", parts);
        }

        public override string ToString()
        {
            var lines = new List<string>
            {
                $"{Puzzle.Id} \"{Puzzle.Name}\": {SolutionCount} solution(s) in {SearchSpace} combinations, fewest changes {FewestChanges}, {StartMatched}/{Puzzle.Targets.Count} targets matched at start",
            };
            foreach (var s in Solutions.Take(4)) lines.Add($"  solution ({s.Changes} changes): " + Describe(s));
            foreach (var e in Errors) lines.Add("  ERROR: " + e);
            foreach (var w in Warnings) lines.Add("  warning: " + w);
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Checks circuits by brute force: tries every combination of the inputs and
    /// blocks the player can change. Every shipped circuit must have a target, at
    /// least one solution, and must not start solved. Used by the edit mode tests,
    /// the editor menu and the design tool.
    /// </summary>
    public static class PuzzleSolver
    {
        public const long MaxSearch = 20_000_000;

        public static PuzzleReport Analyse(PuzzleDef p, int keepSolutions = 16)
        {
            var report = new PuzzleReport { Puzzle = p };

            CompiledCircuit circuit;
            try { circuit = new CompiledCircuit(p); }
            catch (CircuitException e)
            {
                report.Errors.Add(e.Message);
                return report;
            }

            if (p.Targets.Count == 0) report.Errors.Add("has no targets, so it can never be solved.");

            var start = new PuzzleSession(p);
            report.StartMatched = start.MatchedTargets;
            if (start.Solved) report.Errors.Add("is solved before the player touches it.");

            // The dials the player can turn: (is input, index, how many values).
            var dials = new List<(bool input, int index, int count)>();
            for (int i = 0; i < p.Inputs.Count; i++)
                if (p.InputChangeable(i)) dials.Add((true, i, p.Inputs[i].ChoiceCount(p.Medium)));
            for (int b = 0; b < p.Blocks.Count; b++)
                if (p.BlockChangeable(b)) dials.Add((false, b, p.Blocks[b].Kind.StateCount));

            long space = 1;
            foreach (var d in dials) space *= d.count;
            report.SearchSpace = space;
            if (space > MaxSearch)
            {
                report.Errors.Add($"has {space} combinations, too many to check (limit {MaxSearch}).");
                return report;
            }

            var inputs = p.StartInputs();
            var states = p.StartStates();
            var startInputs = p.StartInputs();
            var startStates = p.StartStates();
            var values = new int[circuit.NodeCount];
            var targetNodes = p.Targets.Select(t => circuit.NodeOf(t.Node)).ToArray();
            var wants = p.Targets.Select(t => t.Want).ToArray();
            var digits = new int[dials.Count];

            for (long combo = 0; combo < space; combo++)
            {
                for (int k = 0; k < dials.Count; k++)
                {
                    var d = dials[k];
                    if (d.input) inputs[d.index] = p.Inputs[d.index].Choice(digits[k]);
                    else states[d.index] = digits[k];
                }

                circuit.Evaluate(inputs, states, values);
                bool ok = targetNodes.Length > 0;
                for (int t = 0; t < targetNodes.Length && ok; t++) ok = Media.Same(p.Medium, values[targetNodes[t]], wants[t]);
                if (ok)
                {
                    report.SolutionCount++;
                    int changes = 0;
                    for (int i = 0; i < inputs.Length; i++) if (inputs[i] != startInputs[i]) changes++;
                    for (int b = 0; b < states.Length; b++) if (states[b] != startStates[b]) changes++;
                    if (report.FewestChanges < 0 || changes < report.FewestChanges) report.FewestChanges = changes;
                    if (report.Solutions.Count < keepSolutions)
                        report.Solutions.Add(new Solution { Inputs = (int[])inputs.Clone(), States = (int[])states.Clone(), Changes = changes });
                }

                // Count up like an odometer.
                for (int k = 0; k < dials.Count; k++)
                {
                    if (++digits[k] < dials[k].count) break;
                    digits[k] = 0;
                }
            }
            report.Solutions.Sort((a, b) => a.Changes.CompareTo(b.Changes));

            if (report.SolutionCount == 0 && p.Targets.Count > 0) report.Errors.Add("has no solution.");

            if (p.Probes > p.Wires.Count) report.Warnings.Add($"allows {p.Probes} probes but has only {p.Wires.Count} wires.");
            foreach (var t in p.Targets)
                if (t.Node is InputDef input && !input.Adjustable && !Media.Same(p.Medium, input.Value, t.Want))
                    report.Errors.Add($"input '{input.Id}' is fixed at {Media.Describe(p.Medium, input.Value)} but its target is {Media.Describe(p.Medium, t.Want)}.");
            foreach (var b in p.Blocks)
                if (b.Locked && b.Kind.StateCount == 1) report.Warnings.Add($"block '{b.Id}' is locked but only has one state anyway.");

            return report;
        }
    }
}
