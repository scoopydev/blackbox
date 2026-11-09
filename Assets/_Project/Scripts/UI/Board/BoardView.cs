using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>What the board tells the screen that owns it.</summary>
    public interface IBoardHost
    {
        void BoxClicked(int box);
        void BoxRightClicked(int box);
        void BoxScrolled(int box, int direction);
        void PipClicked(int box, int setting);
        void BoxHover(int box, bool hovering);
        RuleName NameOf(BoxDef box, int setting);
    }

    /// <summary>Maps the puzzle's grid onto the board area of the frame.</summary>
    public sealed class BoardMetrics
    {
        public float ColSpacing;
        public float RowSpacing;
        /// <summary>Node size multiplier, below 1 when rows are packed tightly.</summary>
        public float Scale;
        public Vector2 Centre;
        float _midCol, _midRow;

        /// <summary>The board area, in frame coordinates measured from the top-left.</summary>
        public static readonly Rect Area = new Rect(90f, 128f, 1740f, 636f);

        public static BoardMetrics Fit(PuzzleDef p)
        {
            BoardLayout.Bounds(p, out float minCol, out float maxCol, out float minRow, out float maxRow);
            var m = new BoardMetrics();
            float colSpan = Mathf.Max(1f, maxCol - minCol);
            float rowSpan = Mathf.Max(0f, maxRow - minRow);
            m.ColSpacing = Mathf.Min(420f, (Area.width - 260f) / colSpan);
            m.RowSpacing = rowSpan > 0f ? Mathf.Min(196f, (Area.height - 190f) / rowSpan) : 196f;
            m.Scale = Mathf.Clamp(m.RowSpacing / 176f, 0.78f, 1f);
            m._midCol = (minCol + maxCol) * 0.5f;
            m._midRow = (minRow + maxRow) * 0.5f;
            // Frame coordinates are centred on the screen, y up.
            m.Centre = new Vector2(Area.center.x - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - Area.center.y);
            return m;
        }

        public Vector2 Position(GridPoint g) => new Vector2(Centre.x + (g.Col - _midCol) * ColSpacing, Centre.y - (g.Row - _midRow) * RowSpacing);
        public float RowY(float row) => Centre.y - (row - _midRow) * RowSpacing;
    }

    /// <summary>
    /// The circuit on screen: inputs, boxes, outputs and the wires between them.
    /// It shows one moment of one case at a time and animates the signal front
    /// travelling from the inputs to the outputs.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public PuzzleDef Puzzle { get; private set; }
        public CompiledCircuit Circuit { get; private set; }
        public BoardMetrics Metrics { get; private set; }
        public readonly List<InputView> Inputs = new List<InputView>();
        public readonly List<BoxView> Boxes = new List<BoxView>();
        public readonly List<OutputView> Outputs = new List<OutputView>();
        public readonly List<WireView> Wires = new List<WireView>();

        IBoardHost _host;
        RectTransform _wireLayer, _nodeLayer;
        bool _stale;
        bool _solved;
        int _maxDepth;

        /// <summary>Seconds for the signal front to cross one column.</summary>
        public float StepTime = 0.16f;

        /// <summary>No arrival sounds, for the title screen's idle circuit.</summary>
        public bool Quiet;

        public static BoardView Create(Transform parent, PuzzleDef puzzle, CompiledCircuit circuit, IBoardHost host)
        {
            var rt = UIKit.Rect(parent, "Board").Stretch();
            var board = rt.gameObject.AddComponent<BoardView>();
            board.Build(puzzle, circuit, host);
            return board;
        }

        void Build(PuzzleDef puzzle, CompiledCircuit circuit, IBoardHost host)
        {
            Puzzle = puzzle;
            Circuit = circuit;
            _host = host;
            Metrics = BoardMetrics.Fit(puzzle);
            var root = (RectTransform)transform;
            _wireLayer = UIKit.Rect(root, "Wires");
            _wireLayer.Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            _nodeLayer = UIKit.Rect(root, "Nodes");
            _nodeLayer.Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);

            foreach (var input in puzzle.Inputs) Inputs.Add(InputView.Create(_nodeLayer, input, Metrics));
            foreach (var box in puzzle.Boxes) Boxes.Add(BoxView.Create(_nodeLayer, box, Metrics, host));
            foreach (var output in puzzle.Outputs) Outputs.Add(OutputView.Create(_nodeLayer, output, Metrics));

            foreach (var w in puzzle.Wires)
            {
                Vector2 from = SourcePort(w.From);
                Vector2 to = TargetPort(w.To, w.ToPort);
                var view = WireView.Create(_wireLayer, w, from, to, Metrics);
                view.FromNode = NodeIndex(w.From);
                Wires.Add(view);
            }

            _maxDepth = 0;
            foreach (int d in circuit.Depth) _maxDepth = Mathf.Max(_maxDepth, d);
            ShowDark();
        }

        int NodeIndex(NodeDef n)
        {
            switch (n.Role)
            {
                case NodeRole.Input: return n.Index;
                case NodeRole.Box: return Circuit.BoxNode(n.Index);
                default: return Circuit.OutputNode(n.Index);
            }
        }

        Vector2 SourcePort(NodeDef n)
        {
            if (n.Role == NodeRole.Input) return Inputs[n.Index].OutPort;
            return Boxes[n.Index].OutPort;
        }

        Vector2 TargetPort(NodeDef n, int port)
        {
            if (n.Role == NodeRole.Output) return Outputs[n.Index].InPort;
            return Boxes[n.Index].InPort(port);
        }

        /// <summary>How long a full propagation animation takes.</summary>
        public float PropagationTime => (_maxDepth + 1) * StepTime;

        /// <summary>Nothing has run: every wire dark, outputs blank.</summary>
        public void ShowDark()
        {
            _stale = false;
            foreach (var o in Outputs) Tweener.Kill(o, "arrive");
            foreach (var w in Wires) w.SetValue(false, false, 0f, 0f);
            foreach (var i in Inputs) i.SetValue(null);
            foreach (var b in Boxes) b.SetPorts(null, false, 1f);
            foreach (var o in Outputs) o.SetValue(null, -1, true, false);
        }

        /// <summary>Shows the inputs of a case before it runs, so the player can see what goes in.</summary>
        public void ShowInputsOnly(CaseDef cs, int tick)
        {
            _stale = false;
            foreach (var o in Outputs) Tweener.Kill(o, "arrive");
            foreach (var w in Wires) w.SetValue(false, false, 0f, 0f);
            foreach (var b in Boxes) b.SetPorts(null, false, 1f);
            for (int i = 0; i < Inputs.Count; i++) Inputs[i].SetValue(cs.Inputs[tick][i]);
            for (int o = 0; o < Outputs.Count; o++) Outputs[o].SetValue(null, cs.Want[tick][o], true, false);
        }

        /// <summary>
        /// Shows one tick of one case. With <paramref name="animate"/> the signal
        /// front sweeps across the board, column by column.
        /// </summary>
        public void Show(CaseResult result, int tick, bool animate, bool stale)
        {
            _stale = stale;
            float step = animate ? StepTime : 0f;
            var values = result.Values[tick];

            for (int i = 0; i < Inputs.Count; i++) Inputs[i].SetValue(values[i], stale);

            foreach (var w in Wires)
            {
                bool v = values[w.FromNode];
                float delay = Circuit.Depth[w.FromNode] * step;
                w.SetValue(v, _solved && !stale, delay, step, stale);
            }

            for (int b = 0; b < Boxes.Count; b++)
            {
                int node = Circuit.BoxNode(b);
                var ins = result.BoxInputs(b, tick);
                bool output = values[node];
                float delay = Circuit.Depth[node] * step;
                Boxes[b].SetPorts(ins, output, stale ? 0.3f : 1f, delay);
            }

            for (int o = 0; o < Outputs.Count; o++)
            {
                int node = Circuit.OutputNode(o);
                float delay = Circuit.Depth[node] * step;
                var view = Outputs[o];
                bool value = values[node];
                sbyte want = result.Case.Want[tick][o];
                bool ok = result.OutputOk(tick, o);
                if (delay <= 0f)
                {
                    Tweener.Kill(view, "arrive");
                    view.SetValue(value, want, ok, stale);
                }
                else
                {
                    view.SetValue(null, want, true, stale);
                    Tweener.After(view, "arrive", delay, () =>
                    {
                        view.SetValue(value, want, ok, stale);
                        view.Flash(!Quiet);
                    });
                }
            }
        }

        /// <summary>Dims the last result when the settings no longer match it.</summary>
        public void SetStale(bool stale)
        {
            if (_stale == stale) return;
            _stale = stale;
            foreach (var w in Wires) w.SetStale(stale);
            foreach (var b in Boxes) b.SetPortAlpha(stale ? 0.3f : 1f);
            foreach (var o in Outputs) o.SetStale(stale);
            foreach (var i in Inputs) i.SetStale(stale);
        }

        /// <summary>The whole circuit lights up when it is solved.</summary>
        public void SetSolved(bool solved)
        {
            if (_solved == solved) return;
            _solved = solved;
            foreach (var w in Wires) w.SetCelebrate(solved, solved ? Circuit.Depth[w.FromNode] * 0.07f : 0f);
            foreach (var b in Boxes) b.Solved = solved;
            foreach (var o in Outputs) o.Solved = solved;
        }

        public void RefreshNames()
        {
            foreach (var b in Boxes) b.RefreshName();
        }
    }
}
