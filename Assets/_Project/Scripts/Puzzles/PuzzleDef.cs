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

        /// <summary>In a circuit the player wires: the signal this input sends, of any type, steady or a pattern.</summary>
        public SignalSpec Signal;
    }

    /// <summary>A block with a hidden rule. Its target, if any, is on the signal it sends.</summary>
    public sealed class BlockDef : NodeDef
    {
        public BlockKind Kind;

        /// <summary>
        /// What the player sees on the block: K1, K2 and so on, counted down each
        /// column of the board in turn (the order the file lists them). It says
        /// nothing about the block's kind, so K1 is a different block in every
        /// circuit. Hints name blocks as {id}, which the loader turns into this.
        /// </summary>
        public string Label => "K" + (Index + 1);
        /// <summary>The state the block starts in.</summary>
        public int State;
        /// <summary>A locked block keeps its state. It is drawn with a dashed outline and a padlock.</summary>
        public bool Locked;
    }

    /// <summary>A readout on the right of the board.</summary>
    public sealed class OutputDef : NodeDef
    {
        /// <summary>In a circuit the player wires: the signal that must arrive, worked out from the designer's wiring.</summary>
        public Trace Want;

        /// <summary>
        /// What the output is. A plain display unless the file says otherwise. The
        /// target is met when the device does what it would with <see cref="Want"/>.
        /// </summary>
        public Device Device = Device.Display;
    }

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

        /// <summary>
        /// The player wires this circuit: it starts with inputs, blocks and outputs
        /// and no wires, and is solved when every output gets its wanted signal,
        /// every block is used and every block input is connected. Inputs carry
        /// signals of any type, and blocks keep the state the file gives them.
        /// </summary>
        public bool Wiring;


        /// <summary>A wired circuit's intended wiring, which its outputs' wanted signals come from. Never shown.</summary>
        public readonly List<WireDef> Solution = new List<WireDef>();
        /// <summary>
        /// Wired circuits: the wires already in place when the circuit is first
        /// opened, for teaching. The player can move or remove them like any other.
        /// Usually empty.
        /// </summary>
        public readonly List<WireDef> Start = new List<WireDef>();

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

        /// <summary>The player wires this chapter's circuits.</summary>
        public bool Wiring;
        /// <summary>A wired chapter with no one medium: its signals are of every type.</summary>
        public bool Mixed;

        /// <summary>The part of the game the chapter belongs to.</summary>
        public PartDef Part;

        /// <summary>The chapter that must be played first, or null for the first chapter.</summary>
        public string UnlockAfter;
        /// <summary>How many circuits of <see cref="UnlockAfter"/> must be solved.</summary>
        public int UnlockSolved;

        public readonly List<PuzzleDef> Puzzles = new List<PuzzleDef>();

        public override string ToString() => $"Chapter {Numeral} · {Title}";
    }

    /// <summary>
    /// A part of the game: a run of chapters. The first part teaches each signal
    /// and block, the second how signals combine, and the last is the main game.
    /// A part can wait for the one before it to be finished.
    /// </summary>
    public sealed class PartDef
    {
        public string Id;
        public string Title;
        /// <summary>One line about what the part is for.</summary>
        public string Tagline;
        /// <summary>Position in the game, from 0.</summary>
        public int Number;
        /// <summary>The part opens only once every circuit in the part before it is solved.</summary>
        public bool AfterPrevious;

        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();

        public override string ToString() => $"Part {Number + 1} · {Title}";
    }
}
