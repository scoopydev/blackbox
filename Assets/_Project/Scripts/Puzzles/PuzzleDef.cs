using System.Collections.Generic;
using Blackbox.Components;

namespace Blackbox.Puzzles
{
    public enum NodeRole { Input, Block, Output }

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
        /// <summary>Index within its role (input 0, 1, ...; block 0, 1, ...; output 0, 1, ...).</summary>
        public int Index;
        /// <summary>The value this node must end on, if it has a target.</summary>
        public int? Target;
    }

    /// <summary>A signal source on the left of the board.</summary>
    public sealed class InputDef : NodeDef
    {
        /// <summary>The value it starts with.</summary>
        public int Value;
        /// <summary>True when the player may change it.</summary>
        public bool Adjustable;
        /// <summary>
        /// The values an adjustable input steps through, in order. Null means every
        /// value of the medium. Waves have hundreds, so their inputs list a few.
        /// </summary>
        public int[] Options;

        /// <summary>How many values the player can choose from.</summary>
        public int ChoiceCount(Medium m) => Options?.Length ?? Media.Size(m);

        /// <summary>The <paramref name="k"/>th value the player can choose.</summary>
        public int Choice(int k) => Options != null ? Options[k] : k;

        /// <summary>Where a value sits among the choices, or -1 if it isn't one.</summary>
        public int ChoiceIndex(Medium m, int value)
        {
            if (Options != null) return System.Array.IndexOf(Options, value);
            return Media.IsValid(m, value) ? value : -1;
        }
    }

    /// <summary>A block with a hidden rule. Its target, if any, is on the signal it sends.</summary>
    public sealed class BlockDef : NodeDef
    {
        public BlockKind Kind;
        /// <summary>The state the block starts in.</summary>
        public int State;
        /// <summary>A locked block keeps its state. It is drawn with a dashed outline and a padlock.</summary>
        public bool Locked;
    }

    /// <summary>A readout on the right of the board.</summary>
    public sealed class OutputDef : NodeDef { }

    public sealed class WireDef
    {
        public NodeDef From;
        public NodeDef To;
        /// <summary>Input port on <see cref="To"/>. 0 is the top port.</summary>
        public int ToPort;
        /// <summary>Position in <see cref="PuzzleDef.Wires"/>.</summary>
        public int Index;
    }

    /// <summary>One thing the player must make true: a node ending on a value.</summary>
    public sealed class TargetDef
    {
        public NodeDef Node;
        public int Want;
        public int Index;
    }

    public sealed class PuzzleDef
    {
        public string Id;
        public string Name;
        /// <summary>A line about the controls or what is new. Never about what blocks do.</summary>
        public string Hint;
        /// <summary>Overrides the goal sentence written from the targets.</summary>
        public string Goal;
        public Medium Medium;
        /// <summary>How many probes the player may place.</summary>
        public int Probes;

        public readonly List<InputDef> Inputs = new List<InputDef>();
        public readonly List<BlockDef> Blocks = new List<BlockDef>();
        public readonly List<OutputDef> Outputs = new List<OutputDef>();
        public readonly List<WireDef> Wires = new List<WireDef>();
        /// <summary>Every target: inputs first, then blocks, then outputs.</summary>
        public readonly List<TargetDef> Targets = new List<TargetDef>();

        /// <summary>Set when the puzzle is part of a chapter.</summary>
        public ChapterDef Chapter;
        /// <summary>Position within the chapter, from 0.</summary>
        public int Number;

        public IEnumerable<NodeDef> AllNodes
        {
            get
            {
                foreach (var n in Inputs) yield return n;
                foreach (var n in Blocks) yield return n;
                foreach (var n in Outputs) yield return n;
            }
        }

        public NodeDef Find(string id)
        {
            foreach (var n in AllNodes)
                if (n.Id == id) return n;
            return null;
        }

        public int[] StartInputs()
        {
            var values = new int[Inputs.Count];
            for (int i = 0; i < Inputs.Count; i++) values[i] = Inputs[i].Value;
            return values;
        }

        public int[] StartStates()
        {
            var states = new int[Blocks.Count];
            for (int b = 0; b < Blocks.Count; b++) states[b] = Blocks[b].State;
            return states;
        }

        /// <summary>True when the player can change anything about this input.</summary>
        public bool InputChangeable(int input) => Inputs[input].Adjustable;

        /// <summary>True when the player can change this block's state.</summary>
        public bool BlockChangeable(int block) => !Blocks[block].Locked && Blocks[block].Kind.StateCount > 1;

        /// <summary>"03"</summary>
        public string Label => $"{Number + 1:00}";

        public override string ToString() => $"{Id} {Name}";
    }

    /// <summary>A set of circuits on one medium that get harder as they go.</summary>
    public sealed class ChapterDef
    {
        public string Id;
        /// <summary>"I", "II", ...</summary>
        public string Numeral;
        public string Title;
        public Medium Medium;
        /// <summary>One line shown when the chapter opens.</summary>
        public string Tagline;
        /// <summary>What the signals in this chapter are, in a sentence, for the title card and help.</summary>
        public string Signals;
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
