using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Board
{
    public enum BoardItem { Input, Block, Output, Wire, Probe }

    /// <summary>What the board tells the screen that owns it.</summary>
    public interface IBoardHost
    {
        /// <summary>Step an input up (+1) or down (-1).</summary>
        void InputStep(int input, int direction);
        /// <summary>Step a block to its next (+1) or previous (-1) state.</summary>
        void BlockStep(int block, int direction);
        /// <summary>A block's state dot was clicked.</summary>
        void BlockPip(int block, int state);
        /// <summary>A wire was clicked <paramref name="at"/> this fraction of its length.</summary>
        void WireClicked(int wire, float at);
        void ProbeClicked(int probe);
        /// <summary>A value was clicked: plays it, for waves.</summary>
        void PlayValue(int value);
        void Hover(BoardItem item, int index, bool on);
        /// <summary>True when clicking a wire would place a probe.</summary>
        bool CanPlaceProbe { get; }
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
        public static Rect Area = new Rect(90f, 222f, 1740f, 648f);

        public static BoardMetrics Fit(PuzzleDef p) => Fit(p, Area);

        public static BoardMetrics Fit(PuzzleDef p, Rect area)
        {
            BoardLayout.Bounds(p, out float minCol, out float maxCol, out float minRow, out float maxRow);
            var m = new BoardMetrics();
            float colSpan = Mathf.Max(1f, maxCol - minCol);
            float rowSpan = Mathf.Max(0f, maxRow - minRow);
            m.ColSpacing = Mathf.Min(400f, (area.width - 330f) / colSpan);
            m.RowSpacing = rowSpan > 0f ? Mathf.Min(180f, (area.height - 170f) / rowSpan) : 180f;
            m.Scale = Mathf.Clamp(m.RowSpacing / 150f, 0.74f, 1f);
            m._midCol = (minCol + maxCol) * 0.5f;
            m._midRow = (minRow + maxRow) * 0.5f;
            // Frame coordinates are centred on the screen, y up. Outputs are wider
            // than inputs, so the grid sits a little left of the area's centre.
            m.Centre = new Vector2(area.center.x - VoidFrame.Width * 0.5f - 40f * m.Scale, VoidFrame.Height * 0.5f - area.center.y);
            return m;
        }

        public Vector2 Position(GridPoint g) => new Vector2(Centre.x + (g.Col - _midCol) * ColSpacing, Centre.y - (g.Row - _midRow) * RowSpacing);
    }

    /// <summary>
    /// The circuit on screen: inputs, blocks, outputs, the wires between them and
    /// the probes on the wires. Wires never show their values; only probes,
    /// inputs and outputs do. A change sends a pulse of light downstream from
    /// whatever changed, so the player can see what it reaches. With
    /// <see cref="HideUntilRun"/>, outputs, probes and target marks show only what
    /// the latest run found.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public PuzzleSession Session { get; private set; }
        public PuzzleDef Puzzle => Session.Puzzle;
        public CompiledCircuit Circuit => Session.Circuit;
        public BoardMetrics Metrics { get; private set; }
        public readonly List<InputView> Inputs = new List<InputView>();
        public readonly List<BlockView> Blocks = new List<BlockView>();
        public readonly List<OutputView> Outputs = new List<OutputView>();
        public readonly List<WireView> Wires = new List<WireView>();
        public readonly List<ProbeView> Probes = new List<ProbeView>();

        IBoardHost _host;
        RectTransform _wireLayer, _probeLayer, _nodeLayer;
        bool _solved;
        bool[] _targetWasMatched;

        public IBoardHost Host => _host;

        /// <summary>No sounds, for the title screen's idle circuit.</summary>
        public bool Quiet;

        /// <summary>
        /// Outputs, probe readings and target marks show only after a run, as on the
        /// circuit screen. Off for the title screen's idle circuit, which is always live.
        /// </summary>
        public bool HideUntilRun { get; private set; }

        public static BoardView Create(Transform parent, PuzzleSession session, IBoardHost host, BoardMetrics metrics = null, bool hideUntilRun = false)
        {
            var rt = UIKit.Rect(parent, "Board").Stretch();
            var board = rt.gameObject.AddComponent<BoardView>();
            board.HideUntilRun = hideUntilRun;
            board.Build(session, host, metrics ?? BoardMetrics.Fit(session.Puzzle));
            return board;
        }

        void Build(PuzzleSession session, IBoardHost host, BoardMetrics metrics)
        {
            Session = session;
            _host = host;
            Metrics = metrics;
            var p = session.Puzzle;
            var root = (RectTransform)transform;
            _wireLayer = UIKit.Rect(root, "Wires").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            _nodeLayer = UIKit.Rect(root, "Nodes").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            _probeLayer = UIKit.Rect(root, "Probes").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);

            foreach (var input in p.Inputs) Inputs.Add(InputView.Create(_nodeLayer, input, p.Medium, Metrics, host));
            foreach (var block in p.Blocks) Blocks.Add(BlockView.Create(_nodeLayer, block, p.Medium, Metrics, host));
            foreach (var output in p.Outputs) Outputs.Add(OutputView.Create(_nodeLayer, output, p.Medium, Metrics, host));

            var router = new WireRouter(this);
            foreach (var w in p.Wires)
            {
                var pts = router.Route(w);
                Wires.Add(WireView.Create(_wireLayer, w, pts, this));
            }

            _targetWasMatched = new bool[p.Targets.Count];
            Refresh(false);
            RebuildProbes();
        }

        /// <summary>Results can be shown: the circuit has run since its last change, or it is always live.</summary>
        public bool Shown => !HideUntilRun || Session.HasRun;

        // ------------------------------------------------------------------ geometry

        public Vector2 SourcePort(NodeDef n) => n.Role == NodeRole.Input ? Inputs[n.Index].OutPort : Blocks[n.Index].OutPort;

        public Vector2 TargetPort(NodeDef n, int port) => n.Role == NodeRole.Output ? Outputs[n.Index].InPort : Blocks[n.Index].InPort(port);

        /// <summary>The vertical span a node covers on screen, with everything drawn around it.</summary>
        public void Extent(NodeDef n, out float x0, out float x1, out float yLow, out float yHigh)
        {
            switch (n.Role)
            {
                case NodeRole.Input:
                {
                    var v = Inputs[n.Index];
                    x0 = v.Centre.x - v.Radius - 40f; x1 = v.Centre.x + v.Radius;
                    yLow = v.Centre.y - v.Reach; yHigh = v.Centre.y + v.Reach;
                    break;
                }
                case NodeRole.Block:
                {
                    var v = Blocks[n.Index];
                    x0 = v.Centre.x - v.Width * 0.5f; x1 = v.Centre.x + v.Width * 0.5f;
                    yLow = v.Centre.y - v.ReachDown; yHigh = v.Centre.y + v.ReachUp;
                    break;
                }
                default:
                {
                    var v = Outputs[n.Index];
                    x0 = v.InPort.x; x1 = v.InPort.x + v.Width;
                    yLow = v.Centre.y - v.Height * 0.5f; yHigh = v.Centre.y + v.Height * 0.5f + 26f;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ showing state

        /// <summary>
        /// Shows the session as it is now: every input and block, and the outputs,
        /// target marks and probe readings when they can be shown. Returns how many
        /// targets became matched since the last refresh.
        /// </summary>
        public int Refresh(bool animate)
        {
            var s = Session;
            var p = Puzzle;
            bool shown = Shown;
            for (int i = 0; i < Inputs.Count; i++) Inputs[i].SetValue(s.InputValue(i), false);
            for (int b = 0; b < Blocks.Count; b++) Blocks[b].SetState(s.State(b), false);

            int newlyMatched = 0;
            for (int t = 0; t < p.Targets.Count; t++)
            {
                bool now = shown && s.TargetMatched(t);
                if (now && !_targetWasMatched[t]) newlyMatched++;
                _targetWasMatched[t] = now;
                bool? result = shown ? now : (bool?)null;
                var node = p.Targets[t].Node;
                if (node is InputDef inp) Inputs[inp.Index].Target?.SetResult(result);
                else if (node is BlockDef blk) Blocks[blk.Index].Target?.SetResult(result);
            }
            for (int o = 0; o < Outputs.Count; o++)
            {
                if (shown) ShowOutput(o, animate);
                else Outputs[o].SetUnknown();
            }
            for (int k = 0; k < Probes.Count; k++) ShowProbe(k, animate);
            foreach (var w in Wires) w.Probed = false;
            foreach (var probe in s.Probes) Wires[probe.Wire].Probed = true;
            return newlyMatched;
        }

        /// <summary>Debug: writes what every block does under it, or takes the words away.</summary>
        public void SetShowRules(bool on)
        {
            foreach (var b in Blocks) b.ShowRule = on;
        }

        /// <summary>Hides every output, probe reading and target mark, for the start of a run.</summary>
        public void Conceal()
        {
            foreach (var o in Outputs) o.SetUnknown();
            foreach (var probe in Probes) probe.SetValue(-1, false);
            for (int t = 0; t < Puzzle.Targets.Count; t++)
            {
                _targetWasMatched[t] = false;
                var node = Puzzle.Targets[t].Node;
                if (node is InputDef inp) Inputs[inp.Index].Target?.SetResult(null);
                else if (node is BlockDef blk) Blocks[blk.Index].Target?.SetResult(null);
            }
        }

        /// <summary>Shows what an output receives now, whether or not the circuit has run: for revealing it during a run.</summary>
        public void ShowOutput(int o, bool flash)
        {
            var def = Puzzle.Outputs[o];
            int value = Session.ValueOf(def);
            Outputs[o].SetValue(value, def.Target.HasValue && value == def.Target.Value, flash);
        }

        /// <summary>Shows a probe's reading if it has one, or a ?.</summary>
        public void ShowProbe(int k, bool flash, bool force = false)
        {
            if (k < 0 || k >= Probes.Count || k >= Session.Probes.Count) return;
            bool known = !HideUntilRun || force || Session.ProbeShown(k);
            Probes[k].SetValue(known ? Session.ProbeValue(k) : -1, flash && known);
        }

        /// <summary>The circuit's depth: how many steps the signal takes to reach the furthest output.</summary>
        public int Depth
        {
            get
            {
                int depth = 1;
                foreach (var o in Puzzle.Outputs) depth = Mathf.Max(depth, Circuit.Depth[Circuit.NodeOf(o)]);
                return depth;
            }
        }

        /// <summary>When the signal reaches a node on a run, in steps from the start.</summary>
        public int StepOf(NodeDef n) => Circuit.Depth[Circuit.NodeOf(n)];

        /// <summary>Lights the wires downstream of a node, one column after another.</summary>
        public void Pulse(int node)
        {
            if (App.ReduceMotion) return;
            var down = Circuit.Downstream(node);
            int baseDepth = Circuit.Depth[node];
            foreach (var w in Wires)
            {
                int from = Circuit.NodeOf(w.Def.From);
                if (from != node && !down[from]) continue;
                float delay = Mathf.Max(0, Circuit.Depth[from] - baseDepth) * 0.12f;
                w.Pulse(delay);
            }
        }

        public void RebuildProbes()
        {
            foreach (var probe in Probes) if (probe != null) Destroy(probe.gameObject);
            Probes.Clear();
            var s = Session;
            for (int k = 0; k < s.Probes.Count; k++)
            {
                var pr = s.Probes[k];
                var view = ProbeView.Create(_probeLayer, k, Wires[pr.Wire], pr.At, Puzzle.Medium, this);
                Probes.Add(view);
                ShowProbe(k, false);
            }
            foreach (var w in Wires) w.Probed = false;
            foreach (var probe in s.Probes) Wires[probe.Wire].Probed = true;
            if (_solved) foreach (var probe in Probes) probe.Solved = true;
        }

        public bool Solved => _solved;

        /// <summary>The whole circuit lights up when it is solved.</summary>
        public void SetSolved(bool solved)
        {
            if (_solved == solved) return;
            _solved = solved;
            foreach (var w in Wires) w.SetCelebrate(solved, solved ? Circuit.Depth[Circuit.NodeOf(w.Def.From)] * 0.08f : 0f);
            foreach (var v in Inputs) v.Solved = solved;
            foreach (var v in Blocks) v.Solved = solved;
            foreach (var v in Outputs) v.Solved = solved;
            foreach (var v in Probes) v.Solved = solved;
        }
    }

    /// <summary>
    /// Plans each wire's path. Wires between neighbouring columns are smooth
    /// curves. Longer wires leave their source, run along a free lane between the
    /// rows of the columns they cross, and drop into their target, so they never
    /// pass behind a block.
    /// </summary>
    public sealed class WireRouter
    {
        readonly BoardView _board;

        public WireRouter(BoardView board) { _board = board; }

        public List<Vector2> Route(WireDef w)
        {
            var pts = new List<Vector2>();
            Vector2 from = _board.SourcePort(w.From);
            Vector2 to = _board.TargetPort(w.To, w.ToPort);
            float fromCol = w.From.At.Col, toCol = w.To.At.Col;
            float spacing = _board.Metrics.ColSpacing;

            // Columns the wire passes over, and what sits in them.
            var blockers = new List<(float low, float high)>();
            foreach (var n in _board.Puzzle.AllNodes)
            {
                if (n == w.From || n == w.To) continue;
                if (n.At.Col <= fromCol + 0.01f || n.At.Col >= toCol - 0.01f) continue;
                _board.Extent(n, out _, out _, out float low, out float high);
                blockers.Add((low - 14f, high + 14f));
            }

            if (blockers.Count == 0 && to.x > from.x + 24f)
            {
                MeshKit.WireCurve(pts, from, to);
                return pts;
            }

            // Pick the free lane with the least climbing, then the most room around it.
            float best = from.y, bestCost = float.MaxValue;
            float step = _board.Metrics.RowSpacing * 0.125f;
            float lo = Mathf.Min(from.y, to.y) - _board.Metrics.RowSpacing * 1.5f;
            float hi = Mathf.Max(from.y, to.y) + _board.Metrics.RowSpacing * 1.5f;
            for (float y = lo; y <= hi + 0.01f; y += step)
            {
                float clearance = float.MaxValue;
                bool free = true;
                foreach (var (low, high) in blockers)
                {
                    if (y > low && y < high) { free = false; break; }
                    clearance = Mathf.Min(clearance, Mathf.Min(Mathf.Abs(y - low), Mathf.Abs(y - high)));
                }
                if (!free) continue;
                float climb = Mathf.Abs(from.y - y) + Mathf.Abs(y - to.y);
                float cost = climb - Mathf.Min(clearance, 60f) * 0.6f;
                if (cost < bestCost) { bestCost = cost; best = y; }
            }

            float xa = from.x + Mathf.Min(spacing * 0.3f, 90f);
            float xb = to.x - Mathf.Min(spacing * 0.3f, 90f);
            if (xb < xa + 10f) { xa = from.x + 30f; xb = Mathf.Max(xa + 10f, to.x - 30f); }
            var corners = new List<Vector2> { from };
            AddCorner(corners, new Vector2(xa, from.y));
            AddCorner(corners, new Vector2(xa, best));
            AddCorner(corners, new Vector2(xb, best));
            AddCorner(corners, new Vector2(xb, to.y));
            AddCorner(corners, to);
            if (corners.Count == 2)
            {
                pts.Add(from);
                pts.Add(to);
                return pts;
            }
            MeshKit.RoundedPolyline(pts, corners, 22f);
            return pts;
        }

        static void AddCorner(List<Vector2> corners, Vector2 p)
        {
            var last = corners[corners.Count - 1];
            if ((p - last).sqrMagnitude < 1f) return;
            // Drop the middle of three points in a straight line.
            if (corners.Count >= 2)
            {
                var prev = corners[corners.Count - 2];
                bool sameX = Mathf.Abs(prev.x - last.x) < 0.5f && Mathf.Abs(last.x - p.x) < 0.5f;
                bool sameY = Mathf.Abs(prev.y - last.y) < 0.5f && Mathf.Abs(last.y - p.y) < 0.5f;
                if (sameX || sameY) corners[corners.Count - 1] = p;
                else corners.Add(p);
                return;
            }
            corners.Add(p);
        }
    }
}
