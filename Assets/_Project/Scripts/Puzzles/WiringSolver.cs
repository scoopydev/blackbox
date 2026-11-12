using System;
using System.Collections.Generic;
using System.Text;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    /// <summary>What the editor validator and the design tool learn about a wired circuit.</summary>
    public sealed class WiringAnalysis
    {
        public PuzzleDef Puzzle;
        /// <summary>The designer's wiring solves the circuit.</summary>
        public bool SolutionWorks;
        /// <summary>The circuit isn't solved before anything is wired (it never is: outputs start empty).</summary>
        public bool StartsUnsolved;
        /// <summary>Ways to wire the blocks' inputs, before outputs are chosen.</summary>
        public long Space;
        /// <summary>False when the circuit was too big to search, so <see cref="Solutions"/> is unknown.</summary>
        public bool Searched;
        /// <summary>Every wiring that solves the circuit.</summary>
        public long Solutions;
        /// <summary>A few of them, as wire lists.</summary>
        public readonly List<string> Examples = new List<string>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{Puzzle.Id} {Puzzle.Name}: solution {(SolutionWorks ? "works" : "FAILS")}");
            if (!Searched) sb.Append($", {Space} block wirings: too many to search");
            else sb.Append($", {Solutions} solving wiring(s) out of {Space} block wirings");
            foreach (var e in Examples) sb.Append("\n    ").Append(e);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Searches every way to wire a wired circuit and counts those that solve it.
    /// Each block input takes any input or other block, without loops; then each
    /// output takes any signal its device reads the way its target wants, and the
    /// wiring counts if every block ends up leading to an output. Inputs may go unused.
    /// An optional block is either left out, with no wires in, or wired and leading
    /// to an output: wired but going nowhere is the same answer as left out.
    /// Swapping the wires into a block that treats its inputs alike (AND, MIX, the
    /// two bounds of BETWEEN ...) is the same answer, so those are tried in one order only.
    /// </summary>
    public static class WiringSolver
    {
        public static WiringAnalysis Analyse(PuzzleDef p, long maxSpace = 5_000_000, int keep = 6)
        {
            var result = new WiringAnalysis { Puzzle = p };
            var session = new WiringSession(p);
            result.StartsUnsolved = !session.Check().Solved;
            result.SolutionWorks = WiringSession.Check(p, DriversOf(p, p.Solution)).Solved;

            int inputs = p.Inputs.Count, blocks = p.Blocks.Count, outputs = p.Outputs.Count;
            int sources = inputs + blocks;
            var ports = new List<(int block, int port)>();
            for (int b = 0; b < blocks; b++)
                for (int k = 0; k < p.Blocks[b].Kind.InputCount; k++) ports.Add((b, k));
            // An optional block adds one more way to wire it: leaving it out.
            long space = 1;
            for (int b = 0; b < blocks && space <= long.MaxValue / 1024; b++)
            {
                long ways = 1;
                for (int k = 0; k < p.Blocks[b].Kind.InputCount && ways <= long.MaxValue / 1024; k++) ways *= sources - 1;
                if (p.Blocks[b].Optional) ways++;
                space = ways > long.MaxValue / 1024 / space ? long.MaxValue / 1024 : space * ways;
            }
            result.Space = space;
            if (space > maxSpace) return result;
            result.Searched = true;

            var driver = session.Drivers;
            var ids = new Dictionary<string, int>();
            var traces = new List<Trace>();
            int Intern(Trace t)
            {
                if (t == null) return -1;
                if (ids.TryGetValue(t.Key, out int id)) return id;
                id = traces.Count;
                traces.Add(t);
                ids[t.Key] = id;
                return id;
            }
            var inputIds = new int[inputs];
            for (int i = 0; i < inputs; i++) inputIds[i] = Intern(p.Inputs[i].Signal.Trace);
            var blockMemo = new Dictionary<string, int>();
            var matchMemo = new Dictionary<(int output, int trace), bool>();
            var value = new int[sources];

            bool Reaches(int source, int target)
            {
                if (source == target) return true;
                if (target < inputs) return false;
                foreach (int d in driver[target]) if (d >= 0 && Reaches(source, d)) return true;
                return false;
            }

            int Value(int node, bool[] done)
            {
                if (node < inputs) return inputIds[node];
                if (done[node]) return value[node];
                int b = node - inputs;
                var key = new StringBuilder().Append(b);
                var ins = driver[node];
                var traceIns = new Trace[ins.Length];
                bool fed = true;
                for (int k = 0; k < ins.Length; k++)
                {
                    int id = ins[k] < 0 ? -1 : Value(ins[k], done);
                    key.Append(',').Append(id);
                    if (id < 0) fed = false;
                    else traceIns[k] = traces[id];
                }
                string memoKey = key.ToString();
                if (!fed)
                {
                    value[node] = -1;
                    done[node] = true;
                    return -1;
                }
                if (!blockMemo.TryGetValue(memoKey, out int result))
                {
                    var block = p.Blocks[b];
                    result = Intern(block.Kind.Apply(traceIns));
                    blockMemo[memoKey] = result;
                }
                value[node] = result;
                done[node] = true;
                return result;
            }

            bool Matches(int output, int traceId)
            {
                if (traceId < 0) return false;
                if (matchMemo.TryGetValue((output, traceId), out bool m)) return m;
                var o = p.Outputs[output];
                m = Devices.Same(o.Device, traces[traceId], o.Want);
                matchMemo[(output, traceId)] = m;
                return m;
            }

            var consumed = new bool[blocks];
            var candidates = new List<int>[outputs];
            for (int o = 0; o < outputs; o++) candidates[o] = new List<int>();
            var choice = new int[outputs];

            void Leaf()
            {
                Array.Clear(consumed, 0, blocks);
                for (int b = 0; b < blocks; b++)
                    foreach (int d in driver[inputs + b])
                        if (d >= inputs) consumed[d - inputs] = true;
                // Every block no other block reads has to go straight to an output,
                // apart from an optional block left out.
                int dangling = 0;
                for (int b = 0; b < blocks; b++) if (!consumed[b] && !Off(b)) dangling++;
                if (dangling > outputs) return;

                var done = new bool[sources];
                for (int n = inputs; n < sources; n++) Value(n, done);
                for (int o = 0; o < outputs; o++)
                {
                    candidates[o].Clear();
                    for (int s = 0; s < sources; s++)
                    {
                        int id = s < inputs ? inputIds[s] : value[s];
                        if (Matches(o, id)) candidates[o].Add(s);
                    }
                    if (candidates[o].Count == 0) return;
                }
                Choose(0);
            }

            void Choose(int o)
            {
                if (o == outputs)
                {
                    for (int k = 0; k < outputs; k++) driver[sources + k][0] = choice[k];
                    if (WiringSession.AllBlocksUsed(p, driver) && OptionalUsedOrOff())
                    {
                        result.Solutions++;
                        if (result.Examples.Count < keep) result.Examples.Add(Describe(session, driver));
                    }
                    for (int k = 0; k < outputs; k++) driver[sources + k][0] = -1;
                    return;
                }
                foreach (int s in candidates[o])
                {
                    choice[o] = s;
                    Choose(o + 1);
                }
            }

            // An optional block with no wires in is left out of the circuit.
            bool Off(int b) => p.Blocks[b].Optional && driver[inputs + b][0] < 0;

            bool OptionalUsedOrOff()
            {
                bool any = false;
                for (int b = 0; b < blocks && !any; b++) any = p.Blocks[b].Optional && !Off(b);
                if (!any) return true;
                var used = WiringSession.Used(p, driver);
                for (int b = 0; b < blocks; b++)
                    if (p.Blocks[b].Optional && !Off(b) && !used[inputs + b]) return false;
                return true;
            }

            void Assign(int i)
            {
                if (i == ports.Count)
                {
                    Leaf();
                    return;
                }
                var (b, k) = ports[i];
                int node = inputs + b;
                int count = p.Blocks[b].Kind.InputCount;
                // An optional block can be left out: no wires in at all.
                if (k == 0 && p.Blocks[b].Optional) Assign(i + count);
                int alike = p.Blocks[b].Kind.AlikeFrom;
                for (int s = 0; s < sources; s++)
                {
                    if (s == node) continue;
                    if (s >= inputs && Reaches(node, s)) continue;
                    if (alike >= 0 && k > alike && s < driver[node][k - 1]) continue;
                    driver[node][k] = s;
                    Assign(i + 1);
                    driver[node][k] = -1;
                }
            }

            Assign(0);
            return result;
        }

        static string Describe(WiringSession s, int[][] driver)
        {
            var parts = new List<string>();
            for (int n = 0; n < driver.Length; n++)
                for (int k = 0; k < driver[n].Length; k++)
                    if (driver[n][k] >= 0) parts.Add(s.LinkText(driver[n][k], n, k));
            return string.Join(", ", parts);
        }

        static int[][] DriversOf(PuzzleDef p, List<WireDef> wires)
        {
            var s = new WiringSession(p);
            var driver = s.Drivers;
            foreach (var w in wires) driver[s.NodeOf(w.To)][w.ToPort] = s.NodeOf(w.From);
            return driver;
        }
    }
}
