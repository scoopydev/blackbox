using System;
using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Components;
using Blackbox.Core;

namespace Blackbox.Puzzles
{
    /// <summary>A probe on a wire: which wire, and how far along it (0 at the source, 1 at the end).</summary>
    public struct Probe
    {
        public int Wire;
        public float At;
        /// <summary>The probe was on the circuit for the latest run, so it has a reading to show.</summary>
        public bool Read;
        public Probe(int wire, float at) { Wire = wire; At = at; Read = false; }
    }

    public enum ChangeKind { Input, Block }

    /// <summary>
    /// One attempt at one circuit: input values, block states, probes and an undo
    /// history. Every change settles the circuit at once, but the player only sees
    /// what comes out when they run it: outputs, probe readings and target marks
    /// belong to the latest <see cref="Run"/>, and any change clears them. The
    /// circuit is solved by a run in which every target matches. Has no Unity
    /// dependencies, so the whole play loop can be tested without the engine. The
    /// screen listens to its events.
    /// </summary>
    public sealed class PuzzleSession
    {
        readonly int[] _inputs;
        readonly int[] _states;
        readonly int[] _values;
        readonly List<Probe> _probes = new List<Probe>();
        readonly Stack<(ChangeKind kind, int index, int previous)> _undo = new Stack<(ChangeKind, int, int)>();

        public PuzzleDef Puzzle { get; }
        public CompiledCircuit Circuit { get; }
        public Medium Medium => Puzzle.Medium;

        /// <summary>Changes made on this visit, undos included.</summary>
        public int Moves { get; private set; }

        /// <summary>Runs on this visit.</summary>
        public int Runs { get; private set; }

        /// <summary>
        /// The circuit has been run and nothing has changed since, so its outputs,
        /// probe readings and target marks can be shown.
        /// </summary>
        public bool HasRun { get; private set; }

        /// <summary>True once a run has solved the circuit on this visit.</summary>
        public bool EverSolved { get; private set; }

        /// <summary>An input or block changed: (what, its index). The circuit has already settled.</summary>
        public event Action<ChangeKind, int> Changed;
        /// <summary>Everything went back to the start.</summary>
        public event Action Reset;
        /// <summary>A probe was placed or removed.</summary>
        public event Action ProbesChanged;
        /// <summary>The circuit was run: its results can be shown.</summary>
        public event Action Ran;

        public PuzzleSession(PuzzleDef puzzle, CircuitState resume = null)
        {
            Puzzle = puzzle;
            Circuit = new CompiledCircuit(puzzle);
            _inputs = puzzle.StartInputs();
            _states = puzzle.StartStates();
            _values = new int[Circuit.NodeCount];

            if (resume != null)
            {
                if (resume.Inputs.Length == _inputs.Length)
                    for (int i = 0; i < _inputs.Length; i++)
                        if (puzzle.InputChangeable(i) && puzzle.Inputs[i].ChoiceIndex(Medium, resume.Inputs[i]) >= 0) _inputs[i] = resume.Inputs[i];
                if (resume.States.Length == _states.Length)
                    for (int b = 0; b < _states.Length; b++)
                        if (puzzle.BlockChangeable(b) && resume.States[b] >= 0 && resume.States[b] < puzzle.Blocks[b].Kind.StateCount) _states[b] = resume.States[b];
                foreach (var (wire, at) in resume.Probes)
                    if (wire >= 0 && wire < puzzle.Wires.Count && _probes.Count < puzzle.Probes)
                        _probes.Add(new Probe(wire, Clamp01(at / 1000f)));
            }
            Settle();
        }

        void Settle()
        {
            Circuit.Evaluate(_inputs, _states, _values);
            HasRun = false;
        }

        /// <summary>
        /// Runs the circuit: its outputs, every probe's reading and every target's
        /// mark can now be shown. Returns true when the run solves it.
        /// </summary>
        public bool Run()
        {
            Runs++;
            HasRun = true;
            for (int k = 0; k < _probes.Count; k++)
            {
                var probe = _probes[k];
                probe.Read = true;
                _probes[k] = probe;
            }
            if (Solved) EverSolved = true;
            Ran?.Invoke();
            return Solved;
        }

        /// <summary>The latest run solved the circuit, and nothing has changed since.</summary>
        public bool RunSolved => HasRun && Solved;

        // ------------------------------------------------------------------ reading

        public int InputValue(int input) => _inputs[input];
        public int State(int block) => _states[block];
        public int Value(int node) => _values[node];
        public int ValueOf(NodeDef node) => _values[Circuit.NodeOf(node)];
        public int[] CopyInputs() => (int[])_inputs.Clone();
        public int[] CopyStates() => (int[])_states.Clone();
        public int[] CopyValues() => (int[])_values.Clone();

        public bool TargetMatched(int target)
        {
            var t = Puzzle.Targets[target];
            return Media.Same(Medium, ValueOf(t.Node), t.Want);
        }

