using System;
using System.Collections.Generic;
using Blackbox.Puzzles;

namespace Blackbox.Circuits
{
    public sealed class CircuitException : Exception
    {
        public CircuitException(string message) : base(message) { }
    }

    /// <summary>
    /// A puzzle's wiring turned into arrays that evaluate quickly.
    ///
    /// Nodes are numbered inputs first, then blocks, then outputs. Every node has one
    /// value: an input's is what it is set to, a block's is the signal it sends, and an
    /// output's is the signal that reaches it. Signals flow one way, so one pass in
    /// <see cref="Order"/> settles the whole circuit.
    /// </summary>
    public sealed class CompiledCircuit
    {
        public PuzzleDef Puzzle { get; }
        public int InputCount { get; }
        public int BlockCount { get; }
        public int OutputCount { get; }
        public int NodeCount => InputCount + BlockCount + OutputCount;

        /// <summary>[node][port] = the node whose signal drives that port. Inputs have no ports.</summary>
        public int[][] Drivers { get; }

        /// <summary>Blocks and outputs in an order where every node comes after what it reads.</summary>
        public int[] Order { get; }

        /// <summary>Distance of each node from the inputs, for animating a change travelling across the board.</summary>
        public int[] Depth { get; }

        /// <summary>The deepest node's depth.</summary>
        public int MaxDepth { get; }

        /// <summary>[node] = the nodes its signal reaches directly.</summary>
        public int[][] Readers { get; }

        readonly int[][] _scratch;

        public int BlockNode(int block) => InputCount + block;
        public int OutputNode(int output) => InputCount + BlockCount + output;
        public bool IsInput(int node) => node < InputCount;
        public bool IsBlock(int node) => node >= InputCount && node < InputCount + BlockCount;
        public bool IsOutput(int node) => node >= InputCount + BlockCount;

        public CompiledCircuit(PuzzleDef puzzle)
        {
            Puzzle = puzzle;
            InputCount = puzzle.Inputs.Count;
            BlockCount = puzzle.Blocks.Count;
            OutputCount = puzzle.Outputs.Count;

            Drivers = new int[NodeCount][];
            for (int i = 0; i < InputCount; i++) Drivers[i] = Array.Empty<int>();
            for (int b = 0; b < BlockCount; b++) Drivers[BlockNode(b)] = Filled(puzzle.Blocks[b].Kind.InputCount);
            for (int o = 0; o < OutputCount; o++) Drivers[OutputNode(o)] = Filled(1);
            foreach (var w in puzzle.Wires) Drivers[NodeOf(w.To)][w.ToPort] = NodeOf(w.From);

            for (int n = 0; n < NodeCount; n++)
                foreach (int d in Drivers[n])
                    if (d < 0) throw new CircuitException($"{puzzle.Id}: {NameOf(n)} has a port with nothing wired into it.");

            var readers = new List<int>[NodeCount];
            for (int n = 0; n < NodeCount; n++) readers[n] = new List<int>();
            for (int n = 0; n < NodeCount; n++)
                foreach (int d in Drivers[n])
                    if (!readers[d].Contains(n)) readers[d].Add(n);
            Readers = new int[NodeCount][];
            for (int n = 0; n < NodeCount; n++) Readers[n] = readers[n].ToArray();

            var order = new List<int>();
            var state = new byte[NodeCount]; // 0 unvisited, 1 visiting, 2 done
            for (int n = InputCount; n < NodeCount; n++) Visit(n, state, order);
            Order = order.ToArray();

            Depth = new int[NodeCount];
            foreach (int n in Order)
            {
                int depth = 0;
                foreach (int d in Drivers[n]) depth = Math.Max(depth, Depth[d] + 1);
                Depth[n] = depth;
            }
            int max = 0;
            foreach (int d in Depth) max = Math.Max(max, d);
            MaxDepth = max;

            _scratch = new int[NodeCount][];
            for (int n = 0; n < NodeCount; n++) _scratch[n] = new int[Drivers[n].Length];
        }

        static int[] Filled(int count)
        {
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = -1;
            return a;
        }

        void Visit(int n, byte[] state, List<int> order)
        {
            if (state[n] == 2) return;
            if (state[n] == 1) throw new CircuitException($"{Puzzle.Id}: the wires loop back through {NameOf(n)}.");
            state[n] = 1;
            foreach (int d in Drivers[n])
                if (d >= InputCount) Visit(d, state, order);
            state[n] = 2;
            order.Add(n);
        }

        public int NodeOf(NodeDef def)
        {
            switch (def.Role)
            {
                case NodeRole.Input: return def.Index;
                case NodeRole.Block: return BlockNode(def.Index);
                default: return OutputNode(def.Index);
            }
        }

        public NodeDef DefOf(int node)
        {
            if (IsInput(node)) return Puzzle.Inputs[node];
            if (IsBlock(node)) return Puzzle.Blocks[node - InputCount];
            return Puzzle.Outputs[node - InputCount - BlockCount];
        }

        public string NameOf(int node) => DefOf(node).Id;

        /// <summary>Settles the circuit: fills <paramref name="values"/> (one per node) from the inputs and block states.</summary>
        public void Evaluate(int[] inputs, int[] states, int[] values)
        {
            for (int i = 0; i < InputCount; i++) values[i] = inputs[i];
            foreach (int n in Order)
            {
                var drivers = Drivers[n];
                if (IsOutput(n))
                {
                    values[n] = values[drivers[0]];
                    continue;
                }
                var ins = _scratch[n];
                for (int p = 0; p < drivers.Length; p++) ins[p] = values[drivers[p]];
                int b = n - InputCount;
                values[n] = Puzzle.Blocks[b].Kind.Apply(ins, states[b]);
            }
        }

        public int[] Evaluate(int[] inputs, int[] states)
        {
            var values = new int[NodeCount];
            Evaluate(inputs, states, values);
            return values;
        }

        /// <summary>True for every node whose value can depend on <paramref name="node"/>, itself included.</summary>
        public bool[] Downstream(int node)
        {
            var reached = new bool[NodeCount];
            var stack = new Stack<int>();
            stack.Push(node);
            reached[node] = true;
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                foreach (int r in Readers[n])
                {
                    if (reached[r]) continue;
                    reached[r] = true;
                    stack.Push(r);
                }
            }
            return reached;
        }
    }
}
