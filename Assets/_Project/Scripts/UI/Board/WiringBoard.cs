using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blackbox.UI.Board
{
    /// <summary>What a wiring board needs from the screen that owns it.</summary>
    public interface IWiringHost
    {
        /// <summary>False while wires can't be drawn: during a run, or with a note tool out.</summary>
        bool CanWire { get; }
        /// <summary>A line for the status bar.</summary>
        void Say(string message, float seconds);
        /// <summary>Plays a signal's sound.</summary>
        void PlayTrace(Trace trace);
        /// <summary>The player moved a block, so where the blocks sit should be saved.</summary>
        void BlocksMoved();
    }

    /// <summary>
    /// A circuit the player wires. Every input has a dot on its right, every
    /// block dots on both sides, every output a dot on its left. Drag from one dot
    /// to another to run a wire (or click one dot, then the other). Dragging the
    /// end of a wire out of a block or output picks it up to move it; dropping it
    /// on nothing takes it off. Clicking a wire, or right-clicking a dot, takes
    /// wires off too. Dragging a block by its body moves it, and its wires
    /// follow; a block can't be dropped on top of another card.
    ///
    /// Outputs show what reaches them only after a run, beside their target, and
    /// each block shows what it sends, or why it sends nothing.
    /// </summary>
    public sealed class WiringBoard : MonoBehaviour
    {
        public WiringSession Session { get; private set; }
        public PuzzleDef Puzzle => Session.Puzzle;
        public BoardMetrics Metrics { get; private set; }
        public IWiringHost Host { get; private set; }

        public readonly List<WiredInputView> Inputs = new List<WiredInputView>();
        public readonly List<WiredBlockView> Blocks = new List<WiredBlockView>();
        public readonly List<WiredOutputView> Outputs = new List<WiredOutputView>();
        public readonly List<LinkView> Links = new List<LinkView>();
        readonly List<PortView> _ports = new List<PortView>();

        RectTransform _linkLayer, _nodeLayer, _dragLayer;
        PathGraphic _ghost;
        PortView _dragFrom, _hoverTarget;
        bool _dragging, _armed;
        Vector2 _pressAt;
        bool _solved;
        float _clock;
        bool _shown;

        /// <summary>Seconds for one loop of the signals on screen.</summary>
        public const float LoopSeconds = 2.4f;
        const float SnapReach = 30f;

        /// <param name="places">Where the player last left each block, by block id, in board coordinates. Missing blocks sit where the circuit puts them.</param>
        /// <param name="metrics">Where and how big to draw it; the circuit screen's board area when left out.</param>
        public static WiringBoard Create(Transform parent, WiringSession session, IWiringHost host, IDictionary<string, Vector2> places = null, BoardMetrics metrics = null)
        {
            var rt = UIKit.Rect(parent, "Wiring board").Stretch();
            var board = rt.gameObject.AddComponent<WiringBoard>();
            board.Build(session, host, places, metrics);
            return board;
        }

        void Build(WiringSession session, IWiringHost host, IDictionary<string, Vector2> places, BoardMetrics metrics)
        {
            Session = session;
            Host = host;
            Metrics = metrics ?? BoardMetrics.Fit(session.Puzzle);
            var root = (RectTransform)transform;

            // Clicking empty board puts down a wire held by a click.
            var area = BoardMetrics.Area;
            var back = UIKit.Hit(root, "Background");
            back.rectTransform.Centered(area.center.x - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - area.center.y, area.width, area.height);
            var backClick = back.gameObject.AddComponent<Clickable>();
            backClick.HoverSound = false;
            backClick.ClickSound = Sfx.Hover;
            backClick.Clicked = () => { if (_armed) CancelDrag(true); else ClearSelection(); };
            backClick.RightClicked = () => DeleteSelection();
            // Dragging on empty board draws a box: left selects the wires in it, right takes them off.
            back.gameObject.AddComponent<BoardBox>().Board = this;

            _linkLayer = UIKit.Rect(root, "Wires").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            _nodeLayer = UIKit.Rect(root, "Nodes").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            _dragLayer = UIKit.Rect(root, "Drag").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);

            var p = session.Puzzle;
            foreach (var input in p.Inputs) Inputs.Add(WiredInputView.Create(_nodeLayer, input, Metrics, this));
            foreach (var block in p.Blocks) Blocks.Add(WiredBlockView.Create(_nodeLayer, block, Metrics, this));
            foreach (var output in p.Outputs) Outputs.Add(WiredOutputView.Create(_nodeLayer, output, Metrics, this));
            if (places != null)
                foreach (var b in Blocks)
                    if (places.TryGetValue(b.Def.Id, out var at) && Fits(b, Clamp(b, at))) b.MoveTo(Clamp(b, at));

            _ghost = UIKit.Add<PathGraphic>(_dragLayer, "Ghost");
            _ghost.raycastTarget = false;
            _ghost.rectTransform.Stretch();
            _ghost.BaseWidth = 1.8f;
            _ghost.BaseColor = Palette.Dim;
            _ghost.LitWidth = 2.2f;
            _ghost.enabled = false;

            _box = UIKit.Shape(_dragLayer, "Selection box");
            _box.Radius = 4f;
            _box.Dashed = true;
            _box.enabled = false;

            RebuildLinks();
            Conceal();
        }

        internal void Register(PortView port) => _ports.Add(port);

        public int NodeOf(NodeDef def) => Session.NodeOf(def);

        // ------------------------------------------------------------------ links

        /// <summary>Redraws every wire from the session.</summary>
        public void RebuildLinks()
        {
            _selected.Clear();
            foreach (var l in Links) if (l != null) Destroy(l.gameObject);
            Links.Clear();
            foreach (var (from, to, port) in Session.Wires)
                Links.Add(LinkView.Create(_linkLayer, from, to, port, SourcePort(from).Position, SinkPort(to, port).Position, this));
            foreach (var pv in _ports) pv.Connected = pv.IsSource ? Session.ReadersOf(pv.Node).Count > 0 : Session.DriverOf(pv.Node, pv.Port) >= 0;
            if (_solved) foreach (var l in Links) l.SetCelebrate(true, 0f);
        }

        /// <summary>Redraws the wires into and out of one node where its ports are now.</summary>
        void RerouteLinks(int node)
        {
            foreach (var l in Links)
                if (l.From == node || l.To == node) l.SetEnds(SourcePort(l.From).Position, SinkPort(l.To, l.Port).Position);
        }

        public PortView SourcePort(int node) => _ports.Find(p => p.IsSource && p.Node == node);
        public PortView SinkPort(int node, int port) => _ports.Find(p => !p.IsSource && p.Node == node && p.Port == port);

        // ------------------------------------------------------------------ dragging

        internal void PortPressed(PortView port, PointerEventData e)
        {
            if (!Host.CanWire) return;
            if (e.button == PointerEventData.InputButton.Right)
            {
                Unplug(port);
                return;
            }
            if (e.button != PointerEventData.InputButton.Left) return;

            if (_armed)
            {
                // A wire held by a click: this dot is where it ends.
                Finish(port);
                return;
            }

            PortView from = port;
            if (!port.IsSource && Session.DriverOf(port.Node, port.Port) >= 0)
            {
                // Grabbing the end of a wire picks it up, still attached at its start.
                int source = Session.DriverOf(port.Node, port.Port);
                Session.Disconnect(port.Node, port.Port);
                from = SourcePort(source);
                App.Sound(Sfx.Tick, 1.3f, 0.5f);
            }
            _dragFrom = from;
            _dragging = true;
            _pressAt = Pointer();
            _ghost.enabled = true;
            UpdateGhost();
        }

        internal void PortReleased(PortView port, PointerEventData e)
        {
            if (!_dragging || e.button != PointerEventData.InputButton.Left) return;
            _dragging = false;
            var target = FindTarget(Pointer());
            if (target != null)
            {
                Finish(target);
                return;
            }
            if ((Pointer() - _pressAt).magnitude < 8f && _dragFrom == port)
            {
                // A click on a dot: the wire follows the pointer until the next click.
                _armed = true;
                Host.Say(_dragFrom.IsSource ? "Now click where the wire goes: a dot on the left of a block, or an output." : "Now click where the signal comes from: an input, or the dot on the right of a block.", 4f);
                return;
            }
            CancelDrag(false);
        }

        void Finish(PortView target)
        {
            var from = _dragFrom;
            _armed = false;
            _dragging = false;
            _ghost.enabled = false;
            _dragFrom = null;
            SetHoverTarget(null);
            if (from == null || target == null) return;
            if (target == from) return;

            PortView source = from.IsSource ? target.IsSource ? null : from : target.IsSource ? target : null;
            PortView sink = from.IsSource ? target : from;
            if (source == null || sink == null || sink.IsSource)
            {
                App.Sound(Sfx.Miss, 1.5f, 0.35f);
                Host.Say(from.IsSource ? "A wire runs from a signal into a block's left side, or into an output." : "A wire runs into here from an input or from a block's right side.", 3.5f);
                return;
            }
            if (!Session.CanConnect(source.Node, sink.Node, sink.Port, out string why))
            {
                App.Sound(Sfx.Miss, 1.5f, 0.35f);
                Host.Say(why, 4f);
                return;
            }
            bool replacing = Session.DriverOf(sink.Node, sink.Port) >= 0;
            Session.Connect(source.Node, sink.Node, sink.Port);
            App.Sound(Sfx.Dial, replacing ? 0.9f : 1.1f, 0.8f);
        }

        void CancelDrag(bool quiet)
        {
            bool had = _dragFrom != null;
            _dragging = false;
            _armed = false;
            _dragFrom = null;
            _ghost.enabled = false;
            SetHoverTarget(null);
            if (had && !quiet) App.Sound(Sfx.Close, 1.5f, 0.35f);
        }

        /// <summary>Puts down a wire being drawn. True if there was one.</summary>
        public bool CancelWire()
        {
            if (_dragFrom == null) return false;
            CancelDrag(false);
            return true;
        }

        public bool Drawing => _dragFrom != null;

        void Unplug(PortView port)
        {
            if (port.IsSource)
            {
                if (Session.DisconnectFrom(port.Node)) App.Sound(Sfx.Close, 1.4f, 0.5f);
                return;
            }
            if (Session.Disconnect(port.Node, port.Port)) App.Sound(Sfx.Close, 1.4f, 0.5f);
        }

        // ------------------------------------------------------------------ selecting wires with a box

        ShapeGraphic _box;
        Vector2 _boxFrom;
        bool _boxCut;
        readonly HashSet<LinkView> _selected = new HashSet<LinkView>();

        /// <summary>How many wires the last box selected.</summary>
        public int SelectedCount => _selected.Count;

        bool BoxPoint(PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_linkLayer, e.position, e.pressEventCamera, out local);

        internal void BoxBegin(PointerEventData e)
        {
            if (!Host.CanWire || Drawing || !BoxPoint(e, out _boxFrom)) return;
            _boxCut = e.button == PointerEventData.InputButton.Right;
            if (!_boxCut) ClearSelection();
            Color c = _boxCut ? Palette.Violet : Palette.Cyan;
            _box.Style(Paint.Solid, c.WithAlpha(0.06f), Paint.Solid, c.WithAlpha(0.8f), 1.2f);
            _box.rectTransform.Centered(_boxFrom.x, _boxFrom.y, 0f, 0f);
            _box.enabled = true;
        }

        internal void BoxDrag(PointerEventData e)
        {
            if (!_box.enabled || !BoxPoint(e, out var to)) return;
            var r = BoxRect(_boxFrom, to);
            _box.rectTransform.Centered(r.center.x, r.center.y, r.width, r.height);
            // Light up what the box would take, as it's drawn.
            foreach (var l in Links) l.SetSelected(Touches(l, r), _boxCut);
        }

        internal void BoxEnd(PointerEventData e)
        {
            if (!_box.enabled) return;
            _box.enabled = false;
            BoxPoint(e, out var to);
            var r = BoxRect(_boxFrom, to);
            _selected.Clear();
            foreach (var l in Links)
            {
                bool inside = Touches(l, r);
                l.SetSelected(inside, false);
                if (inside) _selected.Add(l);
            }
            if (_boxCut)
            {
                if (_selected.Count == 0) { Host.Say("Right-drag a box over wires to take them all off.", 3f); return; }
                DeleteSelection();
                return;
            }
            if (_selected.Count == 0) Host.Say("Drag a box over wires to select them.", 2.5f);
            else Host.Say(_selected.Count == 1
                ? "1 wire selected. Press Delete or Backspace, or right-click, to take it off."
                : $"{_selected.Count} wires selected. Press Delete or Backspace, or right-click, to take them off.", 5f);
        }

        static Rect BoxRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        /// <summary>True if any part of the wire's line falls inside the box.</summary>
        static bool Touches(LinkView link, Rect r)
        {
            var pts = link.Points;
            if (pts == null) return false;
            for (int i = 0; i < pts.Count; i++)
            {
                if (r.Contains(pts[i])) return true;
                if (i > 0 && SegmentCrosses(pts[i - 1], pts[i], r)) return true;
            }
            return false;
        }

        static bool SegmentCrosses(Vector2 a, Vector2 b, Rect r)
        {
            // Sample the segment; wire curves are made of short segments, so a few steps are plenty.
            for (int k = 1; k < 8; k++) if (r.Contains(Vector2.Lerp(a, b, k / 8f))) return true;
            return false;
        }

        /// <summary>Takes off every selected wire as one change. False if none were selected.</summary>
        public bool DeleteSelection()
        {
            if (_selected.Count == 0 || !Host.CanWire) return false;
            var wires = new List<(int to, int port)>();
            foreach (var l in _selected) if (l != null) wires.Add((l.To, l.Port));
            int n = wires.Count;
            _selected.Clear();
            if (!Session.DisconnectMany(wires)) return false;
            App.Sound(Sfx.Close, 1.2f, 0.6f);
            Host.Say(n == 1 ? "Took the wire off. Undo puts it back." : $"Took {n} wires off. Undo puts them all back.", 3f);
            return true;
        }

        /// <summary>Unselects every wire. False if none were selected.</summary>
        public bool ClearSelection()
        {
            if (_selected.Count == 0) return false;
            foreach (var l in _selected) if (l != null) l.SetSelected(false, false);
            _selected.Clear();
            return true;
        }

        internal void LinkClicked(LinkView link)
        {
            if (!Host.CanWire || Drawing) return;
            if (Session.Disconnect(link.To, link.Port)) App.Sound(Sfx.Close, 1.4f, 0.5f);
        }

        PortView FindTarget(Vector2 at)
        {
            if (_dragFrom == null) return null;
            PortView best = null;
            float bestDistance = SnapReach;
            foreach (var p in _ports)
            {
                if (p == _dragFrom || p.IsSource == _dragFrom.IsSource) continue;
                float d = Vector2.Distance(p.Position, at);
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }

        Vector2 Pointer()
        {
            var canvas = _nodeLayer.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_nodeLayer, GameInput.PointerPosition, cam, out var local);
            return local;
        }

        void UpdateGhost()
        {
            if (_dragFrom == null) return;
            Vector2 at = Pointer();
            var target = FindTarget(at);
            SetHoverTarget(target);
            Vector2 end = target != null ? target.Position : at;
            var pts = new List<Vector2>();
            if (_dragFrom.IsSource) MeshKit.WireCurve(pts, _dragFrom.Position, end);
            else MeshKit.WireCurve(pts, end, _dragFrom.Position);
            _ghost.SetPoints(pts);
            _ghost.BaseColor = target != null ? Palette.Text : Palette.Dim;
        }

        void SetHoverTarget(PortView target)
        {
            if (_hoverTarget == target) return;
            if (_hoverTarget != null) _hoverTarget.Targeted = false;
            _hoverTarget = target;
            if (_hoverTarget != null) _hoverTarget.Targeted = true;
        }

        void Update()
        {
            if (_dragFrom != null)
            {
                if (!Host.CanWire) CancelDrag(true);
                else UpdateGhost();
            }

            _clock += Time.unscaledDeltaTime;
            float loop = App.ReduceMotion ? -1f : Mathf.Repeat(_clock / LoopSeconds, 1f);
            float dt = Time.unscaledDeltaTime;
            foreach (var v in Inputs) v.Tick(loop);
            foreach (var v in Blocks) v.Tick(_shown ? loop : -1f);
            foreach (var v in Outputs) v.Tick(loop, dt, _shown);
        }

        // ------------------------------------------------------------------ moving blocks

        WiredBlockView _moving;
        Vector2 _moveFrom, _grabOffset;

        /// <summary>Any block sits somewhere other than where the circuit put it.</summary>
        public bool BlocksMoved
        {
            get
            {
                foreach (var b in Blocks) if ((b.Centre - b.Home).sqrMagnitude > 1f) return true;
                return false;
            }
        }

        /// <summary>Where every block sits now, by block id, in board coordinates. Blocks still at home are left out.</summary>
        public Dictionary<string, Vector2> Places()
        {
            var places = new Dictionary<string, Vector2>();
            foreach (var b in Blocks) if ((b.Centre - b.Home).sqrMagnitude > 1f) places[b.Def.Id] = b.Centre;
            return places;
        }

        /// <summary>Puts every block back where the circuit placed it. True if any moved.</summary>
        public bool PutBlocksBack()
        {
            bool any = false;
            foreach (var b in Blocks)
            {
                if ((b.Centre - b.Home).sqrMagnitude <= 1f) continue;
                Glide(b, b.Home);
                any = true;
            }
            return any;
        }

        internal bool BeginMove(WiredBlockView block)
        {
            if (!Host.CanWire || Drawing || _moving != null) return false;
            _moving = block;
            _moveFrom = block.Centre;
            _grabOffset = block.Centre - Pointer();
            block.transform.SetAsLastSibling();
            block.Lifted = true;
            App.Sound(Sfx.Tick, 1.2f, 0.4f);
            return true;
        }

        internal void Move(WiredBlockView block)
        {
            if (_moving != block) return;
            if (!Host.CanWire) { EndMove(block); return; }
            var at = Clamp(block, Pointer() + _grabOffset);
            block.MoveTo(at);
            block.Crowded = !Fits(block, at);
            RerouteLinks(NodeOf(block.Def));
        }

        internal void EndMove(WiredBlockView block)
        {
            if (_moving != block) return;
            _moving = null;
            block.Lifted = false;
            block.Crowded = false;
            if (!Fits(block, block.Centre))
            {
                Glide(block, _moveFrom);
                App.Sound(Sfx.Miss, 1.5f, 0.3f);
                Host.Say("There's no room there: a block can't sit on top of another one.", 3f);
                return;
            }
            if ((block.Centre - _moveFrom).sqrMagnitude > 1f)
            {
                App.Sound(Sfx.Dial, 1.3f, 0.45f);
                Host.BlocksMoved();
            }
        }

        void Glide(WiredBlockView block, Vector2 to)
        {
            Vector2 from = block.Centre;
            int node = NodeOf(block.Def);
            if (App.ReduceMotion)
            {
                block.MoveTo(to);
                RerouteLinks(node);
                return;
            }
            Tweener.Run(block, "glide", 0.25f, t =>
            {
                block.MoveTo(Vector2.Lerp(from, to, t));
                RerouteLinks(node);
            }, Ease.OutCubic);
        }

        /// <summary>Keeps a block, with the readout above it and the note below, inside the board.</summary>
        Vector2 Clamp(WiredBlockView block, Vector2 at)
        {
            var area = BoardMetrics.Area;
            float halfW = block.Size.x * 0.5f, halfH = block.Size.y * 0.5f;
            float minX = area.xMin - VoidFrame.Width * 0.5f + halfW + 40f;
            float maxX = area.xMax - VoidFrame.Width * 0.5f - halfW - 40f;
            float maxY = VoidFrame.Height * 0.5f - area.yMin - halfH - 44f;
            float minY = VoidFrame.Height * 0.5f - area.yMax + halfH + 30f;
            return new Vector2(Mathf.Clamp(at.x, minX, maxX), Mathf.Clamp(at.y, minY, maxY));
        }

        /// <summary>The block's card, at this spot, keeps clear of every other card on the board.</summary>
        bool Fits(WiredBlockView block, Vector2 at)
        {
            var me = new Rect(at - block.Size * 0.5f, block.Size);
            const float gap = 14f;
            bool Hits(Rect other) => me.xMin < other.xMax + gap && me.xMax > other.xMin - gap && me.yMin < other.yMax + gap && me.yMax > other.yMin - gap;
            foreach (var b in Blocks) if (b != block && Hits(new Rect(b.Centre - b.Size * 0.5f, b.Size))) return false;
            foreach (var i in Inputs) if (Hits(i.Bounds)) return false;
            foreach (var o in Outputs) if (Hits(o.Bounds)) return false;
            return true;
        }

        // ------------------------------------------------------------------ running

        /// <summary>Each node's distance from the inputs along the wires, for the run's flow.</summary>
        public int[] Depths()
        {
            int n = Session.NodeCount;
            var depth = new int[n];
            var state = new byte[n];
            int Visit(int node)
            {
                if (Session.IsInput(node)) return 0;
                if (state[node] == 2) return depth[node];
                if (state[node] == 1) return 0;
                state[node] = 1;
                int d = 0;
                for (int k = 0; k < Session.PortCount(node); k++)
                {
                    int src = Session.DriverOf(node, k);
                    if (src >= 0) d = Mathf.Max(d, Visit(src) + 1);
                }
                depth[node] = Mathf.Max(1, d);
                state[node] = 2;
                return depth[node];
            }
            for (int i = 0; i < n; i++) Visit(i);
            return depth;
        }

        /// <summary>Hides everything a run shows: outputs, block readouts, marks.</summary>
        /// <summary>Hides every signal a run showed, except the outputs in <paramref name="keepOutputs"/>.</summary>
        public void Conceal(bool[] keepOutputs = null)
        {
            _shown = false;
            foreach (var b in Blocks) b.Conceal();
            for (int o = 0; o < Outputs.Count; o++)
            {
                if (keepOutputs != null && keepOutputs[o] && Outputs[o].Showing) _shown = true;
                else Outputs[o].Conceal();
            }
        }

        /// <summary>
        /// After a wiring change: hides every block not in <paramref name="keep"/>
        /// (see <see cref="WiringSession.BlocksUnchanged"/>), and every output not in
        /// <paramref name="keepOutputs"/>. A kept block that was showing a signal keeps
        /// it until the next run, with its note brought up to date from
        /// <paramref name="now"/>, so a block newly wired on to an output is no longer
        /// NOT USED. A kept output stays as the last run showed it.
        /// </summary>
        public void ConcealChanged(bool[] keep, WiringReport now, bool[] keepOutputs = null)
        {
            bool any = false;
            for (int b = 0; b < Blocks.Count; b++)
            {
                var view = Blocks[b];
                if (!view.Showing) continue;
                if (keep[b])
                {
                    view.Renote(now.Values[Session.BlockNode(b)], now.EmptyBlocks.Contains(b), now.UnusedBlocks.Contains(b));
                    any = true;
                }
                else view.Conceal();
            }
            for (int o = 0; o < Outputs.Count; o++)
            {
                if (keepOutputs != null && keepOutputs[o] && Outputs[o].Showing) any = true;
                else Outputs[o].Conceal();
            }
            _shown = any;
        }

        /// <summary>Shows one block's result from a run.</summary>
        public void ShowBlock(int block, WiringReport report)
        {
            _shown = true;
            var sent = report.Values[Session.BlockNode(block)];
            if (sent != null) _seenBlocks[block] = sent.Medium;
            Blocks[block].Show(sent, report.EmptyBlocks.Contains(block), report.UnusedBlocks.Contains(block));
        }

        /// <summary>The kind of signal each block has been seen sending, on some run this visit.</summary>
        readonly Dictionary<int, Medium> _seenBlocks = new Dictionary<int, Medium>();

        /// <summary>
        /// For a wire into a block of another kind of signal, how that block reads
        /// what the wire carries ("K3 reads this wave as a digit: 0 at the bottom up
        /// to 9 at the top."), or null. Only once the player has seen the block
        /// send something, so it never gives away what kind of block it is.
        /// </summary>
        internal string ReadingNote(int from, int to, int port)
        {
            int inputs = Session.InputCount, blocks = Session.BlockCount;
            if (to < inputs || to >= inputs + blocks) return null;
            int block = to - inputs;
            if (!_seenBlocks.ContainsKey(block)) return null;
            Medium sent;
            if (from < inputs) sent = Session.Puzzle.Inputs[from].Signal.Medium;
            else if (from >= inputs + blocks || !_seenBlocks.TryGetValue(from - inputs, out sent)) return null;
            var def = Session.Puzzle.Blocks[block];
            var reads = def.Kind.Reads(port);
            if (reads == null) return null;
            string how = Media.HowRead(sent, reads.Value);
            return how == null ? null : $"{def.Label} reads this {Media.Noun(sent)} as a {Media.Noun(reads.Value)}: {how}.";
        }

        public void ShowOutput(int output, WiringReport report)
        {
            _shown = true;
            Outputs[output].Show(report.Values[Session.OutputNode(output)], report.OutputMet[output]);
        }

        /// <summary>
        /// The horizontal extent of every card, in frame units from the screen's
        /// centre, so a small board can be centred on what it actually draws.
        /// </summary>
        public (float min, float max) Extent()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in Inputs) { min = Mathf.Min(min, v.Bounds.xMin); max = Mathf.Max(max, v.Bounds.xMax); }
            foreach (var v in Outputs) { min = Mathf.Min(min, v.Bounds.xMin); max = Mathf.Max(max, v.Bounds.xMax); }
            foreach (var v in Blocks) { min = Mathf.Min(min, v.Centre.x - v.Size.x * 0.5f); max = Mathf.Max(max, v.Centre.x + v.Size.x * 0.5f); }
            return min <= max ? (min, max) : (0f, 0f);
        }

        /// <summary>Shows everything from a run at once, and marks the inputs it found unused.</summary>
        /// <summary>Shows everything a run found. Outputs in <paramref name="held"/> that are showing are left as they are.</summary>
        public void ShowAll(WiringReport report, bool[] held = null)
        {
            for (int b = 0; b < Blocks.Count; b++) ShowBlock(b, report);
            for (int o = 0; o < Outputs.Count; o++)
                if (held == null || !held[o] || !Outputs[o].Showing) ShowOutput(o, report);
        }

        public bool Solved => _solved;

        public void SetSolved(bool solved)
        {
            if (_solved == solved) return;
            _solved = solved;
            var depth = Depths();
            foreach (var l in Links) l.SetCelebrate(solved, solved ? depth[l.From] * 0.08f : 0f);
            foreach (var v in Inputs) v.Solved = solved;
            foreach (var v in Blocks) v.Solved = solved;
            foreach (var v in Outputs) v.Solved = solved;
        }

        public void SetShowRules(bool on)
        {
            foreach (var b in Blocks) b.ShowRule = on;
        }
    }

    /// <summary>
    /// A dot where wires join: on the right of an input or block (where a signal
    /// leaves) or on the left of a block or output (where one arrives).
    /// </summary>
    public sealed class PortView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int Node;
        /// <summary>The port on the node, for ports where signals arrive. -1 where they leave.</summary>
        public int Port;
        public bool IsSource;
        /// <summary>Where the dot is, in board space.</summary>
        public Vector2 Position;
        /// <summary>Where the dot is on its card, from the card's centre.</summary>
        public Vector2 Offset;

        WiringBoard _board;
        ShapeGraphic _dot;
        bool _hover, _targeted, _connected, _solved;

        public static PortView Create(RectTransform parent, Vector2 local, Vector2 boardPosition, int node, int port, bool source, WiringBoard board)
        {
            var rt = UIKit.Rect(parent, source ? "Out" : "In " + port).Centered(local.x, local.y, 30f, 30f);
            var view = rt.gameObject.AddComponent<PortView>();
            view.Node = node;
            view.Port = port;
            view.IsSource = source;
            view.Position = boardPosition;
            view.Offset = local;
            view._board = board;
            UIKit.Hit(rt).rectTransform.Stretch();
            view._dot = UIKit.Shape(rt, "Dot", ShapeKind.Circle);
            view._dot.raycastTarget = false;
            view._dot.rectTransform.Centered(0f, 0f, 11f, 11f);
            board.Register(view);
            view.Restyle();
            return view;
        }

        public bool Connected { get => _connected; set { if (_connected != value) { _connected = value; Restyle(); } } }
        public bool Targeted { get => _targeted; set { if (_targeted != value) { _targeted = value; Restyle(); } } }
        public bool Solved { get => _solved; set { if (_solved != value) { _solved = value; Restyle(); } } }

        void Restyle()
        {
            bool lit = _hover || _targeted;
            float size = lit ? 15f : 11f;
            _dot.rectTransform.sizeDelta = new Vector2(size, size);
            if (_solved || (_connected && !lit))
            {
                _dot.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                _dot.Glow = _solved ? 0.5f : 0.25f;
            }
            else
            {
                _dot.Style(Paint.Solid, Color.black, lit ? Paint.Signal : Paint.Solid, lit ? Color.white : Palette.Dim, lit ? 2f : 1.5f);
                _dot.Glow = lit ? 0.5f : 0f;
            }
            _dot.GlowPaint = Paint.Signal;
            _dot.GlowRadius = 12f;
        }

        public void OnPointerDown(PointerEventData e) => _board.PortPressed(this, e);
        public void OnPointerUp(PointerEventData e) => _board.PortReleased(this, e);
        public void OnPointerEnter(PointerEventData e) { _hover = true; Restyle(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; Restyle(); }
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { }
    }

    /// <summary>A wire the player drew. Click it to take it off.</summary>
    public sealed class LinkView : MonoBehaviour
    {
        public int From, To, Port;
        PathGraphic _path;
        FlowGraphic _flow;
        HitArea _hit;
        RectTransform _rt;
        WiringBoard _board;
        bool _hover, _celebrate, _pulsing, _selected, _cutting;
        float _length;

        public static LinkView Create(RectTransform layer, int from, int to, int port, Vector2 a, Vector2 b, WiringBoard board)
        {
            var rt = UIKit.Rect(layer, $"Wire {from}>{to}.{port}").Stretch();
            var view = rt.gameObject.AddComponent<LinkView>();
            view.From = from;
            view.To = to;
            view.Port = port;
            view._board = board;
            view._rt = rt;
            view._path = rt.gameObject.AddComponent<PathGraphic>();
            view._path.raycastTarget = false;
            view._path.LitWidth = 2.2f;

            var flowRt = UIKit.Rect(rt, "Flow").Stretch();
            view._flow = flowRt.gameObject.AddComponent<FlowGraphic>();
            view._flow.raycastTarget = false;
            view._flow.Source = view._path;
            view._flow.enabled = false;

            view._hit = UIKit.Hit(rt, "Hit");
            view._hit.gameObject.AddComponent<LinkHit>().Link = view;
            view.SetEnds(a, b);
            view.ApplyRest();
            return view;
        }

        /// <summary>Redraws the wire between two dots, as when a block it joins is moved.</summary>
        public void SetEnds(Vector2 a, Vector2 b)
        {
            var pts = new List<Vector2>();
            MeshKit.WireCurve(pts, a, b);
            _path.SetPoints(pts);
            _length = MeshKit.Length(pts);
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            _hit.rectTransform.Centered((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.x - min.x + 24f, max.y - min.y + 24f);
            if (_flow.enabled) _flow.SetVerticesDirty();
        }

        public List<Vector2> Points => _path.Points;

        public float Distance(Vector2 local)
        {
            var pts = _path.Points;
            float best = float.MaxValue;
            for (int i = 1; i < pts.Count; i++) best = Mathf.Min(best, InkGraphic.SegmentDistance(local, pts[i - 1], pts[i]));
            return best;
        }

        public bool ScreenToLocal(Vector2 screen, Camera cam, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out local);

        internal void SetHover(bool on)
        {
            _hover = on && _board.Host.CanWire && !_board.Drawing;
            ApplyRest();
            if (!_hover) return;
            string note = _board.ReadingNote(From, To, Port);
            _board.Host.Say(note == null ? "Click to take this wire off." : note + " Click to take the wire off.", note == null ? 2f : 5f);
        }

        internal void Clicked() => _board.LinkClicked(this);

        /// <summary>Marked by a selection box: cyan when selected, violet when a right-drag is about to cut it.</summary>
        public void SetSelected(bool on, bool cutting)
        {
            if (_selected == on && _cutting == cutting) return;
            _selected = on;
            _cutting = cutting;
            ApplyRest();
        }

        /// <summary>A burst of light travelling the length of the wire.</summary>
        public void Pulse(float delay, float duration)
        {
            if (_celebrate) return;
            _pulsing = true;
            Tweener.Run(this, "pulse", duration, t =>
            {
                _path.LitAlpha = 1f;
                _path.SetLit(Mathf.Max(0f, t - 0.35f), t);
                _path.Spark = t < 1f ? t : -1f;
            }, Ease.Linear, delay, () =>
            {
                _pulsing = false;
                ApplyRest();
            });
        }

        public void SetCelebrate(bool on, float delay)
        {
            if (_celebrate == on) return;
            _celebrate = on;
            Tweener.Kill(this, "pulse");
            _pulsing = false;
            if (on && !App.ReduceMotion)
            {
                _path.LitAlpha = 1f;
                _path.SetLit(0f, 0f);
                Tweener.Run(this, "celebrate", 0.3f, t =>
                {
                    _path.SetLit(0f, t);
                    _path.Spark = t < 1f ? t : -1f;
                }, Ease.Linear, delay, ApplyRest);
            }
            else
            {
                Tweener.Kill(this, "celebrate");
                ApplyRest();
            }
        }

        void ApplyRest()
        {
            if (_pulsing) return;
            _path.Spark = -1f;
            _path.BaseColor = _selected ? (_cutting ? Palette.Violet : Palette.Cyan) : _hover ? Palette.Text : Palette.Faint;
            _path.BaseWidth = _selected ? 2.6f : _hover ? 2.2f : 1.6f;
            if (_celebrate)
            {
                _path.LitAlpha = 1f;
                _path.Glow = 0.42f;
                _path.SetLit(0f, 1f);
            }
            else _path.SetLit(0f, 0f);
            _flow.enabled = _celebrate;
            _flow.Animate = !App.ReduceMotion;
            _flow.SetVerticesDirty();
        }
    }

    /// <summary>Drags on empty board, passed to the board as a selection box.</summary>
    public sealed class BoardBox : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public WiringBoard Board;
        bool _active;

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Middle) return;
            _active = true;
            Board.BoxBegin(e);
        }

        public void OnDrag(PointerEventData e) { if (_active) Board.BoxDrag(e); }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_active) return;
            _active = false;
            Board.BoxEnd(e);
        }
    }

    /// <summary>The clickable part of a drawn wire: only points within a few pixels of the line.</summary>
    public sealed class LinkHit : MonoBehaviour, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public LinkView Link;
        const float Reach = 9f;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (Link == null || !Link.ScreenToLocal(screenPoint, eventCamera, out var local)) return false;
            return Link.Distance(local) <= Reach;
        }

        public void OnPointerEnter(PointerEventData e) => Link.SetHover(true);
        public void OnPointerExit(PointerEventData e) => Link.SetHover(false);

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left || e.button == PointerEventData.InputButton.Right) Link.Clicked();
        }
    }

    /// <summary>A fixed signal on the left of a wired circuit, drawn over its whole loop.</summary>
    public sealed class WiredInputView : MonoBehaviour
    {
        public InputDef Def;
        public Vector2 Centre;
        public PortView Out;
        /// <summary>The card and its letter, in board coordinates.</summary>
        public Rect Bounds;
        ShapeGraphic _card;
        SignalView _signal;
        VoidText _label;
        bool _solved;

        public static WiredInputView Create(RectTransform layer, InputDef def, BoardMetrics m, WiringBoard board)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float w = 184f * s, h = 78f * s;
            var rt = UIKit.Rect(layer, "Input " + def.Id).Centered(c.x, c.y, w, h);
            var view = rt.gameObject.AddComponent<WiredInputView>();
            view.Def = def;
            view.Centre = c;
            view.Bounds = Rect.MinMaxRect(c.x - w * 0.5f - 48f * s, c.y - h * 0.5f, c.x + w * 0.5f, c.y + h * 0.5f);

            view._card = UIKit.Shape(rt, "Card");
            view._card.raycastTarget = false;
            view._card.rectTransform.Stretch();
            view._card.Radius = 14f * s;

            view._label = UIKit.Text(rt, def.Id, TextStyle.Label.WithSize(Mathf.RoundToInt(17 * s)), TextAnchor.MiddleRight, "Label");
            view._label.rectTransform.Centered(-w * 0.5f - 28f * s, 0f, 40f, 24f);

            var spec = def.Signal;
            bool wave = Media.IsWave(spec.Medium);
            view._signal = SignalView.Create(rt, w - 36f * s, (wave ? 40f : 46f) * s);
            view._signal.GetComponent<RectTransform>().Centered(0f, wave ? 6f * s : 0f, w - 36f * s, (wave ? 40f : 46f) * s);
            view._signal.ShowWaveSettings(inside: false);
            view._signal.Set(spec.Trace, ValueLook.Live);
            view._signal.MakePlayable(() => board.Host.PlayTrace(spec.Trace));

            // Where every signal is one kind, as in Part One, naming it on each card is noise.
            if (board.Puzzle.Chapter == null || board.Puzzle.Chapter.Mixed)
            {
                var kind = UIKit.Text(rt, Media.Noun(spec.Medium).ToUpperInvariant(), TextStyle.Label.WithSize(Mathf.RoundToInt(10 * s)).WithTracking(0.24f).WithColor(Palette.Faint), TextAnchor.MiddleCenter, "Type");
                kind.rectTransform.Centered(0f, -h * 0.5f - 12f * s, w, 16f);
            }

            view.Out = PortView.Create(rt, new Vector2(w * 0.5f, 0f), c + new Vector2(w * 0.5f, 0f), board.NodeOf(def), -1, true, board);
            view.Restyle();
            return view;
        }

        public void Tick(float loop) => _signal.Playhead = loop;

        public bool Solved { get => _solved; set { _solved = value; Out.Solved = value; Restyle(); } }

        void Restyle()
        {
            Color edge = _solved ? Color.white : Palette.Outline;
            _card.Style(Paint.Solid, Color.black, _solved ? Paint.Signal : Paint.Solid, edge, _solved ? 1.6f : 1.2f);
            _card.Glow = _solved ? 0.3f : 0f;
            _card.GlowPaint = Paint.Signal;
            _card.GlowRadius = 18f;
            _label.color = Palette.Dim;
        }
    }

    /// <summary>
    /// A block in a wired circuit: its label (K1, K2 ...) and its ports. After a run it shows what it sent above it,
    /// and under it why it sent nothing (an empty input) or that it isn't used.
    /// </summary>
    public sealed class WiredBlockView : MonoBehaviour
    {
        public BlockDef Def;
        public Vector2 Centre;
        /// <summary>Where the circuit puts the block, before the player moves it.</summary>
        public Vector2 Home;
        /// <summary>The block's card, in board units.</summary>
        public Vector2 Size;
        public PortView Out;
        public readonly List<PortView> Ins = new List<PortView>();

        ShapeGraphic _outline;
        VoidText _code, _note, _rule;
        SignalView _readout;
        bool _solved, _broken, _lifted, _crowded, _hover;
        float _flash, _noteY;

        public static WiredBlockView Create(RectTransform layer, BlockDef def, BoardMetrics m, WiringBoard board)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float w = 150f * s, h = 88f * s;
            var rt = UIKit.Rect(layer, "Block " + def.Id).Centered(c.x, c.y, w, h);
            var view = rt.gameObject.AddComponent<WiredBlockView>();
            view.Def = def;
            view.Centre = view.Home = c;
            view.Size = new Vector2(w, h);
            view._board = board;

            // The body is what the player grabs to move the block. The dots sit on top of it.
            var body = UIKit.Hit(rt, "Body");
            body.rectTransform.Stretch();
            var grip = body.gameObject.AddComponent<BlockGrip>();
            grip.Block = view;
            grip.Board = board;

            view._outline = UIKit.Shape(rt, "Outline");
            view._outline.raycastTarget = false;
            view._outline.rectTransform.Stretch();
            view._outline.Radius = 16f * s;

            // Every block is numbered K1, K2 ... in its circuit. The number only tells
            // blocks apart: it says nothing about what a block does, and a block has
            // no settings to show.
            view._code = UIKit.Text(rt, def.Label, TextStyle.Value.WithSize(Mathf.RoundToInt(28 * s)).WithWeight(200).WithTracking(0.08f), TextAnchor.MiddleCenter, "Label");
            view._code.rectTransform.Centered(0f, -3f * s, w, h * 0.55f);

            int node = board.NodeOf(def);
            int n = def.Kind.InputCount;
            float spread = n == 1 ? 0f : 24f * s;
            for (int p = 0; p < n; p++)
            {
                float y = n == 1 ? 0f : spread - p * (2f * spread / (n - 1));
                view.Ins.Add(PortView.Create(rt, new Vector2(-w * 0.5f, y), c + new Vector2(-w * 0.5f, y), node, p, false, board));
            }
            view.Out = PortView.Create(rt, new Vector2(w * 0.5f, 0f), c + new Vector2(w * 0.5f, 0f), node, -1, true, board);

            view._readout = SignalView.Create(rt, 118f * s, 32f * s, "Sends");
            view._readout.GetComponent<RectTransform>().Centered(0f, h * 0.5f + 24f * s, 118f * s, 32f * s);
            // The block's card is right under its signal, so a wave's settings go inside the box.
            view._readout.ShowWaveSettings(inside: true);
            view._readout.MakePlayable(() => { if (view._readout.Trace != null) board.Host.PlayTrace(view._readout.Trace); });

            view._note = UIKit.Text(rt, "", TextStyle.Label.WithSize(Mathf.RoundToInt(11 * s)).WithTracking(0.24f).WithColor(Palette.Violet), TextAnchor.MiddleCenter, "Note");
            view._note.rectTransform.Centered(0f, -h * 0.5f - 14f * s, w + 80f, 16f);
            view._noteY = -h * 0.5f - 14f * s;

            // The rule sits right under the block, where the note would go; a note then goes under the rule.
            view._rule = UIKit.Text(rt, "", TextStyle.Body.WithSize(Mathf.Max(12, Mathf.RoundToInt(13 * s))).WithColor(Palette.Dim), TextAnchor.UpperCenter, "Rule");
            view._rule.horizontalOverflow = HorizontalWrapMode.Wrap;
            view._rule.LineHeight = 1.2f;
            view._rule.rectTransform.Centered(0f, -h * 0.5f - 6f * s - 18f, Mathf.Max(w + 60f, 220f * s), 36f);
            view._rule.enabled = false;

            view.Restyle();
            return view;
        }

        public void Conceal()
        {
            _readout.gameObject.SetActive(false);
            _note.text = "";
            _broken = false;
            Restyle();
        }

        /// <summary>What the block sent on the run, or null if nothing; and whether a rule is broken here.</summary>
        public void Show(Trace sent, bool emptyInput, bool unused)
        {
            _readout.gameObject.SetActive(true);
            _readout.Set(sent, ValueLook.Live);
            Renote(sent, emptyInput, unused);
        }

        /// <summary>A signal from a run is showing above the block.</summary>
        public bool Showing => _readout.gameObject.activeSelf;

        /// <summary>Brings the note under the signal up to date, leaving the signal as it is.</summary>
        public void Renote(Trace sent, bool emptyInput, bool unused)
        {
            _note.text = emptyInput ? "AN INPUT IS EMPTY" : sent == null ? "GETS NOTHING" : unused ? "NOT USED" : "";
            _broken = emptyInput || unused;
            Restyle();
        }

        public void Tick(float loop) => _readout.Playhead = loop;

        /// <summary>Moves the block, and its dots with it. The board redraws the wires.</summary>
        public void MoveTo(Vector2 at)
        {
            Centre = at;
            ((RectTransform)transform).Centered(at.x, at.y, Size.x, Size.y);
            foreach (var p in Ins) p.Position = at + p.Offset;
            Out.Position = at + Out.Offset;
        }

        /// <summary>Being dragged: the outline brightens.</summary>
        public bool Lifted { get => _lifted; set { if (_lifted != value) { _lifted = value; Restyle(); } } }
        /// <summary>Being dragged over another card, where it can't be dropped.</summary>
        public bool Crowded { get => _crowded; set { if (_crowded != value) { _crowded = value; Restyle(); } } }
        public bool Hover { get => _hover; set { if (_hover != value) { _hover = value; Restyle(); } } }

        /// <summary>The signal passing through on a run: the outline lights and fades.</summary>
        public void Flash()
        {
            if (App.ReduceMotion) return;
            Tweener.Run(this, "flash", 0.55f, t => { _flash = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public bool ShowRule
        {
            get => _rule.enabled;
            set
            {
                _rule.enabled = value;
                // Under the block goes a short form; pointing at the block gives the whole rule.
                _rule.text = value ? Capitalise(Def.Kind.ShortRule) : "";
                var nrt = _note.rectTransform;
                nrt.anchoredPosition = new Vector2(nrt.anchoredPosition.x, value ? _noteY - 40f : _noteY);
            }
        }

        /// <summary>The whole rule, for the status line, or null while the rule is hidden.</summary>
        public string FullRule => _rule.enabled ? $"{Def.Label}: {Def.Kind.Rule}." : null;

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        WiringBoard _board;

        public bool Solved
        {
            get => _solved;
            set
            {
                _solved = value;
                Out.Solved = value;
                foreach (var p in Ins) p.Solved = value;
                Restyle();
            }
        }

        void Restyle()
        {
            Paint edgePaint = Paint.Solid;
            Color edge = _broken ? Palette.Violet.WithAlpha(0.7f) : Palette.Faint;
            float width = 1.4f, glow = 0f;
            if (_solved) { edgePaint = Paint.Signal; edge = Color.white; width = 1.7f; glow = 0.28f; }
            if (_flash > 0.02f && !_solved)
            {
                edgePaint = Paint.Signal;
                edge = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, _flash));
                width = 1.7f;
                glow = Mathf.Max(glow, 0.4f * _flash);
            }
            if (_crowded) { edgePaint = Paint.Solid; edge = Palette.Violet; width = 1.7f; glow = 0f; }
            else if (_lifted) { edgePaint = Paint.Solid; edge = Palette.Text; width = 1.7f; }
            else if (_hover && !_solved && _flash <= 0.02f) edge = Color.Lerp(edge, Palette.Dim, 0.6f);
            _outline.Style(Paint.Solid, Color.black, edgePaint, edge, width);
            _outline.Glow = glow;
            _outline.GlowPaint = Paint.Signal;
            _outline.GlowRadius = 22f;
            _code.color = Palette.Dim;
        }
    }

    /// <summary>A block's body: press and drag it to move the block.</summary>
    public sealed class BlockGrip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public WiredBlockView Block;
        public WiringBoard Board;
        bool _moving;
        static int _tips;

        public void OnPointerEnter(PointerEventData e)
        {
            Block.Hover = true;
            if (Block.FullRule != null && !Board.Drawing)
            {
                Board.Host.Say(Block.FullRule, 4f);
                return;
            }
            if (_tips < 3 && Board.Host.CanWire && !Board.Drawing)
            {
                _tips++;
                Board.Host.Say("Drag a block to move it. Its wires follow.", 2.5f);
            }
        }

        public void OnPointerExit(PointerEventData e) => Block.Hover = false;

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            _moving = Board.BeginMove(Block);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_moving) Board.Move(Block);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_moving) return;
            _moving = false;
            Board.EndMove(Block);
        }

        void OnDisable()
        {
            if (!_moving) return;
            _moving = false;
            Board.EndMove(Block);
        }
    }

    /// <summary>
    /// An output device in a wired circuit: what it does with the signal that
    /// arrived on the latest run (a ? before one), and beside it, always, what
    /// its target wants it to do, joined by = or ≠.
    /// </summary>
    public sealed class WiredOutputView : MonoBehaviour
    {
        public OutputDef Def;
        public Vector2 Centre;
        public PortView In;
        /// <summary>The card and the labels over it, in board coordinates.</summary>
        public Rect Bounds;

        ShapeGraphic _frame;
        DeviceView _now, _want;
        GlyphGraphic _relation;
        VoidText _label, _unknown, _wantLabel;
        bool _known, _matched, _solved;
        float _flash;

        public static WiredOutputView Create(RectTransform layer, OutputDef def, BoardMetrics m, WiringBoard board)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float size = 70f * s;
            float deviceW = DeviceView.WidthFor(def.Device, size);
            float half = Mathf.Max(118f * s, deviceW + 26f * s);
            float w = half * 2f + 26f * s, h = 104f * s;
            // The port sits where the node is; the card extends to the right of it.
            float left = c.x - 70f * s;
            var rt = UIKit.Rect(layer, "Output " + def.Id).Centered(left + w * 0.5f, c.y, w, h);
            var view = rt.gameObject.AddComponent<WiredOutputView>();
            view.Def = def;
            view.Centre = c;
            view.Bounds = Rect.MinMaxRect(left, c.y - h * 0.5f, left + w, c.y + h * 0.5f + 24f * s);

            view._frame = UIKit.Shape(rt, "Frame");
            view._frame.raycastTarget = false;
            view._frame.rectTransform.Stretch();
            view._frame.Radius = 18f * s;

            float nowX = -w * 0.5f + half * 0.5f, wantX = w * 0.5f - half * 0.5f;
            view._label = UIKit.Text(rt, $"{def.Id}  ·  {Devices.Noun(def.Device)}", TextStyle.Label.WithSize(Mathf.RoundToInt(13 * s)).WithTracking(0.22f), TextAnchor.MiddleLeft, "Label");
            view._label.rectTransform.Centered(-w * 0.5f + 4f + 150f, h * 0.5f + 14f * s, 300f, 20f);

            view._now = DeviceView.Create(rt, def.Device, size, "Now");
            view._now.GetComponent<RectTransform>().Centered(nowX, 0f, deviceW, size);
            view._now.ShowWaveSettings();
            view._now.MakePlayable(() => { if (view._now.Trace != null) board.Host.PlayTrace(view._now.Trace); });
            view._unknown = UIKit.Text(rt, "?", TextStyle.Value.WithSize(Mathf.RoundToInt(size * 0.6f)).WithWeight(200).WithColor(Palette.Faint), TextAnchor.MiddleCenter, "Unknown");
            view._unknown.rectTransform.Centered(nowX, 0f, half, size);

            view._relation = UIKit.Glyph(rt, "notequal", Palette.Faint, 1.8f, "Relation");
            view._relation.rectTransform.Centered((nowX + wantX) * 0.5f, 0f, 18f * s, 18f * s);

            view._want = DeviceView.Create(rt, def.Device, size, "Want");
            view._want.GetComponent<RectTransform>().Centered(wantX, 0f, deviceW, size);
            view._want.ShowWaveSettings();
            view._want.Set(def.Want, ValueLook.Target);
            view._want.MakePlayable(() => board.Host.PlayTrace(def.Want));
            view._wantLabel = UIKit.Text(rt, "TARGET", TextStyle.Label.WithSize(Mathf.RoundToInt(11 * s)).WithTracking(0.26f), TextAnchor.MiddleCenter, "Target label");
            view._wantLabel.rectTransform.Centered(wantX, h * 0.5f + 14f * s, 120f, 18f);

            view.In = PortView.Create(rt, new Vector2(-w * 0.5f, 0f), new Vector2(left, c.y), board.NodeOf(def), 0, false, board);
            view.Conceal();
            return view;
        }

        public void Conceal()
        {
            _known = false;
            _matched = false;
            _now.Set(null, ValueLook.Live);
            _now.gameObject.SetActive(false);
            _unknown.enabled = true;
            Restyle();
        }

        public void Show(Trace got, bool matched)
        {
            _known = true;
            _matched = matched;
            _now.gameObject.SetActive(true);
            _now.Set(got, ValueLook.Live);
            _unknown.enabled = false;
            Restyle();
            if (!App.ReduceMotion) Tweener.Run(this, "flash", 0.6f, t => { _flash = 1f - t; Restyle(); }, Ease.OutQuad);
            _now.Ping();
        }

        public Trace Shown => _known ? _now.Trace : null;
        /// <summary>A signal from a run is showing.</summary>
        public bool Showing => _known;

        public void Ping(bool target)
        {
            if (target) _want.Ping();
            else _now.Ping();
        }

        public void Tick(float loop, float dt, bool shown)
        {
            _want.Tick(loop, dt);
            if (_known) _now.Tick(loop, dt);
        }

        public bool Matched => _matched;
        public bool Solved { get => _solved; set { _solved = value; In.Solved = value; Restyle(); } }

        void Restyle()
        {
            bool lit = _matched || _solved;
            if (lit)
            {
                _frame.Style(Paint.Solid, Color.black, Paint.Signal, Color.white, 1.7f);
                _frame.Glow = (_solved ? 0.5f : 0.3f) + 0.25f * _flash;
                _frame.GlowRadius = 24f;
            }
            else
            {
                _frame.Style(Paint.Solid, Color.black, Paint.Solid, Color.Lerp(Palette.Faint, Palette.Dim, _flash), 1.3f);
                _frame.Glow = 0.12f * _flash;
                _frame.GlowRadius = 20f;
            }
            _frame.GlowPaint = Paint.Signal;
            _relation.enabled = _known || _solved;
            _relation.Glyph = lit ? "equals" : "notequal";
            _relation.Paint = lit ? Paint.Signal : Paint.Solid;
            _relation.Tint = lit ? Color.white : Palette.Dim;
            _want.Look = lit ? ValueLook.Met : ValueLook.Target;
            _wantLabel.color = lit ? Palette.Dim : Palette.Faint;
            _label.color = Palette.Dim;
        }
    }
}
