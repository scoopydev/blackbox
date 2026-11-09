using System;
using System.Collections.Generic;
using Blackbox.Circuits;

namespace Blackbox.Puzzles
{
    /// <summary>
    /// One attempt at one circuit: the current box settings, the last test run and
    /// an undo history. Has no Unity dependencies, so the whole play loop can be
    /// tested without the engine. The board view listens to its events.
    /// </summary>
    public sealed class PuzzleSession
    {
        readonly int[] _settings;
        readonly Stack<(int box, int previous)> _undo = new Stack<(int, int)>();

        public PuzzleDef Puzzle { get; }
        public CompiledCircuit Circuit { get; }

        /// <summary>The most recent test run, which may be out of date if settings changed since.</summary>
        public RunResult LastRun { get; private set; }

        /// <summary>How many times the player has pressed Run on this attempt.</summary>
        public int Tests { get; private set; }

        /// <summary>True once any run on this attempt matched every case.</summary>
        public bool EverSolved { get; private set; }

        /// <summary>A box's setting changed: (box index).</summary>
        public event Action<int> SettingChanged;
        /// <summary>A test run finished.</summary>
        public event Action<RunResult> Ran;
        /// <summary>The settings were reset, or undo restored a box.</summary>
        public event Action Reset;

        public PuzzleSession(PuzzleDef puzzle, int[] resumeSettings = null)
        {
            Puzzle = puzzle;
            Circuit = new CompiledCircuit(puzzle);
            _settings = puzzle.StartSettings();

            if (resumeSettings != null && resumeSettings.Length == _settings.Length)
            {
                for (int b = 0; b < _settings.Length; b++)
                {
                    var box = puzzle.Boxes[b];
                    if (!box.Locked && resumeSettings[b] >= 0 && resumeSettings[b] < box.Kind.SettingCount)
                        _settings[b] = resumeSettings[b];
                }
            }
        }

        public int SettingOf(int box) => _settings[box];
        public int[] CopySettings() => (int[])_settings.Clone();

        public bool CanChange(int box) => box >= 0 && box < _settings.Length && !Puzzle.Boxes[box].Locked;

        /// <summary>True when the last run used exactly the current settings.</summary>
        public bool RunIsCurrent
        {
            get
            {
                if (LastRun == null) return false;
                for (int b = 0; b < _settings.Length; b++)
                    if (LastRun.Settings[b] != _settings[b]) return false;
                return true;
            }
        }

        /// <summary>The circuit is solved right now: the latest run is current and matched everything.</summary>
        public bool SolvedNow => RunIsCurrent && LastRun.AllMatched;

        public bool AtStart
        {
            get
            {
                for (int b = 0; b < _settings.Length; b++)
                    if (_settings[b] != Puzzle.Boxes[b].Setting) return false;
                return true;
            }
        }

        public bool SetSetting(int box, int value)
        {
            if (!CanChange(box)) return false;
            int count = Puzzle.Boxes[box].Kind.SettingCount;
            value = ((value % count) + count) % count;
            if (_settings[box] == value) return false;
            _undo.Push((box, _settings[box]));
            _settings[box] = value;
            SettingChanged?.Invoke(box);
            return true;
        }

        /// <summary>Steps a box to its next (+1) or previous (-1) setting, wrapping around.</summary>
        public bool Cycle(int box, int direction) => CanChange(box) && SetSetting(box, _settings[box] + direction);

        public bool CanUndo => _undo.Count > 0;

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var (box, previous) = _undo.Pop();
            _settings[box] = previous;
            SettingChanged?.Invoke(box);
            return true;
        }

        /// <summary>Puts every box back where the circuit started and forgets the last run.</summary>
        public void ResetToStart()
        {
            for (int b = 0; b < _settings.Length; b++) _settings[b] = Puzzle.Boxes[b].Setting;
            _undo.Clear();
            LastRun = null;
            Reset?.Invoke();
        }

        public RunResult Run()
        {
            Tests++;
            LastRun = Circuit.Run(_settings);
            if (LastRun.AllMatched) EverSolved = true;
            Ran?.Invoke(LastRun);
            return LastRun;
        }
    }
}
