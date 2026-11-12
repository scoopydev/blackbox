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
        /// <summary>
        /// In a circuit the player wires: drawn with a dotted outline, and may be left
        /// out. Inputs are optional unless the circuit marks one required: a solid
        /// input has to lead to an output. Blocks are required unless the circuit
        /// marks one optional; the answer may use an optional block or leave it out,
        /// so a dotted block doesn't say whether it's needed. Outputs are never optional.
        /// </summary>
        public bool Optional;
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
        /// What the player sees on the block: the kind of signal it sends, B for a
        /// bit, D for a digit and W for a wave, numbered per letter down each column
        /// of the board in turn, such as B1, B2, D1. It says what comes out, never
        /// which rule makes it, so B1 is a different block in every circuit. Hints
        /// name blocks as {id}, which the loader turns into this. Set by
        /// <see cref="PuzzleLoader.LabelBlocks"/>; "K" and the index until then.
        /// </summary>
        public string Label
        {
            get => _label ?? "K" + (Index + 1);
            set => _label = value;
        }
        string _label;
        /// <summary>The state the block starts in.</summary>
        public int State;
        /// <summary>A locked block keeps its state. It is drawn with a dashed outline and a padlock.</summary>
        public bool Locked;
        /// <summary>
        /// A listen-only block in a wired circuit: what it sends is never drawn, nor
        /// is anything wired after it, block or output, until the circuit is solved:
        /// those are only heard when clicked (see <see cref="PuzzleDef.Heard(int, int[][])"/>).
        /// A speaker beside its label marks it out.
        /// </summary>
        public bool Listen;
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
        /// <summary>
        /// What the circuit is called: its chapter's title and its number in the
        /// chapter, such as "TALLY 1". A circuit read on its own keeps its file's
        /// "name", if it has one, or else its id.
        /// </summary>
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

        /// <summary>No signal in the circuit is drawn, not even a target: every one is only heard. See <see cref="ChapterDef.Dark"/>.</summary>
        public bool Dark => Chapter != null && Chapter.Dark;

        /// <summary>
        /// True when the signal a node sends is heard but never drawn, whatever the
        /// wiring: every node in a dark circuit, and a listen-only block. Node numbers
        /// are inputs, then blocks, then outputs, as in <see cref="WiringSession"/>.
        /// </summary>
        public bool Heard(int node) => Dark || (node >= Inputs.Count && node < Inputs.Count + Blocks.Count && Blocks[node - Inputs.Count].Listen);

        /// <summary>
        /// True when, wired as <paramref name="driver"/> says (the driver of each port
        /// of each node, as <see cref="WiringSession.Drivers"/> gives it), a node's
        /// signal is heard but not drawn: <see cref="Heard(int)"/>, or anything wired
        /// after a listen-only block, any number of blocks on, outputs included. The
        /// board draws them all once the circuit is solved, unless it's dark.
        /// </summary>
        public bool Heard(int node, int[][] driver)
        {
            if (Heard(node)) return true;
            if (driver == null || node < Inputs.Count || node >= driver.Length) return false;
            foreach (int d in driver[node])
                if (d >= 0 && d != node && Heard(d, driver)) return true;
            return false;
        }

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
    /// <summary>
    /// The page shown after a chapter's title card, before its first circuit,
    /// that points out what to look for in its signals. By default a chapter of
    /// bits, digits or waves explains that kind, and a chapter of every kind
    /// shows how each kind of block reads the others.
    /// </summary>
    public enum SignalGuide
    {
        None,
        /// <summary>A bit's line above or below the middle, steady or switching.</summary>
        Bits,
        /// <summary>A steady digit: a whole number from 0 to 9, shown as just its number, a higher digit sounding higher.</summary>
        Digits,
        /// <summary>A digit that changes through the loop: a line whose height is the digit, each step's digit written on it.</summary>
        ChangingDigits,
        /// <summary>A wave's frequency, amplitude, form and phase.</summary>
        Waves,
        /// <summary>One signal of each kind, as each kind of block reads it.</summary>
        Readings,
        /// <summary>All three kinds side by side, with what to look for in each.</summary>
        Recap,
        /// <summary>A listen-only block: how it's marked, and how each kind of signal sounds.</summary>
        Listening,
        /// <summary>A dark chapter: nothing is drawn, so how to play by ear, and what can't be heard.</summary>
        Dark,
    }

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
        /// <summary>The page that shows the chapter's signals before its first circuit.</summary>
        public SignalGuide Guide;
        /// <summary>Position in the game, from 0.</summary>
        public int Number;

        /// <summary>The player wires this chapter's circuits.</summary>
        public bool Wiring;
        /// <summary>A wired chapter with no one medium: its signals are of every type.</summary>
        public bool Mixed;
        /// <summary>
        /// A wired chapter played by ear: no signal is drawn anywhere in its
        /// circuits (inputs, blocks, outputs or targets), only a speaker that plays
        /// it. Whether each output matches its target still shows.
        /// </summary>
        public bool Dark;

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
