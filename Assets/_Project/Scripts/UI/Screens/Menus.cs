using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Components;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using Blackbox.UI.Board;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>
    /// The title: the wordmark, a one-block circuit quietly ticking beneath it,
    /// and the main menu. The circuit is live: its input flips by itself, and the
    /// block and input can be clicked like any other.
    /// </summary>
    public sealed class TitleScreen : GameScreen, IBoardHost
    {
        const string DemoJson = @"{
            ""id"": ""title"", ""name"": ""BLACKBOX"", ""probes"": 0,
            ""inputs"": { ""A"": { ""at"": [0, 0], ""value"": 0, ""adjustable"": true } },
            ""blocks"": { ""k"": { ""kind"": ""B1"", ""state"": 0, ""at"": [1, 0] } },
            ""outputs"": { ""X"": [2, 0] },
            ""wires"": [""A -> k"", ""k -> X""]
        }";

        const float Tick = 1.7f;
        /// <summary>After the player touches the circuit, it waits this long before ticking again.</summary>
        const float TouchPause = 5f;

        MenuList _menu;
        BoardView _board;
        PuzzleSession _demo;
        float _timer;
        bool _auto;

        protected internal override void Build()
        {
            float cx = VoidFrame.Width * 0.5f;
            var mark = UIKit.Text(Root, "BLACKBOX", TextStyle.Display.WithSize(84), TextAnchor.MiddleCenter, "Wordmark");
            mark.rectTransform.At(cx, 236f, 1400f, 120f);
            var tagline = UIKit.Text(Root, "LEARN THE RULES  ·  COMPLETE THE CIRCUIT", TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Faint));
            tagline.rectTransform.At(cx, 314f, 900f, 20f);

            BuildDemo();

            _menu = new MenuList();
            var progress = Game.Progress;
            bool started = progress.TotalSolved > 0 || !string.IsNullOrEmpty(Game.Save.LastPuzzle);
            float y = 652f;
            const float step = 58f;
            _menu.Add(MenuItem.Create(Root, started ? "CONTINUE" : "START", cx, y), () => Game.GoPuzzle(progress.ContinueTarget()));
            _menu.Add(MenuItem.Create(Root, "CIRCUITS", cx, y += step), () => Game.GoChapters(null));
            _menu.Add(MenuItem.Create(Root, "SETTINGS", cx, y += step), Game.OpenSettings);
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                _menu.Add(MenuItem.Create(Root, "QUIT", cx, y += step), Game.Quit);

            int total = 0;
            foreach (var c in Game.Library.Chapters) total += c.Puzzles.Count;
            if (progress.TotalSolved > 0)
            {
                var solved = UIKit.Text(Root, $"{progress.TotalSolved} OF {total} CIRCUITS SOLVED", TextStyle.Label.WithSize(11).WithTracking(0.3f).WithColor(Palette.Faint));
                solved.rectTransform.At(cx, 1032f, 600f, 16f);
            }
            var version = UIKit.Text(Root, "V" + Application.version, TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            version.rectTransform.At(90f + 100f, 1032f, 200f, 16f);
            Chrome.KeyHints(Root, 1830f, 1032f, true, "ARROWS", "MOVE", "ENTER", "CHOOSE");
        }

        void BuildDemo()
        {
            var def = PuzzleLoader.ReadPuzzle(JsonValue.Parse(DemoJson), "title", Medium.Binary);
            _demo = new PuzzleSession(def);
            _demo.Changed += OnDemoChanged;
            var metrics = BoardMetrics.Fit(def, new Rect(560f, 360f, 800f, 230f));
            metrics.Scale = 0.86f;
            _board = BoardView.Create(Root, _demo, this, metrics);
            _board.Quiet = true;
            _timer = 0.9f;
        }

        void OnDemoChanged(ChangeKind kind, int index)
        {
            if (kind == ChangeKind.Input) _board.Inputs[index].SetValue(_demo.InputValue(index), !_auto);
            else _board.Blocks[index].SetState(_demo.State(index), true);
            _board.Refresh(true);
            _board.Pulse(kind == ChangeKind.Input ? index : _demo.Circuit.BlockNode(index));
        }

        void Update()
        {
            if (!Built || _demo == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = Tick;
            _auto = true;
            _demo.SetInput(0, 1 - _demo.InputValue(0));
            _auto = false;
        }

        public override void HandleInput()
        {
            _menu.HandleKeys();
        }

        // The demo circuit behaves like any circuit.
        public void InputStep(int input, int direction)
        {
            _timer = TouchPause;
            if (_demo.CycleInput(input, direction)) App.Sound(Sfx.Dial);
        }

        public void BlockStep(int block, int direction)
        {
            _timer = TouchPause;
            if (_demo.CycleState(block, direction)) App.Sound(Sfx.Dial);
        }

        public void BlockPip(int block, int state)
        {
            _timer = TouchPause;
            if (_demo.SetState(block, state)) App.Sound(Sfx.Dial);
        }

        public void WireClicked(int wire, float at) { }
        public void ProbeClicked(int probe) { }
        public void PlayValue(int value) { _timer = TouchPause; App.Audio?.PlaySignal(Medium.Binary, value); }
        public void Hover(BoardItem item, int index, bool on) { }
        public bool CanPlaceProbe => false;
    }

    /// <summary>
    /// The map of a chapter: its circuits strung along one wire. The wire lights
    /// up as far as the player has solved.
    /// </summary>
    public sealed class ChapterScreen : GameScreen
    {
        public ChapterDef Chapter;

        sealed class Tab
        {
            public ChapterDef Chapter;
            public VoidText Label;
            public GlyphGraphic Lock;
            public PathGraphic Underline;
            public Clickable Click;
        }

        sealed class Node
        {
            public PuzzleDef Puzzle;
            public Vector2 At; // frame coordinates, y down
            public ShapeGraphic Ring;
            public VoidText Number;
            public GlyphGraphic Lock;
            public VoidText Name;
            public Clickable Click;
            public bool Solved, Open, Current;
        }

        sealed class PartTab
        {
            public PartDef Part;
            public VoidText Label;
            public Clickable Click;
        }

        readonly List<Tab> _tabs = new List<Tab>();
        readonly List<PartTab> _partTabs = new List<PartTab>();
        readonly List<Node> _nodes = new List<Node>();
        RectTransform _page, _tabRoot;
        PartDef _part;
        int _focus;
        float _pulse;
        VoidText _infoNumber, _infoName, _infoStatus;

        const float RowA = 560f;
        const float RowB = 760f;

        protected internal override void Build()
        {
            if (Chapter == null) Chapter = Game.Progress.CurrentChapter();
            Chrome.Wordmark(Root, 90f, 62f, 22, Game.GoTitle);
            BuildPartTabs();
            Chrome.TextButton(Root, "BACK", 1830f, 62f, true, Game.GoTitle, "back");
            UIKit.Rule(Root, 90f, 1830f, 122f, Palette.Rule);

            Chrome.KeyHints(Root, VoidFrame.Width * 0.5f - 330f, 1032f, false, "ARROWS", "MOVE", "ENTER", "PLAY", "TAB", "CHAPTER", "ESC", "BACK");
            ShowChapter(Chapter, false);
        }

        /// <summary>The parts of the game along the top: the tutorials, then the main game. A locked part shows a padlock.</summary>
        void BuildPartTabs()
        {
            var parts = Game.Library.Parts;
            if (parts.Count <= 1)
            {
                var title = UIKit.Text(Root, "CIRCUITS", TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Dim));
                title.rectTransform.At(VoidFrame.Width * 0.5f, 62f, 400f, 20f);
                return;
            }
            var style = TextStyle.Label.WithSize(13).WithTracking(0.4f);
            const float pitch = 230f;
            float x0 = VoidFrame.Width * 0.5f - pitch * (parts.Count - 1) * 0.5f;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                bool open = Game.Progress.IsUnlocked(part);
                var rt = UIKit.Rect(Root, "Part " + part.Id).At(x0 + i * pitch, 62f, 210f, 40f);
                var tab = new PartTab { Part = part };
                tab.Label = UIKit.Text(rt, part.Title, style);
                tab.Label.rectTransform.Centered(open ? 0f : 10f, 0f, 200f, 20f);
                if (!open)
                {
                    var lockGlyph = UIKit.Glyph(rt, "lock", Palette.Faint, 1.2f);
                    float w = tab.Label.MeasureWidth();
                    lockGlyph.rectTransform.Centered(10f - w * 0.5f - 14f, 1f, 12f, 12f);
                }
                UIKit.Hit(rt).rectTransform.Stretch();
                tab.Click = rt.gameObject.AddComponent<Clickable>();
                tab.Click.Clicked = () => ShowPart(part);
                tab.Click.HoverChanged = _ => StylePartTabs();
                _partTabs.Add(tab);
            }
        }

        void StylePartTabs()
        {
            foreach (var tab in _partTabs)
            {
                bool active = tab.Part == _part;
                bool open = Game.Progress.IsUnlocked(tab.Part);
                tab.Label.color = active ? Palette.Text : tab.Click.Hovered ? Palette.Dim : open ? Palette.Dim : Palette.Faint;
                tab.Label.SignalFill = active && open;
            }
        }

        void ShowPart(PartDef part)
        {
            if (part == _part || part.Chapters.Count == 0) return;
            App.Sound(Sfx.Click);
            ChapterDef target = null;
            foreach (var c in part.Chapters)
                if (Game.Progress.CurrentIn(c) != null) { target = c; break; }
            ShowChapter(target ?? part.Chapters[0], true);
        }

        void BuildTabs()
        {
            if (_tabRoot != null) Destroy(_tabRoot.gameObject);
            _tabs.Clear();
            _tabRoot = UIKit.Rect(Root, "Chapter tabs").Stretch();
            var style = TextStyle.Label.WithSize(14).WithTracking(0.32f);
            float total = 0f;
            var widths = new List<float>();
            var chapters = _part != null ? _part.Chapters : Game.Library.Chapters;
            foreach (var c in chapters)
            {
                var probe = UIKit.Text(_tabRoot, $"{c.Numeral}    {c.Title}", style);
                probe.enabled = false;
                float w = probe.MeasureWidth() + (Game.Progress.IsUnlocked(c) ? 0f : 26f);
                Destroy(probe.gameObject);
                widths.Add(w);
                total += w;
            }
            const float gap = 84f;
            total += gap * (widths.Count - 1);
            float x = VoidFrame.Width * 0.5f - total * 0.5f;
            for (int i = 0; i < chapters.Count; i++)
            {
                var c = chapters[i];
                float w = widths[i];
                var rt = UIKit.Rect(_tabRoot, "Tab " + c.Numeral).At(x + w * 0.5f, 176f, w + 30f, 48f);
                var tab = new Tab { Chapter = c };
                bool open = Game.Progress.IsUnlocked(c);
                tab.Label = UIKit.Text(rt, $"{c.Numeral}    {c.Title}", style);
                tab.Label.rectTransform.Centered(open ? 0f : 13f, 0f, w, 30f);
                if (!open)
                {
                    tab.Lock = UIKit.Glyph(rt, "lock", Palette.Faint, 1.3f);
                    tab.Lock.rectTransform.Centered(-w * 0.5f + 7f, 1f, 13f, 13f);
                }
                tab.Underline = UIKit.Add<PathGraphic>(rt, "Underline");
                tab.Underline.raycastTarget = false;
                tab.Underline.rectTransform.Centered(0f, -24f, w, 6f);
                tab.Underline.BaseColor = Color.clear;
                tab.Underline.LitWidth = 1.6f;
                tab.Underline.Glow = 0.4f;
                tab.Underline.GlowRadius = 8f;
                tab.Underline.SetPoints(new List<Vector2> { new Vector2(-w * 0.5f, 0f), new Vector2(w * 0.5f, 0f) });
                UIKit.Hit(rt).rectTransform.Stretch();
                tab.Click = rt.gameObject.AddComponent<Clickable>();
                tab.Click.Clicked = () => ShowChapter(c, true);
                tab.Click.HoverChanged = _ => StyleTabs();
                _tabs.Add(tab);
                x += w + gap;
            }
        }

        void StyleTabs()
        {
            foreach (var tab in _tabs)
            {
                bool active = tab.Chapter == Chapter;
                bool open = Game.Progress.IsUnlocked(tab.Chapter);
                tab.Label.color = active ? Palette.Text : tab.Click.Hovered ? Palette.Dim : open ? Palette.Dim : Palette.Faint;
                tab.Underline.SetLit(0f, active ? 1f : 0f);
                tab.Underline.LitAlpha = active ? 1f : 0f;
            }
        }

        void ShowChapter(ChapterDef chapter, bool animate)
        {
            if (chapter == null) return;
            if (animate && chapter == Chapter && _page != null) return;
            Chapter = chapter;
            if (chapter.Part != _part || _tabRoot == null)
            {
                _part = chapter.Part;
                BuildTabs();
                StylePartTabs();
            }
            StyleTabs();
            if (_page != null)
            {
                var old = _page;
                var oldGroup = old.Group();
                Tweener.Run(old, "fade", 0.15f, t => oldGroup.alpha = 1f - t, Ease.OutQuad, 0f, () => Destroy(old.gameObject));
            }
            _nodes.Clear();
            _page = UIKit.Rect(Root, "Page " + chapter.Numeral).Stretch();
            var group = _page.Group();
            group.alpha = 0f;
            var page = _page;
            Tweener.Run(page, "fade", 0.3f, t => group.alpha = t, Ease.OutCubic, animate ? 0.1f : 0f);

            bool open = Game.Progress.IsUnlocked(chapter);
            int solved = Game.Progress.SolvedIn(chapter);
            float cx = VoidFrame.Width * 0.5f;

            if (Game.Library.Parts.Count > 1 && chapter.Part != null && !string.IsNullOrEmpty(chapter.Part.Tagline))
            {
                var partLine = UIKit.Text(_page, chapter.Part.Tagline, TextStyle.Label.WithSize(11).WithTracking(0.34f).WithColor(Palette.Faint));
                partLine.rectTransform.At(cx, 244f, 1200f, 16f);
            }
            var name = UIKit.Text(_page, chapter.Title, TextStyle.Heading.WithSize(58).WithWeight(200).WithColor(open ? Palette.Text : Palette.Dim));
            name.rectTransform.At(cx, 296f, 1200f, 80f);
            var tagline = UIKit.Text(_page, chapter.Tagline, TextStyle.Body.WithSize(22).WithColor(open ? Palette.Dim : Palette.Faint));
            tagline.rectTransform.At(cx, 356f, 1200f, 32f);

            string signals = chapter.Wiring ? "WIRE IT YOURSELF  ·  EVERY KIND OF SIGNAL" : $"SIGNALS: {Media.Noun(chapter.Medium).ToUpperInvariant()}S";
            string countText;
            if (open) countText = $"{signals}  ·  {solved} OF {chapter.Puzzles.Count} SOLVED";
            else if (!Game.Progress.IsUnlocked(chapter.Part))
            {
                var before = Game.Progress.PartBefore(chapter.Part);
                countText = before != null
                    ? $"OPENS WHEN EVERY CIRCUIT IN {before.Title.ToUpperInvariant()} IS SOLVED  ·  {Game.Progress.SolvedIn(before)} OF {Progress.CountIn(before)} SO FAR"
                    : "LOCKED";
            }
            else
            {
                var before = Game.Library.FindChapter(chapter.UnlockAfter);
                int need = Mathf.Min(chapter.UnlockSolved, before != null ? before.Puzzles.Count : 0);
                int have = before != null ? Game.Progress.SolvedIn(before) : 0;
                countText = before != null ? $"{signals}  ·  SOLVE {need} IN {before.Title.ToUpperInvariant()}  ·  {have} OF {need} SO FAR" : "LOCKED";
            }
            var count = UIKit.Text(_page, countText, TextStyle.Label.WithSize(12).WithTracking(0.3f).WithColor(open ? Palette.Faint : Palette.Dim));
            count.rectTransform.At(cx, 404f, 1000f, 18f);

            BuildMap(chapter, open, animate);
            BuildInfo();

            var current = Game.Progress.CurrentIn(chapter);
            _focus = current != null ? current.Number : 0;
            if (!open) _focus = 0;
            Restyle();
        }

        void BuildMap(ChapterDef chapter, bool open, bool animate)
        {
            int n = chapter.Puzzles.Count;
            int perRow = (n + 1) / 2;
            float spacing = perRow > 1 ? Mathf.Min(300f, 1100f / (perRow - 1)) : 0f;
            float x0 = VoidFrame.Width * 0.5f - spacing * (perRow - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                bool second = i >= perRow;
                int k = second ? (perRow - 1) - (i - perRow) : i;
                _nodes.Add(new Node { Puzzle = chapter.Puzzles[i], At = new Vector2(x0 + k * spacing, second ? RowB : RowA) });
            }

            var wires = UIKit.Rect(_page, "Wires").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            Vector2 L(Vector2 frame) => new Vector2(frame.x - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - frame.y);

            // The signal enters on the left, runs through every circuit and leaves at the end.
            var first = _nodes[0].At;
            var segments = new List<(List<Vector2> points, bool lit)>();
            segments.Add((new List<Vector2> { L(first + new Vector2(-170f, 0f)), L(first) }, open));
            float turnX = x0 + spacing * (perRow - 1) + 150f;
            for (int i = 0; i + 1 < n; i++)
            {
                var a = _nodes[i].At;
                var b = _nodes[i + 1].At;
                var pts = new List<Vector2>();
                if (a.y != b.y)
                {
                    var corners = new List<Vector2> { L(a), L(new Vector2(turnX, a.y)), L(new Vector2(turnX, b.y)), L(b) };
                    MeshKit.RoundedPolyline(pts, corners, 60f);
                }
                else
                {
                    pts.Add(L(a));
                    pts.Add(L(b));
                }
                segments.Add((pts, open && Game.Progress.IsSolved(_nodes[i].Puzzle)));
            }
            var last = _nodes[n - 1].At;
            float outDir = (n - 1) >= perRow ? -1f : 1f;
            var end = last + new Vector2(170f * outDir, 0f);
            bool allSolved = open && Game.Progress.SolvedIn(chapter) == n;
            segments.Add((new List<Vector2> { L(last), L(end) }, allSolved));

            float delay = 0.15f;
            foreach (var (points, lit) in segments)
            {
                var path = UIKit.Add<PathGraphic>(wires, "Wire");
                path.raycastTarget = false;
                path.rectTransform.Stretch();
                path.BaseColor = Palette.Rule;
                path.BaseWidth = 1.5f;
                path.LitWidth = 2f;
                path.Glow = 0.35f;
                path.GlowRadius = 12f;
                path.SetPoints(points);
                path.SetLit(0f, 0f);
                path.LitAlpha = lit ? 1f : 0f;
                if (!lit) continue;
                var flow = UIKit.Add<FlowGraphic>(wires, "Flow");
                flow.raycastTarget = false;
                flow.rectTransform.Stretch();
                flow.Source = path;
                flow.Animate = !App.ReduceMotion;
                if (!animate || App.ReduceMotion) path.SetLit(0f, 1f);
                else
                {
                    var p = path;
                    Tweener.Run(p, "light", 0.22f, t => p.SetLit(0f, Mathf.Max(0.0001f, t)), Ease.Linear, delay);
                    delay += 0.12f;
                }
            }

            // The end of the line: an output ring that lights when the chapter is done.
            var ring = UIKit.Shape(wires, "End", ShapeKind.Circle);
            ring.rectTransform.Centered(L(end).x, L(end).y, 14f, 14f);
            ring.Style(allSolved ? Paint.Signal : Paint.None, Color.white, allSolved ? Paint.None : Paint.Solid, Palette.Outline, 1.4f);
            ring.Glow = allSolved ? 0.5f : 0f;
            ring.GlowPaint = Paint.Signal;
            ring.GlowRadius = 14f;
            var startDot = UIKit.Shape(wires, "Start", ShapeKind.Circle);
            var s = L(first + new Vector2(-170f, 0f));
            startDot.rectTransform.Centered(s.x, s.y, 9f, 9f);
            startDot.Style(open ? Paint.Signal : Paint.Solid, open ? Color.white : Palette.Outline, Paint.None, Color.clear);
            startDot.Glow = open ? 0.5f : 0f;
            startDot.GlowPaint = Paint.Signal;
            startDot.GlowRadius = 10f;

            for (int i = 0; i < n; i++) BuildNode(_nodes[i], i, open);
        }

        void BuildNode(Node node, int index, bool chapterOpen)
        {
            var p = node.Puzzle;
            node.Solved = chapterOpen && Game.Progress.IsSolved(p);
            node.Open = chapterOpen && Game.Progress.IsUnlocked(p);
            node.Current = chapterOpen && Game.Progress.CurrentIn(p.Chapter) == p;

            var rt = UIKit.Rect(_page, "Circuit " + p.Label).At(node.At.x, node.At.y, 96f, 96f);
            node.Ring = UIKit.Shape(rt, "Ring", ShapeKind.Circle);
            node.Ring.rectTransform.Centered(0f, 0f, 64f, 64f);
            node.Number = UIKit.Text(rt, p.Label, TextStyle.Digit.WithSize(18).WithWeight(300));
            node.Number.rectTransform.Centered(0f, 0f, 60f, 30f);
            if (!node.Open)
            {
                node.Lock = UIKit.Glyph(rt, "lock", Palette.Faint, 1.4f);
                node.Lock.rectTransform.Centered(0f, 0f, 16f, 16f);
                node.Number.enabled = false;
            }
            node.Name = UIKit.Text(_page, node.Open ? p.Name : "", TextStyle.Label.WithSize(12).WithTracking(0.22f));
            node.Name.rectTransform.At(node.At.x, node.At.y + 60f, 260f, 18f);

            UIKit.Hit(rt).rectTransform.Stretch();
            node.Click = rt.gameObject.AddComponent<Clickable>();
            node.Click.HoverSound = false;
            node.Click.ClickSound = Sfx.Hover;
            node.Click.HoverChanged = on => { if (on && _focus != index) { _focus = index; App.Sound(Sfx.Hover); Restyle(); } };
            node.Click.Clicked = () => { _focus = index; Open(); };
        }

        void BuildInfo()
        {
            float cx = VoidFrame.Width * 0.5f;
            _infoNumber = UIKit.Text(_page, "", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _infoNumber.rectTransform.At(cx, 880f, 600f, 18f);
            _infoName = UIKit.Text(_page, "", TextStyle.Heading.WithSize(30).WithWeight(300));
            _infoName.rectTransform.At(cx, 918f, 900f, 44f);
            _infoStatus = UIKit.Text(_page, "", TextStyle.Label.WithSize(12).WithTracking(0.3f));
            _infoStatus.rectTransform.At(cx, 958f, 900f, 18f);
        }

        void Restyle()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                bool focused = i == _focus;
                var ring = node.Ring;
                if (node.Solved)
                {
                    ring.Style(Paint.Solid, focused ? Palette.Surface : Color.black, Paint.Signal, Color.white, focused ? 2f : 1.6f);
                    ring.Glow = focused ? 0.5f : 0.32f;
                    ring.GlowPaint = Paint.Signal;
                }
                else if (node.Open)
                {
                    ring.Style(Paint.Solid, focused ? Palette.Surface : Color.black, node.Current ? Paint.Signal : Paint.Solid, node.Current ? Color.white : focused ? Palette.Text : Palette.Dim, focused ? 1.8f : 1.4f);
                    ring.Glow = node.Current ? 0.25f : 0f;
                    ring.GlowPaint = Paint.Signal;
                }
                else
                {
                    ring.Style(Paint.Solid, Color.black, Paint.Solid, focused ? Palette.Faint : Palette.Outline, 1.3f);
                    ring.Glow = 0f;
                }
                ring.Dashed = !node.Open;
                ring.GlowRadius = 16f;
                node.Number.color = node.Solved || focused ? Palette.Text : Palette.Dim;
                node.Name.color = focused ? Palette.Text : node.Solved ? Palette.Dim : Palette.Faint;
            }

            if (_nodes.Count == 0) return;
            var f = _nodes[Mathf.Clamp(_focus, 0, _nodes.Count - 1)];
            var p = f.Puzzle;
            _infoNumber.text = $"CIRCUIT {p.Label}";
            _infoName.text = f.Open ? p.Name : "Locked";
            _infoName.color = f.Open ? Palette.Text : Palette.Faint;
            if (f.Solved)
            {
                Game.Save.BestMoves.TryGetValue(p.Id, out int best);
                _infoStatus.text = best > 0 ? $"SOLVED  ·  BEST {best} {(best == 1 ? "MOVE" : "MOVES")}" : "SOLVED";
                _infoStatus.color = Palette.Cyan;
            }
            else if (f.Open)
            {
                _infoStatus.text = "NOT YET SOLVED";
                _infoStatus.color = Palette.Dim;
            }
            else if (!Game.Progress.IsUnlocked(Chapter.Part))
            {
                _infoStatus.text = $"{Chapter.Part.Title.ToUpperInvariant()} IS LOCKED";
                _infoStatus.color = Palette.Faint;
            }
            else if (!Game.Progress.IsUnlocked(Chapter))
            {
                _infoStatus.text = "THIS CHAPTER IS LOCKED";
                _infoStatus.color = Palette.Faint;
            }
            else
            {
                int solved = Game.Progress.SolvedIn(Chapter);
                int need = Mathf.Max(1, p.Number - 1) - solved;
                _infoStatus.text = need <= 1 ? "SOLVE ONE MORE TO OPEN" : $"SOLVE {need} MORE TO OPEN";
                _infoStatus.color = Palette.Faint;
            }
        }

        void Open()
        {
            if (_nodes.Count == 0) return;
            var node = _nodes[_focus];
            if (!node.Open)
            {
                App.Sound(Sfx.Miss, 1.4f, 0.4f);
                return;
            }
            App.Sound(Sfx.Click);
            Game.GoPuzzle(node.Puzzle);
        }

        void Move(int direction)
        {
            if (_nodes.Count == 0) return;
            int next = Mathf.Clamp(_focus + direction, 0, _nodes.Count - 1);
            if (next == _focus) return;
            _focus = next;
            App.Sound(Sfx.Hover);
            Restyle();
        }

        void SwitchChapter(int direction)
        {
            var chapters = Game.Library.Chapters;
            int i = chapters.IndexOf(Chapter);
            int next = (i + direction + chapters.Count) % chapters.Count;
            if (next == i) return;
            App.Sound(Sfx.Click);
            ShowChapter(chapters[next], true);
        }

        void Update()
        {
            if (_nodes.Count == 0 || App.ReduceMotion) return;
            // The current circuit breathes.
            _pulse += Time.unscaledDeltaTime;
            foreach (var node in _nodes)
            {
                if (!node.Current) continue;
                node.Ring.Glow = 0.18f + 0.22f * (0.5f + 0.5f * Mathf.Sin(_pulse * Mathf.PI * 2f / 2.2f));
            }
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape)) { App.Sound(Sfx.Close); Game.GoTitle(); return; }
            if (GameInput.Down(Key.Tab)) { SwitchChapter(GameInput.Held(Key.Shift) ? -1 : 1); return; }
            if (GameInput.Confirm) { Open(); return; }
            int perRow = (_nodes.Count + 1) / 2;
            bool second = _focus >= perRow;
            // The second row runs right to left, so arrows follow the wire.
            if (GameInput.Down(Key.Right)) Move(second ? -1 : 1);
            else if (GameInput.Down(Key.Left)) Move(second ? 1 : -1);
            else if (GameInput.Down(Key.Down) && !second) Move(Mathf.Clamp(2 * perRow - 1 - _focus, 0, _nodes.Count - 1) - _focus);
            else if (GameInput.Down(Key.Up) && second) Move((2 * perRow - 1 - _focus) - _focus);
        }
    }

    /// <summary>Shown when the puzzle files can't be read, with the loader's message.</summary>
    public sealed class ErrorScreen : GameScreen
    {
        public string Message;

        protected internal override void Build()
        {
            float cx = VoidFrame.Width * 0.5f;
            var title = UIKit.Text(Root, "THE CIRCUITS COULDN'T BE LOADED", TextStyle.Label.WithSize(15).WithTracking(0.36f).WithColor(Palette.Violet));
            title.rectTransform.At(cx, 400f, 1200f, 22f);
            var body = UIKit.Text(Root, Message ?? "", TextStyle.Body.WithSize(22).WithColor(Palette.Text));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.rectTransform.At(cx, 520f, 1200f, 200f);
            var help = UIKit.Text(Root, "Fix the file under Puzzles/Resources/BlackboxPuzzles and press Play again. BLACKBOX > Validate Puzzles checks every file.", TextStyle.Body.WithSize(18));
            help.horizontalOverflow = HorizontalWrapMode.Wrap;
            help.rectTransform.At(cx, 700f, 1200f, 80f);
        }
    }
}
