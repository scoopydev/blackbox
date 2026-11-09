using System;
using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Puzzles;

namespace Blackbox.Circuits
{
    public sealed class CircuitException : Exception
    {
        public CircuitException(string message) : base(message) { }
    }

    /// <summary>
    /// A puzzle's wiring turned into arrays the simulator can run quickly.
    ///
    /// Nodes are numbered inputs first, then boxes, then outputs. Each node has one
    /// output value per tick (an output node's value is what reached it).
    /// </summary>
    public sealed class CompiledCircuit
    {
        public PuzzleDef Puzzle { get; }
        public int InputCount { get; }
        public int BoxCount { get; }
        public int OutputCount { get; }
        public int NodeCount => InputCount + BoxCount + OutputCount;

        /// <summary>[node][port] = the node whose output drives that port. Inputs have no ports.</summary>
        public int[][] Drivers { get; }

        /// <summary>Boxes and outputs in an order where every node comes after what it reads this tick.</summary>
        public int[] Order { get; }

        /// <summary>Depth of each node from the inputs, for animating the signal front.</summary>
        public int[] Depth { get; }

        public int BoxNode(int box) => InputCount + box;
        public int OutputNode(int output) => InputCount + BoxCount + output;
        public bool IsBox(int node) => node >= InputCount && node < InputCount + BoxCount;
        public bool IsOutput(int node) => node >= InputCount + BoxCount;

        public CompiledCircuit(PuzzleDef puzzle)
        {
            Puzzle = puzzle;
            InputCount = puzzle.Inputs.Count;
            BoxCount = puzzle.Boxes.Count;
            OutputCount = puzzle.Outputs.Count;

            Drivers = new int[NodeCount][];
            for (int i = 0; i < InputCount; i++) Drivers[i] = Array.Empty<int>();
            for (int b = 0; b < BoxCount; b++) Drivers[BoxNode(b)] = Filled(puzzle.Boxes[b].Kind.InputCount);
            for (int o = 0; o < OutputCount; o++) Drivers[OutputNode(o)] = Filled(1);

            foreach (var w in puzzle.Wires) Drivers[NodeOf(w.To)][w.ToPort] = NodeOf(w.From);

            // Registered boxes (delays) answer before they hear their inputs, so edges into
            // them don't constrain the order. Every other edge does; a loop of those is a
            // circuit that can't settle and is rejected.
            var registered = new bool[NodeCount];
            for (int b = 0; b < BoxCount; b++)
                registered[BoxNode(b)] = puzzle.Boxes[b].Kind.CreateLogic(0).Registered;

            var order = new List<int>();
            var state = new byte[NodeCount]; // 0 unvisited, 1 visiting, 2 done
            for (int n = InputCount; n < NodeCount; n++) Visit(n, registered, state, order);
            Order = order.ToArray();

            Depth = new int[NodeCount];
            ComputeDepths();
        }

        static int[] Filled(int count)
        {
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = -1;
            return a;
        }

        int NodeOf(NodeDef def)
        {
            switch (def.Role)
            {
                case NodeRole.Input: return def.Index;
                case NodeRole.Box: return BoxNode(def.Index);
                default: return OutputNode(def.Index);
            }
        }

        void Visit(int node, bool[] registered, byte[] state, List<int> order)
        {
            if (state[node] == 2) return;
            if (state[node] == 1)
                throw new CircuitException($"{Puzzle.Id}: there is a loop through '{NameOf(node)}' with no delay in it, so the circuit can never settle.");
            state[node] = 1;
            foreach (int driver in Drivers[node])
            {
                if (driver < InputCount) continue;
                if (registered[driver]) continue; // known at the start of the tick
                Visit(driver, registered, state, order);
            }
            state[node] = 2;
            order.Add(node);
        }

        void ComputeDepths()
        {
            // Longest path from an input, ignoring edges that close a loop.
            for (int pass = 0; pass < NodeCount; pass++)
            {
                bool changed = false;
                foreach (int node in Order)
                {
                    int d = 0;
                    foreach (int driver in Drivers[node])
                        if (driver >= 0 && IsForward(driver, node)) d = Math.Max(d, Depth[driver] + 1);
                    if (d != Depth[node]) { Depth[node] = d; changed = true; }
                }
                if (!changed) break;
            }
        }

        /// <summary>True when a wire runs left to right on the board (not a loop back).</summary>
        public bool IsForward(int from, int to)
        {
            return ColumnOf(from) < ColumnOf(to) || (ColumnOf(from) == ColumnOf(to) && from < to);
        }

        float ColumnOf(int node)
        {
            if (node < InputCount) return Puzzle.Inputs[node].At.Col;
            if (IsBox(node)) return Puzzle.Boxes[node - InputCount].At.Col;
            return Puzzle.Outputs[node - InputCount - BoxCount].At.Col;
        }

        public string NameOf(int node)
        {
            if (node < InputCount) return Puzzle.Inputs[node].Id;
            if (IsBox(node)) return Puzzle.Boxes[node - InputCount].Id;
            return Puzzle.Outputs[node - InputCount - BoxCount].Id;
        }

        /// <summary>Runs every case with the given settings.</summary>
        public RunResult Run(int[] settings)
        {
            var logics = CreateLogics(settings);
            var cases = new CaseResult[Puzzle.Cases.Count];
            for (int c = 0; c < cases.Length; c++) cases[c] = RunCase(Puzzle.Cases[c], logics);
            return new RunResult(this, settings, cases);
        }