        public int MatchedTargets
        {
            get
            {
                int n = 0;
                for (int t = 0; t < Puzzle.Targets.Count; t++) if (TargetMatched(t)) n++;
                return n;
            }
        }

        /// <summary>Every target matches as the circuit stands, run or not. A circuit with no targets is never solved.</summary>
        public bool Solved => Puzzle.Targets.Count > 0 && MatchedTargets == Puzzle.Targets.Count;

        public bool AtStart
        {
            get
            {
                for (int i = 0; i < _inputs.Length; i++) if (_inputs[i] != Puzzle.Inputs[i].Value) return false;
                for (int b = 0; b < _states.Length; b++) if (_states[b] != Puzzle.Blocks[b].State) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ changing

        public bool CanChangeInput(int input) => input >= 0 && input < _inputs.Length && Puzzle.InputChangeable(input);
        public bool CanChangeBlock(int block) => block >= 0 && block < _states.Length && Puzzle.BlockChangeable(block);

        public bool SetInput(int input, int value)
        {
            if (!CanChangeInput(input)) return false;
            if (Puzzle.Inputs[input].ChoiceIndex(Medium, value) < 0) return false;
            if (_inputs[input] == value) return false;
            _undo.Push((ChangeKind.Input, input, _inputs[input]));
            _inputs[input] = value;
            Moves++;
            Settle();
            Changed?.Invoke(ChangeKind.Input, input);
            return true;
        }

        /// <summary>Steps an input to its next (+1) or previous (-1) choice, wrapping round.</summary>
        public bool CycleInput(int input, int direction)
        {
            if (!CanChangeInput(input)) return false;
            var def = Puzzle.Inputs[input];
            int n = def.ChoiceCount(Medium);
            int k = def.ChoiceIndex(Medium, _inputs[input]);
            k = (((k < 0 ? 0 : k + direction) % n) + n) % n;
            return SetInput(input, def.Choice(k));
        }

        public bool SetState(int block, int state)
        {
            if (!CanChangeBlock(block)) return false;
            int count = Puzzle.Blocks[block].Kind.StateCount;
            state = ((state % count) + count) % count;
            if (_states[block] == state) return false;
            _undo.Push((ChangeKind.Block, block, _states[block]));
            _states[block] = state;
            Moves++;
            Settle();
            Changed?.Invoke(ChangeKind.Block, block);
            return true;
        }

        /// <summary>Steps a block to its next (+1) or previous (-1) state, wrapping round.</summary>
        public bool CycleState(int block, int direction) => CanChangeBlock(block) && SetState(block, _states[block] + direction);

        public bool CanUndo => _undo.Count > 0;

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var (kind, index, previous) = _undo.Pop();
            if (kind == ChangeKind.Input) _inputs[index] = previous;
            else _states[index] = previous;
            Moves++;
            Settle();
            Changed?.Invoke(kind, index);
            return true;
        }

        /// <summary>Puts every input and block back where the circuit started. Probes stay.</summary>
        public void ResetToStart()
        {
            for (int i = 0; i < _inputs.Length; i++) _inputs[i] = Puzzle.Inputs[i].Value;
            for (int b = 0; b < _states.Length; b++) _states[b] = Puzzle.Blocks[b].State;
            _undo.Clear();
            Settle();
            Reset?.Invoke();
        }

        // ------------------------------------------------------------------ probes

        public IReadOnlyList<Probe> Probes => _probes;
        public int ProbesLeft => Puzzle.Probes - _probes.Count;

        /// <summary>The value a probe reads: the signal its wire carries.</summary>
        public int ProbeValue(int probe) => _values[Circuit.NodeOf(Puzzle.Wires[_probes[probe].Wire].From)];

        /// <summary>The probe has a reading to show: it was there for a run, and nothing has changed since.</summary>
        public bool ProbeShown(int probe) => HasRun && probe >= 0 && probe < _probes.Count && _probes[probe].Read;

        public bool AddProbe(int wire, float at)
        {
            if (ProbesLeft <= 0 || wire < 0 || wire >= Puzzle.Wires.Count) return false;
            _probes.Add(new Probe(wire, Clamp01(at)));
            ProbesChanged?.Invoke();
            return true;
        }

        public bool RemoveProbe(int probe)
        {
            if (probe < 0 || probe >= _probes.Count) return false;
            _probes.RemoveAt(probe);
            ProbesChanged?.Invoke();
            return true;
        }

        public void ClearProbes()
        {
            if (_probes.Count == 0) return;
            _probes.Clear();
            ProbesChanged?.Invoke();
        }

        // ------------------------------------------------------------------ saving

        /// <summary>Where the player is, for the save file.</summary>
        public CircuitState Snapshot()
        {
            var state = new CircuitState { Inputs = CopyInputs(), States = CopyStates() };
            foreach (var p in _probes) state.Probes.Add((p.Wire, (int)Math.Round(p.At * 1000f)));
            return state;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
