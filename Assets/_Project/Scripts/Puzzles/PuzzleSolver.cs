using System;
using System.Collections.Generic;
using System.Linq;
using Blackbox.Circuits;

namespace Blackbox.Puzzles
{
    public sealed class PuzzleReport
    {
        public PuzzleDef Puzzle;
        /// <summary>How many setting combinations the player could try.</summary>
        public long SearchSpace;
        public int SolutionCount;
        public readonly List<int[]> Solutions = new List<int[]>();
        /// <summary>Problems that make the puzzle broken.</summary>
        public readonly List<string> Errors = new List<string>();
        /// <summary>Things worth a second look, but not necessarily wrong.</summary>
        public readonly List<string> Warnings = new List<string>();

        public bool Ok => Errors.Count == 0;

        public string Describe(int[] settings)
        {
            var parts = new List<string>();
            for (int b = 0; b < Puzzle.Boxes.Count; b++)
            {
                var box = Puzzle.Boxes[b];
                parts.Add($"{box.Id}={box.Kind.Spoilers[settings[b]]}{(box.Locked ? " (locked)" : "")}");
            }
            return string.Join(", ", parts);
        }

        public override string ToString()
        {
            var lines = new List<string>
            {
                $"{Puzzle.Id} \"{Puzzle.Name}\": {SolutionCount} solution(s) in {SearchSpace} combinations",
            };
            foreach (var s in Solutions.Take(4)) lines.Add("  solution: " + Describe(s));
            foreach (var e in Errors) lines.Add("  ERROR: " + e);
            foreach (var w in Warnings) lines.Add("  warning: " + w);
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Checks puzzles by brute force: tries every combination of settings on the boxes
    /// the player can change. Every shipped puzzle must have at least one solution and
    /// must not start solved. Used by the edit mode tests and the editor menu.
    /// </summary>
    public static class PuzzleSolver
    {
        public const long MaxSearch = 5_000_000;

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

            var free = p.Boxes.Where(b => !b.Locked).ToList();
            long space = 1;
            foreach (var b in free) space *= b.Kind.SettingCount;
            report.SearchSpace = space;
            if (space > MaxSearch)
            {
                report.Errors.Add($"{space} combinations is too many to check.");
                return report;
            }

            var settings = p.StartSettings();
            if (circuit.Passes(settings)) report.Errors.Add("It is already solved at its starting settings.");

            // Count up through every combination like an odometer.
            var digits = new int[free.Count];
            for (long i = 0; i < space; i++)
            {
                for (int d = 0; d < free.Count; d++) settings[free[d].Index] = digits[d];
                if (circuit.Passes(settings))
                {
                    report.SolutionCount++;
                    if (report.Solutions.Count < keepSolutions) report.Solutions.Add((int[])settings.Clone());
                }
                for (int d = 0; d < free.Count; d++)
                {
                    if (++digits[d] < free[d].Kind.SettingCount) break;
                    digits[d] = 0;
                }
            }
            if (report.SolutionCount == 0) report.Errors.Add("No combination of settings solves it.");

            CheckStructure(p, circuit, report);
            return report;
        }

        static void CheckStructure(PuzzleDef p, CompiledCircuit circuit, PuzzleReport report)
        {
            foreach (var input in p.Inputs)
                if (!p.Wires.Any(w => w.From == input)) report.Warnings.Add($"Input {input.Id} is not wired to anything.");
            foreach (var box in p.Boxes)
                if (!p.Wires.Any(w => w.From == box)) report.Warnings.Add($"Box {box.Id} drives nothing.");

            // Inputs that never change across the cases can't teach the player anything.
            for (int i = 0; i < p.Inputs.Count; i++)
            {
                bool seen0 = false, seen1 = false;
                foreach (var cs in p.Cases)
                    for (int t = 0; t < cs.Ticks; t++)
                    {
                        if (cs.Inputs[t][i]) seen1 = true; else seen0 = true;
                    }
                if (p.Cases.Count > 1 && !(seen0 && seen1))
                    report.Warnings.Add($"Input {p.Inputs[i].Id} has the same value in every case.");
            }

            // Two identical cases are noise.
            if (p.Mode == PuzzleMode.Table)
            {
                var keys = new HashSet<string>();
                foreach (var cs in p.Cases)
                {
                    var key = string.Concat(cs.Inputs[0].Select(b => b ? '1' : '0'));
                    if (!keys.Add(key)) report.Errors.Add($"The table lists inputs {key} twice.");
                }
            }

            foreach (var w in p.Wires)
            {
                if (w.From.At.Col >= w.To.At.Col && !w.ViaRow.HasValue)
                    report.Warnings.Add($"Wire {w.From.Id} -> {w.To.Id} runs backwards without a \"via\" row; it will be routed below the board.");
            }
        }
    }
}