        /// <summary>
        /// True when every case passes. Stops at the first failure and keeps no history,
        /// which makes it suitable for the brute-force solver.
        /// </summary>
        public bool Passes(int[] settings, IBoxLogic[] scratch = null)
        {
            var logics = scratch ?? CreateLogics(settings);
            var values = new bool[NodeCount];
            foreach (var cs in Puzzle.Cases)
            {
                foreach (var logic in logics) logic.Reset();
                for (int tick = 0; tick < cs.Ticks; tick++)
                {
                    Tick(cs, tick, logics, values);
                    for (int o = 0; o < OutputCount; o++)
                        if (cs.Cares(tick, o) && values[OutputNode(o)] != cs.Wanted(tick, o)) return false;
                }
            }
            return true;
        }

        public IBoxLogic[] CreateLogics(int[] settings)
        {
            if (settings.Length != BoxCount) throw new ArgumentException($"Expected {BoxCount} settings, got {settings.Length}.");
            var logics = new IBoxLogic[BoxCount];
            for (int b = 0; b < BoxCount; b++) logics[b] = Puzzle.Boxes[b].Kind.CreateLogic(settings[b]);
            return logics;
        }

        CaseResult RunCase(CaseDef cs, IBoxLogic[] logics)
        {
            foreach (var logic in logics) logic.Reset();
            var values = new bool[cs.Ticks][];
            for (int tick = 0; tick < cs.Ticks; tick++)
            {
                values[tick] = new bool[NodeCount];
                Tick(cs, tick, logics, values[tick]);
            }
            return new CaseResult(this, cs, values);
        }

        readonly List<bool[]> _portBuffers = new List<bool[]>();

        bool[] PortBuffer(int count)
        {
            while (_portBuffers.Count <= count) _portBuffers.Add(new bool[_portBuffers.Count]);
            return _portBuffers[count];
        }

        void Tick(CaseDef cs, int tick, IBoxLogic[] logics, bool[] values)
        {
            for (int i = 0; i < InputCount; i++) values[i] = cs.Inputs[tick][i];

            // Registered boxes speak first: their output comes from what they stored last tick.
            for (int b = 0; b < BoxCount; b++)
                if (logics[b].Registered) values[BoxNode(b)] = logics[b].Peek();

            foreach (int node in Order)
            {
                var drivers = Drivers[node];
                if (IsOutput(node)) { values[node] = values[drivers[0]]; continue; }
                var logic = logics[node - InputCount];
                if (logic.Registered) continue;
                var ins = PortBuffer(drivers.Length);
                for (int p = 0; p < drivers.Length; p++) ins[p] = values[drivers[p]];
                values[node] = logic.Step(ins);
            }

            // Now that every value for this tick is known, registered boxes store their inputs.
            for (int b = 0; b < BoxCount; b++)
            {
                if (!logics[b].Registered) continue;
                var drivers = Drivers[BoxNode(b)];
                var ins = PortBuffer(drivers.Length);
                for (int p = 0; p < drivers.Length; p++) ins[p] = values[drivers[p]];
                logics[b].Step(ins);
            }
        }
    }

    /// <summary>Everything that happened in one case: every node's value on every tick.</summary>
    public sealed class CaseResult
    {
        readonly CompiledCircuit _circuit;
        public CaseDef Case { get; }
        public int Ticks => Case.Ticks;
        /// <summary>[tick][node]</summary>
        public bool[][] Values { get; }

        public CaseResult(CompiledCircuit circuit, CaseDef cs, bool[][] values)
        {
            _circuit = circuit;
            Case = cs;
            Values = values;
        }

        public bool Value(int tick, int node) => Values[tick][node];
        public bool Output(int tick, int output) => Values[tick][_circuit.OutputNode(output)];

        /// <summary>True when the output matches on this tick (or nobody cares what it is).</summary>
        public bool OutputOk(int tick, int output) => !Case.Cares(tick, output) || Output(tick, output) == Case.Wanted(tick, output);

        public bool OutputOkAllTicks(int output)
        {
            for (int t = 0; t < Ticks; t++) if (!OutputOk(t, output)) return false;
            return true;
        }

        public bool TickOk(int tick)
        {
            for (int o = 0; o < _circuit.OutputCount; o++) if (!OutputOk(tick, o)) return false;
            return true;
        }

        public bool Matched
        {
            get
            {
                for (int t = 0; t < Ticks; t++) if (!TickOk(t)) return false;
                return true;
            }
        }

        /// <summary>The values arriving at each input port of a box on a tick.</summary>
        public bool[] BoxInputs(int box, int tick)
        {
            var drivers = _circuit.Drivers[_circuit.BoxNode(box)];
            var ins = new bool[drivers.Length];
            for (int p = 0; p < drivers.Length; p++) ins[p] = Values[tick][drivers[p]];
            return ins;
        }

        public bool BoxOutput(int box, int tick) => Values[tick][_circuit.BoxNode(box)];
    }

    public sealed class RunResult
    {
        public CompiledCircuit Circuit { get; }
        public int[] Settings { get; }
        public CaseResult[] Cases { get; }

        public RunResult(CompiledCircuit circuit, int[] settings, CaseResult[] cases)
        {
            Circuit = circuit;
            Settings = (int[])settings.Clone();
            Cases = cases;
        }

        public bool AllMatched
        {
            get
            {
                foreach (var c in Cases) if (!c.Matched) return false;
                return true;
            }
        }

        public int MatchedCases
        {
            get
            {
                int n = 0;
                foreach (var c in Cases) if (c.Matched) n++;
                return n;
            }
        }

        /// <summary>An output counts as matched when it is right on every tick of every case.</summary>
        public bool OutputMatched(int output)
        {
            foreach (var c in Cases) if (!c.OutputOkAllTicks(output)) return false;
            return true;
        }

        public int MatchedOutputs
        {
            get
            {
                int n = 0;
                for (int o = 0; o < Circuit.OutputCount; o++) if (OutputMatched(o)) n++;
                return n;
            }
        }
    }
}
