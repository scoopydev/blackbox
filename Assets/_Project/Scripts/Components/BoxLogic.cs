using System;

namespace Blackbox.Components
{
    /// <summary>
    /// The hidden rule inside one box, for one setting.
    ///
    /// Time moves in ticks. Each tick the simulator asks every box for its output once.
    /// Most boxes answer from this tick's inputs (and, for boxes that remember, from
    /// their own state). <see cref="Registered"/> boxes answer only from state, so their
    /// output is known before their inputs are; that is what lets a wire loop back on
    /// itself without creating a paradox.
    /// </summary>
    public interface IBoxLogic
    {
        /// <summary>True when the output depends only on stored state (a delay line).</summary>
        bool Registered { get; }

        /// <summary>Forget everything. Called at the start of every case.</summary>
        void Reset();

        /// <summary>Registered boxes only: the output for the current tick, before inputs are known.</summary>
        bool Peek();

        /// <summary>
        /// Advance one tick. Non-registered boxes return their output for this tick.
        /// Registered boxes store the inputs and return the value <see cref="Peek"/> gave.
        /// </summary>
        bool Step(bool[] inputs);
    }

    /// <summary>A combinational rule written as a truth table.</summary>
    public sealed class TruthTableLogic : IBoxLogic
    {
        readonly bool[] _table;

        /// <param name="table">
        /// One character per input combination, '0' or '1'. Port 0 (the top port) is the
        /// most significant bit, so for two inputs the order is 00, 01, 10, 11.
        /// </param>
        public TruthTableLogic(string table)
        {
            _table = new bool[table.Length];
            for (int i = 0; i < table.Length; i++) _table[i] = table[i] == '1';
        }

        public bool Registered => false;
        public void Reset() { }
        public bool Peek() => false;

        public bool Step(bool[] inputs)
        {
            int index = 0;
            for (int i = 0; i < inputs.Length; i++) index = (index << 1) | (inputs[i] ? 1 : 0);
            return _table[index];
        }
    }

    /// <summary>True when at least <c>threshold</c> inputs are on.</summary>
    public sealed class ThresholdLogic : IBoxLogic
    {
        readonly int _threshold;
        public ThresholdLogic(int threshold) { _threshold = threshold; }
        public bool Registered => false;
        public void Reset() { }
        public bool Peek() => false;

        public bool Step(bool[] inputs)
        {
            int on = 0;
            foreach (bool b in inputs) if (b) on++;
            return on >= _threshold;
        }
    }

    /// <summary>Repeats its input <c>length</c> ticks later. Starts empty (all zeros).</summary>
    public sealed class DelayLogic : IBoxLogic
    {
        readonly bool[] _line;
        int _head;

        public DelayLogic(int length) { _line = new bool[Math.Max(1, length)]; }
        public bool Registered => true;

        public void Reset()
        {
            Array.Clear(_line, 0, _line.Length);
            _head = 0;
        }

        public bool Peek() => _line[_head];

        public bool Step(bool[] inputs)
        {
            bool output = _line[_head];
            _line[_head] = inputs[0];
            _head = (_head + 1) % _line.Length;
            return output;
        }
    }

    public enum EdgeMode { Change, Rising, Falling }

    /// <summary>Compares this tick's input with the previous tick's.</summary>
    public sealed class EdgeLogic : IBoxLogic
    {
        readonly EdgeMode _mode;
        bool _previous;

        public EdgeLogic(EdgeMode mode) { _mode = mode; }
        public bool Registered => false;
        public void Reset() { _previous = false; }
        public bool Peek() => false;

        public bool Step(bool[] inputs)
        {
            bool now = inputs[0];
            bool output;
            switch (_mode)
            {
                case EdgeMode.Rising: output = now && !_previous; break;
                case EdgeMode.Falling: output = !now && _previous; break;
                default: output = now != _previous; break;
            }
            _previous = now;
            return output;
        }
    }

    public enum ToggleMode { WhileOn, OnRise }

    /// <summary>Holds a bit and flips it when triggered. The flip shows on the same tick.</summary>
    public sealed class ToggleLogic : IBoxLogic
    {
        readonly ToggleMode _mode;
        bool _state;
        bool _previous;

        public ToggleLogic(ToggleMode mode) { _mode = mode; }
        public bool Registered => false;
        public void Reset() { _state = false; _previous = false; }
        public bool Peek() => _state;

        public bool Step(bool[] inputs)
        {
            bool now = inputs[0];
            bool trigger = _mode == ToggleMode.WhileOn ? now : now && !_previous;
            if (trigger) _state = !_state;
            _previous = now;
            return _state;
        }
    }

    /// <summary>
    /// Holds a bit. One port sets it, the other clears it, and when both are on it keeps
    /// what it had. <c>swapped</c> exchanges the two ports.
    /// </summary>
    public sealed class LatchLogic : IBoxLogic
    {
        readonly bool _swapped;
        bool _state;

        public LatchLogic(bool swapped) { _swapped = swapped; }
        public bool Registered => false;
        public void Reset() { _state = false; }
        public bool Peek() => _state;

        public bool Step(bool[] inputs)
        {
            bool set = _swapped ? inputs[1] : inputs[0];
            bool clear = _swapped ? inputs[0] : inputs[1];
            if (set && !clear) _state = true;
            else if (clear && !set) _state = false;
            return _state;
        }
    }
}
