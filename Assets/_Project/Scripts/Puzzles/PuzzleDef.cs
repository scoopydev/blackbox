using System.Collections.Generic;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    /// <summary>How a circuit is tested.</summary>
    public enum PuzzleMode
    {
        /// <summary>Each case is one row of a truth table: one tick, inputs in, outputs out.</summary>
        Table,
        /// <summary>Each case is a trace: inputs change over several ticks and outputs are watched over time.</summary>
        Trace,
    }

    public enum NodeRole { Input, Box, Output }

    /// <summary>A point on the board, in grid units. Columns run left to right, rows top to bottom.</summary>
    public struct GridPoint
    {
        public float Col;
        public float Row;
        public GridPoint(float col, float row) { Col = col; Row = row; }
        public override string ToString() => $"({Col}, {Row})";
    }

    public class NodeDef
    {
        public string Id;
        public NodeRole Role;
        /// <summary>Position on the board. Filled in by <see cref="BoardLayout"/> when the file leaves it out.</summary>
        public GridPoint At;
        public bool HasPosition;
        /// <summary>Index within its role (input 0, 1, ...; box 0, 1, ...; output 0, 1, ...).</summary>
        public int Index;
    }

    public sealed class BoxDef : NodeDef
    {
        public ComponentKind Kind;
        /// <summary>The setting the box starts on.</summary>
        public int Setting;
        /// <summary>A locked box cannot be changed. It is drawn with a dashed outline.</summary>
        public bool Locked;
    }

    public sealed class WireDef
    {
        public NodeDef From;
        public NodeDef To;
        /// <summary>Input port on <see cref="To"/>. 0 is the top port.</summary>
        public int ToPort;
        /// <summary>For wires that run backwards (loops): the board row the return path runs along.</summary>
        public float? ViaRow;
    }

    /// <summary>
    /// One test the circuit must pass. A table row is a case with one tick.
    /// </summary>
    public sealed class CaseDef
    {
        public int Ticks;
        /// <summary>[tick][input]</summary>
        public bool[][] Inputs;
        /// <summary>[tick][output]: 0, 1, or -1 when the output doesn't matter on that tick.</summary>
        public sbyte[][] Want;

        public bool Cares(int tick, int output) => Want[tick][output] >= 0;
        public bool Wanted(int tick, int output) => Want[tick][output] == 1;
    }

    public sealed class PuzzleDef
    {
        public string Id;
        public string Name;
        /// <summary>An optional quiet line about the controls. Never about what boxes do.</summary>
        public string Hint;
        public PuzzleMode Mode;

        public readonly List<NodeDef> Inputs = new List<NodeDef>();
        public readonly List<BoxDef> Boxes = new List<BoxDef>();
        public readonly List<NodeDef> Outputs = new List<NodeDef>();
        public readonly List<WireDef> Wires = new List<WireDef>();
        public readonly List<CaseDef> Cases = new List<CaseDef>();

        /// <summary>Set when the puzzle is part of a chapter.</summary>
        public ChapterDef Chapter;
        /// <summary>Position within the chapter, from 0.</summary>
        public int Number;

        public IEnumerable<NodeDef> AllNodes
        {
            get
            {
                foreach (var n in Inputs) yield return n;
                foreach (var n in Boxes) yield return n;
                foreach (var n in Outputs) yield return n;
            }
        }

        public NodeDef Find(string id)
        {
            foreach (var n in AllNodes)
                if (n.Id == id) return n;
            return null;
        }

        public int[] StartSettings()
        {
            var settings = new int[Boxes.Count];
            for (int i = 0; i < Boxes.Count; i++) settings[i] = Boxes[i].Setting;
            return settings;
        }

        /// <summary>"Circuit 03"</summary>
        public string Label => $"{Number + 1:00}";

        public override string ToString() => $"{Id} {Name}";
    }

    /// <summary>A set of circuits that share a theme and get harder as they go.</summary>
    public sealed class ChapterDef
    {
        public string Id;
        /// <summary>"I", "II", ...</summary>
        public string Numeral;
        public string Title;
        /// <summary>One line shown when the chapter opens.</summary>
        public string Tagline;
        /// <summary>Position in the game, from 0.</summary>
        public int Number;

        /// <summary>The chapter that must be played first, or null for the first chapter.</summary>
        public string UnlockAfter;
        /// <summary>How many circuits of <see cref="UnlockAfter"/> must be solved.</summary>
        public int UnlockSolved;

        public readonly List<PuzzleDef> Puzzles = new List<PuzzleDef>();

        public override string ToString() => $"Chapter {Numeral} · {Title}";
    }
}
