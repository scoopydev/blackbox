using System;
using System.Collections.Generic;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    /// <summary>
    /// What a run of a wired circuit found: the signal at every node, which
    /// outputs got what they want, and which of the rules are broken.
    /// </summary>
    public sealed class WiringReport
    {
        /// <summary>The signal each node sends or receives, or null for nothing.</summary>
        public Trace[] Values;
        /// <summary>For each output: it got the signal it wants.</summary>
        public bool[] OutputMet;
        /// <summary>Blocks with an input that has no wire. They send nothing.</summary>
        public readonly List<int> EmptyBlocks = new List<int>();
        /// <summary>Blocks wired up fully, but fed nothing by a block upstream. They send nothing either.</summary>
        public readonly List<int> StarvedBlocks = new List<int>();
        /// <summary>Blocks whose signal doesn't lead to any output.</summary>
        public readonly List<int> UnusedBlocks = new List<int>();
        /// <summary>Inputs whose signal doesn't lead to any output.</summary>
        public readonly List<int> UnusedInputs = new List<int>();
        /// <summary>Outputs with no wire into them.</summary>
        public readonly List<int> UnwiredOutputs = new List<int>();

        /// <summary>How many outputs got what they want.</summary>
        public int Met
        {
            get
            {
                int n = 0;
                foreach (bool m in OutputMet) if (m) n++;
                return n;
            }
        }

        /// <summary>Every input and every block is used, and every block input is connected.</summary>
        public bool RulesKept => EmptyBlocks.Count == 0 && UnusedBlocks.Count == 0 && UnusedInputs.Count == 0;

        /// <summary>Every output gets what it wants, and the rules are kept.</summary>
        public bool Solved => RulesKept && Met == OutputMet.Length;
    }

    /// <summary>
    /// A circuit the player wires. It starts with no wires. A wire runs from an
    /// input or a block's output to one of a block's inputs or to an output; each
    /// of those takes one wire, and a new wire there replaces the old one. A
    /// signal can feed any number of wires, but never loop back on itself.
    ///
    /// Nodes are numbered as in <see cref="Circuits.CompiledCircuit"/>: inputs
    /// first, then blocks, then outputs. A block sends nothing until every one of
    /// its inputs is connected to something that sends a signal.
    ///
    /// The circuit is solved by a run in which every output gets the signal it
    /// wants, every input and every block leads to an output, and no block input
    /// is empty.
    /// </summary>
    public sealed class WiringSession
    {
        public PuzzleDef Puzzle { get; }
        public int InputCount { get; }
        public int BlockCount { get; }
        public int OutputCount { get; }
        public int NodeCount => InputCount + BlockCount + OutputCount;

        /// <summary>Wires added or taken off.</summary>
        public int Moves { get; private set; }
        public int Runs { get; private set; }
        /// <summary>The circuit has run since its last change, so <see cref="LastRun"/> is current.</summary>
        public bool HasRun { get; private set; }
        public WiringReport LastRun { get; private set; }
        /// <summary>The latest run solved the circuit, and nothing has changed since.</summary>
        public bool RunSolved => HasRun && LastRun != null && LastRun.Solved;

        /// <summary>Raised after any change to the wires.</summary>
        public event Action Changed;

        readonly int[][] _driver;
        readonly List<int[][]> _undo = new List<int[][]>();
        const int UndoLimit = 200;

        public WiringSession(PuzzleDef puzzle, IList<string> resume = null)
        {
            if (!puzzle.Wiring) throw new ArgumentException($"{puzzle.Id} isn't a wired circuit.", nameof(puzzle));
            Puzzle = puzzle;
            InputCount = puzzle.Inputs.Count;
            BlockCount = puzzle.Blocks.Count;
            OutputCount = puzzle.Outputs.Count;
            _driver = Empty(puzzle);
            if (resume == null)
            {
                PlaceStart();
                return;
            }
            foreach (var text in resume)
                if (TryReadLink(text, out int from, out int to, out int port) && CanConnect(from, to, port, out _))
                    _driver[to][port] = from;
        }

        void PlaceStart()
        {
            foreach (var w in Puzzle.Start)
            {
                int from = NodeOf(w.From), to = NodeOf(w.To);
                if (CanConnect(from, to, w.ToPort, out _)) _driver[to][w.ToPort] = from;
            }
        }

        /// <summary>True when the circuit begins with some wires in place.</summary>
        public bool HasStart => Puzzle.Start.Count > 0;

        /// <summary>True when the wiring is as the circuit began.</summary>
        public bool AsStarted
        {
            get
            {
                var fresh = new WiringSession(Puzzle);
                for (int n = 0; n < NodeCount; n++)
                    for (int k = 0; k < _driver[n].Length; k++)
                        if (_driver[n][k] != fresh._driver[n][k]) return false;
                return true;
            }
        }

        /// <summary>Puts the wiring back as the circuit began, as one change. False if it already is.</summary>
        public bool Restart()
        {
            if (AsStarted) return false;
            Remember();
            foreach (var ports in _driver) for (int k = 0; k < ports.Length; k++) ports[k] = -1;
            PlaceStart();
            Touched();
            return true;
        }

        static int[][] Empty(PuzzleDef p)
        {
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count;
            var driver = new int[inputs + blocks + p.Outputs.Count][];
            for (int n = 0; n < driver.Length; n++)
            {
                int ports = n < inputs ? 0 : n < inputs + blocks ? p.Blocks[n - inputs].Kind.InputCount : 1;
                driver[n] = new int[ports];
                for (int k = 0; k < ports; k++) driver[n][k] = -1;
            }
            return driver;
        }

        // ------------------------------------------------------------------ nodes

        public int BlockNode(int block) => InputCount + block;
        public int OutputNode(int output) => InputCount + BlockCount + output;
        public bool IsInput(int node) => node >= 0 && node < InputCount;
        public bool IsBlock(int node) => node >= InputCount && node < InputCount + BlockCount;
        public bool IsOutput(int node) => node >= InputCount + BlockCount && node < NodeCount;
        /// <summary>Inputs and blocks send signals.</summary>
        public bool IsSource(int node) => IsInput(node) || IsBlock(node);
        /// <summary>The ports a node receives on: one for an output, one per input for a block, none for an input.</summary>
        public int PortCount(int node) => node >= 0 && node < NodeCount ? _driver[node].Length : 0;

        public NodeDef DefOf(int node)
        {
            if (IsInput(node)) return Puzzle.Inputs[node];
            if (IsBlock(node)) return Puzzle.Blocks[node - InputCount];
            return Puzzle.Outputs[node - InputCount - BlockCount];
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

        public string NameOf(int node) => DefOf(node).Id;

        // ------------------------------------------------------------------ wires

        /// <summary>The node wired into a port, or -1 if none is.</summary>
        public int DriverOf(int node, int port) => _driver[node][port];

        /// <summary>Every wire, in node and port order.</summary>
        public List<(int from, int to, int port)> Wires
        {
            get
            {
                var list = new List<(int, int, int)>();
                for (int n = 0; n < NodeCount; n++)
                    for (int k = 0; k < _driver[n].Length; k++)
                        if (_driver[n][k] >= 0) list.Add((_driver[n][k], n, k));
                return list;
            }
        }

        public int WireCount
        {
            get
            {
                int count = 0;
                foreach (var ports in _driver) foreach (int d in ports) if (d >= 0) count++;
                return count;
            }
        }

        /// <summary>The nodes a node's signal feeds directly.</summary>
        public List<int> ReadersOf(int node)
        {
            var list = new List<int>();
            for (int n = 0; n < NodeCount; n++)
                foreach (int d in _driver[n])
                    if (d == node && !list.Contains(n)) list.Add(n);
            return list;
        }

        /// <summary>
        /// True when a wire may run from <paramref name="from"/> into port
        /// <paramref name="port"/> of <paramref name="to"/>. If not, <paramref name="why"/>
        /// says why in a sentence for the player.
        /// </summary>
        public bool CanConnect(int from, int to, int port, out string why)
        {
            why = null;
            if (!IsSource(from)) { why = "Wires start at an input or at the right side of a block."; return false; }
            if (to < 0 || to >= NodeCount || IsInput(to)) { why = "Wires end at the left side of a block, or at an output."; return false; }
            if (port < 0 || port >= PortCount(to)) { why = "That block has no input there."; return false; }
            if (from == to) { why = "A block can't feed itself."; return false; }
            if (_driver[to][port] == from) { why = "Those are already wired together."; return false; }
            if (IsBlock(to) && Reaches(to, from)) { why = $"That would loop: {NameOf(from)} already takes its signal from {NameOf(to)}. Signals only flow one way."; return false; }
            return true;
        }

        /// <summary>True when <paramref name="target"/>'s signal can depend on <paramref name="source"/>'s, through the wires.</summary>
        public bool Reaches(int source, int target)
        {
            if (source == target) return true;
            var seen = new bool[NodeCount];
            var stack = new Stack<int>();
            stack.Push(target);
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                if (n == source) return true;
                if (seen[n]) continue;
                seen[n] = true;
                foreach (int d in _driver[n]) if (d >= 0) stack.Push(d);
            }
            return false;
        }

        /// <summary>Runs a wire, replacing any wire already in that port. False if it isn't allowed.</summary>
        public bool Connect(int from, int to, int port)
        {
            if (!CanConnect(from, to, port, out _)) return false;
            Remember();
            _driver[to][port] = from;
            Touched();
            return true;
        }

        /// <summary>Takes the wire out of a port. False if there wasn't one.</summary>
        public bool Disconnect(int to, int port)
        {
            if (to < 0 || to >= NodeCount || port < 0 || port >= PortCount(to) || _driver[to][port] < 0) return false;
            Remember();
            _driver[to][port] = -1;
            Touched();
            return true;
        }

        /// <summary>Takes off every wire a node's signal feeds, as one change. False if there were none.</summary>
        public bool DisconnectFrom(int source)
        {
            if (ReadersOf(source).Count == 0) return false;
            Remember();
            foreach (var ports in _driver)
                for (int k = 0; k < ports.Length; k++)
                    if (ports[k] == source) ports[k] = -1;
            Touched();
            return true;
        }

        /// <summary>Takes off every wire. False if there were none.</summary>
        public bool Clear()
        {
            if (WireCount == 0) return false;
            Remember();
            foreach (var ports in _driver) for (int k = 0; k < ports.Length; k++) ports[k] = -1;
            Touched();
            return true;
        }

        public bool AtStart => WireCount == 0;
        public bool CanUndo => _undo.Count > 0;

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var last = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            for (int n = 0; n < NodeCount; n++) Array.Copy(last[n], _driver[n], _driver[n].Length);
            Moves++;
            HasRun = false;
            Changed?.Invoke();
            return true;
        }

        void Remember()
        {
            var copy = new int[NodeCount][];
            for (int n = 0; n < NodeCount; n++) copy[n] = (int[])_driver[n].Clone();
            _undo.Add(copy);
            if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        }

        void Touched()
        {
            Moves++;
            HasRun = false;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ running

        /// <summary>What the circuit does as wired now, without counting as a run.</summary>
        public WiringReport Check() => Check(Puzzle, _driver);

        /// <summary>Runs the circuit. True if the run solves it.</summary>
        public bool Run()
        {
            Runs++;
            LastRun = Check();
            HasRun = true;
            return LastRun.Solved;
        }

        public static WiringReport Check(PuzzleDef p, int[][] driver)
        {
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count, outputs = p.Outputs.Count;
            var report = new WiringReport { Values = Evaluate(p, driver), OutputMet = new bool[outputs] };
            for (int b = 0; b < blocks; b++)
            {
                var ports = driver[inputs + b];
                bool empty = Array.IndexOf(ports, -1) >= 0;
                if (empty) report.EmptyBlocks.Add(b);
                else if (report.Values[inputs + b] == null) report.StarvedBlocks.Add(b);
            }
            for (int o = 0; o < outputs; o++)
            {
                int node = inputs + blocks + o;
                if (driver[node][0] < 0) report.UnwiredOutputs.Add(o);
                var got = report.Values[node];
                report.OutputMet[o] = got != null && Devices.Same(p.Outputs[o].Device, got, p.Outputs[o].Want);
            }
            report.UnusedBlocks.AddRange(UnusedBlocks(p, driver));
            report.UnusedInputs.AddRange(UnusedInputs(p, driver));
            return report;
        }

        /// <summary>
        /// The signal at every node, or null where there is none: an output with
        /// no wire, or a block with an input that gets nothing.
        /// </summary>
        public static Trace[] Evaluate(PuzzleDef p, int[][] driver)
        {
            int inputs = p.Inputs.Count;
            var values = new Trace[driver.Length];
            var state = new byte[driver.Length]; // 0 not yet, 1 working it out, 2 done
            for (int i = 0; i < inputs; i++)
            {
                values[i] = p.Inputs[i].Signal.Trace;
                state[i] = 2;
            }

            Trace Value(int n)
            {
                if (state[n] == 2) return values[n];
                if (state[n] == 1) return null; // a loop: never allowed, but never hang on one
                state[n] = 1;
                var ports = driver[n];
                Trace result = null;
                if (n >= inputs + p.Blocks.Count)
                {
                    result = ports[0] >= 0 ? Value(ports[0]) : null;
                }
                else
                {
                    var ins = new Trace[ports.Length];
                    bool all = true;
                    for (int k = 0; k < ports.Length && all; k++)
                    {
                        ins[k] = ports[k] >= 0 ? Value(ports[k]) : null;
                        all = ins[k] != null;
                    }
                    if (all)
                    {
                        var block = p.Blocks[n - inputs];
                        result = block.Kind.Apply(ins);
                    }
                }
                values[n] = result;
                state[n] = 2;
                return result;
            }

            for (int n = inputs; n < driver.Length; n++) Value(n);
            return values;
        }

        /// <summary>Evaluates a list of wires, such as a circuit's solution.</summary>
        public static Trace[] Evaluate(PuzzleDef p, IEnumerable<WireDef> wires) => Evaluate(p, DriversOf(p, wires));

        /// <summary>Blocks whose signal reaches no output, through these wires.</summary>
        public static List<int> UnusedBlocks(PuzzleDef p, IEnumerable<WireDef> wires) => UnusedBlocks(p, DriversOf(p, wires));

        public static List<int> UnusedBlocks(PuzzleDef p, int[][] driver)
        {
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count;
            var used = Used(p, driver);
            var unused = new List<int>();
            for (int b = 0; b < blocks; b++) if (!used[inputs + b]) unused.Add(b);
            return unused;
        }

        public static List<int> UnusedInputs(PuzzleDef p, IEnumerable<WireDef> wires) => UnusedInputs(p, DriversOf(p, wires));

        /// <summary>Inputs whose signal doesn't lead to any output, straight there or through blocks.</summary>
        public static List<int> UnusedInputs(PuzzleDef p, int[][] driver)
        {
            var used = Used(p, driver);
            var unused = new List<int>();
            for (int i = 0; i < p.Inputs.Count; i++) if (!used[i]) unused.Add(i);
            return unused;
        }

        /// <summary>Every input and every block leads to an output: the rule on what must be used.</summary>
        public static bool AllUsed(PuzzleDef p, int[][] driver)
        {
            var used = Used(p, driver);
            for (int n = 0; n < p.Inputs.Count + p.Blocks.Count; n++) if (!used[n]) return false;
            return true;
        }

        /// <summary>Each node whose signal leads to an output.</summary>
        static bool[] Used(PuzzleDef p, int[][] driver)
        {
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count;
            var used = new bool[driver.Length];
            var stack = new Stack<int>();
            for (int o = 0; o < p.Outputs.Count; o++) stack.Push(inputs + blocks + o);
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                if (used[n]) continue;
                used[n] = true;
                foreach (int d in driver[n]) if (d >= 0) stack.Push(d);
            }
            return used;
        }

        static int[][] DriversOf(PuzzleDef p, IEnumerable<WireDef> wires)
        {
            var driver = Empty(p);
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count;
            int Node(NodeDef d) => d.Role == NodeRole.Input ? d.Index : d.Role == NodeRole.Block ? inputs + d.Index : inputs + blocks + d.Index;
            foreach (var w in wires) driver[Node(w.To)][w.ToPort] = Node(w.From);
            return driver;
        }

        /// <summary>
        /// Which blocks still send what they sent when the circuit was wired as
        /// <paramref name="before"/>, now that it is wired as <paramref name="now"/>.
        /// A block whose own input wires are all as they were, fed only by inputs
        /// and by blocks that are unchanged too, is unchanged. A block whose inputs
        /// were rewired has changed, and so has every block it feeds. Rewiring where
        /// a block's signal goes changes nothing about the block itself.
        /// </summary>
        public static bool[] BlocksUnchanged(PuzzleDef p, int[][] before, int[][] now)
        {
            int inputs = p.Inputs.Count, blocks = p.Blocks.Count;
            var state = new int[blocks]; // 0 not yet known, 1 unchanged, 2 changed, 3 being worked out
            bool Unchanged(int b)
            {
                if (state[b] == 1 || state[b] == 2) return state[b] == 1;
                if (state[b] == 3) return false; // a loop, which wiring never allows; to be safe
                state[b] = 3;
                bool same = true;
                var was = before[inputs + b];
                var wired = now[inputs + b];
                for (int k = 0; k < wired.Length && same; k++)
                {
                    if (k >= was.Length || was[k] != wired[k]) same = false;
                    else if (wired[k] >= inputs && wired[k] < inputs + blocks) same = Unchanged(wired[k] - inputs);
                }
                state[b] = same ? 1 : 2;
                return same;
            }
            var result = new bool[blocks];
            for (int b = 0; b < blocks; b++) result[b] = Unchanged(b);
            return result;
        }

        /// <summary>A copy of the wiring, as the driver of every port.</summary>
        public int[][] Drivers
        {
            get
            {
                var copy = new int[NodeCount][];
                for (int n = 0; n < NodeCount; n++) copy[n] = (int[])_driver[n].Clone();
                return copy;
            }
        }

        // ------------------------------------------------------------------ saving

        /// <summary>The wires as text for the save, like "A -> g.0".</summary>
        public List<string> Snapshot()
        {
            var list = new List<string>();
            foreach (var (from, to, port) in Wires) list.Add(LinkText(from, to, port));
            return list;
        }

        public string LinkText(int from, int to, int port) =>
            IsBlock(to) && PortCount(to) > 1 ? $"{NameOf(from)} -> {NameOf(to)}.{port}" : $"{NameOf(from)} -> {NameOf(to)}";

        bool TryReadLink(string text, out int from, out int to, out int port)
        {
            from = to = port = -1;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split(new[] { "->" }, StringSplitOptions.None);
            if (parts.Length != 2) return false;
            string target = parts[1].Trim();
            port = 0;
            int dot = target.LastIndexOf('.');
            if (dot > 0 && int.TryParse(target.Substring(dot + 1), out int p))
            {
                port = p;
                target = target.Substring(0, dot);
            }
            var source = Puzzle.Find(parts[0].Trim());
            var sink = Puzzle.Find(target);
            if (source == null || sink == null) return false;
            from = NodeOf(source);
            to = NodeOf(sink);
            return true;
        }
    }
}
