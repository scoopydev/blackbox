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
    /// The title: the wordmark, a small circuit beneath it, and the main menu.
    /// The circuit is drawn exactly as the circuit screen draws one: an input, a
    /// block and a display, wired and showing their signals. Each time the title
    /// opens, and every few seconds after, it picks a new kind of signal, a new
    /// input and a new block at random.
    /// </summary>
    public sealed class TitleScreen : GameScreen, IWiringHost
    {
        /// <summary>A circuit that always loads, if a random one is ever refused.</summary>
        const string FallbackJson = @"{
            ""id"": ""title"", ""name"": ""BLACKBOX"", ""mode"": ""wiring"",
            ""inputs"": { ""A"": { ""signal"": ""binary 0110"" } },
            ""blocks"": { ""k"": { ""kind"": ""NOT"" } },
            ""outputs"": { ""X"": { ""device"": ""display"" } },
            ""solution"": [""A -> k"", ""k -> X""]
        }";

        /// <summary>How long each random circuit stays before the next fades in.</summary>
        const float Hold = 7f;
        const float Fade = 0.6f;
        /// <summary>Where the circuit sits: centred under the wordmark, above the menu.</summary>
        static readonly Rect DemoArea = new Rect(460f, 360f, 1000f, 230f);

        MenuList _menu;
        WiringBoard _board;
        float _timer;
        static readonly System.Random s_Random = new System.Random();

        protected internal override void Build()
        {
            float cx = VoidFrame.Width * 0.5f;
            var mark = UIKit.Text(Root, "BLACKBOX", TextStyle.Display.WithSize(84), TextAnchor.MiddleCenter, "Wordmark");
            mark.rectTransform.At(cx, 236f, 1400f, 120f);
            var tagline = UIKit.Text(Root, "LEARN THE RULES  ·  COMPLETE THE CIRCUIT", TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Faint));
            tagline.rectTransform.At(cx, 314f, 900f, 20f);

            ShowDemo(false);

            _menu = new MenuList();
            var progress = Game.Progress;
            bool started = progress.TotalSolved > 0 || !string.IsNullOrEmpty(Game.Save.LastPuzzle);
            var target = progress.ContinueTarget();
            float y = 652f;
            const float step = 58f;
            _menu.Add(MenuItem.Create(Root, started ? "CONTINUE" : "START", cx, y), () => Game.GoPuzzle(target));
            if (target != null && target.Chapter != null)
            {
                // Where Continue goes, so the player knows before pressing it.
                var where = UIKit.Text(Root, $"CHAPTER {target.Chapter.Numeral}  ·  {target.Name}",
                    TextStyle.Label.WithSize(11).WithTracking(0.26f).WithColor(Palette.Faint));
                where.rectTransform.At(cx, y + 28f, 900f, 16f);
                y += 22f;
            }
            _menu.Add(MenuItem.Create(Root, "CHAPTERS", cx, y += step), () => Game.GoChapters(null));
            _menu.Add(MenuItem.Create(Root, "HOW TO PLAY", cx, y += step), OpenHelp);
            _menu.Add(MenuItem.Create(Root, "SETTINGS", cx, y += step), Game.OpenSettings);
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                _menu.Add(MenuItem.Create(Root, "QUIT", cx, y += step), Game.Quit);

            int total = 0;
            foreach (var c in Game.Library.Chapters) total += c.Puzzles.Count;
            if (progress.TotalSolved > 0 && total > 0) Chrome.ProgressBar(Root, cx, 1032f, 260f, progress.TotalSolved, total, "CIRCUITS SOLVED");
            var version = UIKit.Text(Root, "V" + Application.version, TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            version.rectTransform.At(90f + 100f, 1032f, 200f, 16f);
            Chrome.KeyHints(Root, 1830f, 1032f, true, "ARROWS", "MOVE", "ENTER", "CHOOSE");
        }

        /// <summary>A one-block circuit with a random signal and block of the same kind.</summary>
        static string RandomJson()
        {
            var r = s_Random;
            string signal, kind;
            switch (r.Next(3))
            {
                case 0:
                {
                    // A run of bits that isn't all one value.
                    string bits;
                    do
                    {
                        var b = new System.Text.StringBuilder();
                        for (int i = 0; i < 4; i++) b.Append(r.Next(2));
                        bits = b.ToString();
                    } while (bits == "0000" || bits == "1111");
                    signal = "binary " + bits;
                    kind = "NOT";
                    break;
                }
                case 1:
                {
                    var b = new System.Text.StringBuilder();
                    int n = r.Next(2) == 0 ? 1 : 4;
                    for (int i = 0; i < n; i++) b.Append(r.Next(10));
                    signal = "decimal " + b;
                    kind = "MIRROR";
                    break;
                }
                default:
                {
                    string[] forms = { "sine", "square", "saw" };
                    signal = $"wave {forms[r.Next(forms.Length)]} f{1 + r.Next(3)} a{3 + r.Next(5)}";
                    string[] kinds = { "FLIP", "FASTER", "LATER" };
                    kind = kinds[r.Next(kinds.Length)];
                    break;
                }
            }
            return @"{
            ""id"": ""title"", ""name"": ""BLACKBOX"", ""mode"": ""wiring"",
            ""inputs"": { ""A"": { ""signal"": """ + signal + @""" } },
            ""blocks"": { ""k"": { ""kind"": """ + kind + @""" } },
            ""outputs"": { ""X"": { ""device"": ""display"" } },
            ""solution"": [""A -> k"", ""k -> X""]
        }";
        }

        static PuzzleDef Load(string json) => PuzzleLoader.ReadPuzzle(JsonValue.Parse(json), "title", Medium.Binary, true);

        /// <summary>Builds a new random circuit, centred, and fades it in over the old one.</summary>
        void ShowDemo(bool fade)
        {
            PuzzleDef def;
            try { def = Load(RandomJson()); }
            catch (System.Exception e)
            {
                Debug.LogWarning("BLACKBOX title circuit: " + e.Message);
                def = Load(FallbackJson);
            }
            var session = new WiringSession(def, new List<string> { "A -> k", "k -> X" });

            var metrics = BoardMetrics.Fit(def, DemoArea);
            metrics.Scale = 0.86f;
            metrics.ColSpacing = 330f;
            var old = _board;
            _board = WiringBoard.Create(Root, session, this, null, metrics);
            _board.transform.SetSiblingIndex(2);
            // Centre on what's drawn: the output card, with its target, is wider than the input.
            var (min, max) = _board.Extent();
            ((RectTransform)_board.transform).anchoredPosition = new Vector2(-(min + max) * 0.5f, 0f);
            session.Run();
            _board.ShowAll(session.LastRun);

            // Just to look at: the menu and the keys stay in charge.
            var group = _board.Group();
            group.blocksRaycasts = false;
            group.interactable = false;
            if (fade && !App.ReduceMotion)
            {
                group.alpha = 0f;
                var board = _board;
                Tweener.Run(board, "fade", Fade, t => group.alpha = t, Ease.OutCubic, Fade * 0.5f);
            }
            if (old != null)
            {
                var oldGroup = old.Group();
                Tweener.Run(old, "fade", fade ? Fade * 0.6f : 0.01f, t => oldGroup.alpha = 1f - t, Ease.OutQuad, 0f, () => Destroy(old.gameObject));
            }
            _timer = Hold;
        }

        void Update()
        {
            if (!Built || _board == null) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f) ShowDemo(true);
        }

        void OpenHelp()
        {
            var help = Game.Screens.Create<HelpOverlay>();
            help.Wiring = true;
            Game.Screens.Push(help);
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.H)) { OpenHelp(); return; }
            _menu.HandleKeys();
        }

        // IWiringHost: the title's circuit only shows its signals.
        public bool CanWire => false;
        public void Say(string message, float seconds) { }
        public void PlayTrace(Trace trace) { }
        public void BlocksMoved() { }
    }

    /// <summary>
    /// Every chapter at once. The list down the left holds the parts and their
    /// chapters with how far the player has got in each; the right shows the
    /// chosen chapter: its circuits strung along one wire that lights up as far
    /// as the player has solved, and the chosen circuit with a PLAY button.
    /// Up and down choose a chapter, left and right a circuit, Enter plays.
    /// </summary>
    public sealed class ChapterScreen : GameScreen
    {
        public ChapterDef Chapter;

        sealed class Row
        {
            public ChapterDef Chapter;
            public RectTransform Rect;
            public ShapeGraphic Back;
            public PathGraphic Bar;
            public VoidText Numeral, Title, Count;
            public Clickable Click;
            public bool Open, Done;
        }

        sealed class Node
        {
            public PuzzleDef Puzzle;
            public Vector2 At; // frame coordinates, y down
            public ShapeGraphic Ring;
            public VoidText Number;
            public GlyphGraphic Lock;
            public Clickable Click;
            public bool Solved, Open, Current;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly List<Node> _nodes = new List<Node>();
        RectTransform _page;
        int _focus;
        float _pulse;
        VoidText _infoNumber, _infoName, _infoStatus;
        GlyphGraphic _caret;
        PillButton _play;

        const float ListLeft = 90f;
        const float ListRight = 520f;
        const float Divider = 590f;
        const float PanelX = 1210f; // the centre of the panel right of the list
        const float MapY = 560f;

        static readonly string[] PartWords = { "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX" };

        protected internal override void Build()
        {
            if (Chapter == null) Chapter = Game.Progress.CurrentChapter();
            Chrome.Header(Root, "CHAPTERS", "BACK", () => { App.Sound(Sfx.Close); Game.GoTitle(); });

            var divider = UIKit.Add<PathGraphic>(Root, "Divider");
            divider.raycastTarget = false;
            divider.rectTransform.At(Divider, 570f, 4f, 820f);
            divider.BaseColor = Palette.Rule;
            divider.BaseWidth = 1f;
            divider.SetPoints(new List<Vector2> { new Vector2(0f, 410f), new Vector2(0f, -410f) });

            BuildList();
            Chrome.KeyHintsCentred(Root, VoidFrame.Width * 0.5f, 1032f, "UP DOWN", "CHAPTER", "LEFT RIGHT", "CIRCUIT", "ENTER", "PLAY", "ESC", "BACK");
            ShowChapter(Chapter, false);
        }

        // ------------------------------------------------------------------ the list of chapters

        void BuildList()
        {
            var parts = Game.Library.Parts;
            bool headed = parts.Count > 1;
            float y = 176f;
            if (headed)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (i > 0) y += 20f;
                    string word = part.Number >= 0 && part.Number < PartWords.Length ? PartWords[part.Number] : (part.Number + 1).ToString();
                    bool open = Game.Progress.IsUnlocked(part);
                    var head = UIKit.Text(Root, $"PART {word}  ·  {part.Title}", TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(open ? Palette.Dim : Palette.Faint), TextAnchor.MiddleLeft, "Part " + part.Id);
                    head.rectTransform.At(ListLeft + 20f + 200f, y, 400f, 18f);
                    if (!open)
                    {
                        var lockGlyph = UIKit.Glyph(Root, "lock", Palette.Faint, 1.2f);
                        lockGlyph.rectTransform.At(ListRight - 6f, y, 12f, 12f);
                    }
                    y += 40f;
                    foreach (var c in part.Chapters) { AddRow(c, y); y += 52f; }
                }
            }
            else
            {
                foreach (var c in Game.Library.Chapters) { AddRow(c, y); y += 52f; }
            }
        }

        void AddRow(ChapterDef chapter, float y)
        {
            var row = new Row { Chapter = chapter };
            row.Open = Game.Progress.IsUnlocked(chapter);
            int solved = row.Open ? Game.Progress.SolvedIn(chapter) : 0;
            row.Done = row.Open && solved >= chapter.Puzzles.Count;
            float w = ListRight - ListLeft;
            row.Rect = UIKit.Rect(Root, "Chapter " + chapter.Numeral).At((ListLeft + ListRight) * 0.5f, y, w, 46f);

            row.Back = UIKit.Shape(row.Rect, "Back");
            row.Back.Radius = 8f;
            row.Back.rectTransform.Stretch();
            row.Bar = UIKit.Add<PathGraphic>(row.Rect, "Mark");
            row.Bar.raycastTarget = false;
            row.Bar.rectTransform.Centered(-w * 0.5f + 1f, 0f, 6f, 26f);
            row.Bar.BaseColor = Color.clear;
            row.Bar.LitWidth = 2f;
            row.Bar.Glow = 0.5f;
            row.Bar.GlowRadius = 10f;
            row.Bar.SetPoints(new List<Vector2> { new Vector2(0f, 13f), new Vector2(0f, -13f) });

            row.Numeral = UIKit.Text(row.Rect, chapter.Numeral, TextStyle.Label.WithSize(12).WithTracking(0.2f), TextAnchor.MiddleLeft, "Numeral");
            row.Numeral.rectTransform.Centered(-w * 0.5f + 20f + 25f, 0f, 50f, 24f);
            row.Title = UIKit.Text(row.Rect, chapter.Title, TextStyle.Label.WithSize(15).WithTracking(0.26f), TextAnchor.MiddleLeft, "Title");
            row.Title.rectTransform.Centered(-w * 0.5f + 74f + 140f, 0f, 280f, 24f);
            if (row.Open)
            {
                row.Count = UIKit.Text(row.Rect, $"{solved}/{chapter.Puzzles.Count}", TextStyle.Digit.WithSize(14), TextAnchor.MiddleRight, "Count");
                row.Count.rectTransform.Centered(w * 0.5f - 16f - 30f, 0f, 60f, 24f);
                row.Count.SignalFill = row.Done;
            }
            else
            {
                var lockGlyph = UIKit.Glyph(row.Rect, "lock", Palette.Faint, 1.2f);
                lockGlyph.rectTransform.Centered(w * 0.5f - 22f, 0f, 12f, 12f);
            }

            UIKit.Hit(row.Rect).rectTransform.Stretch();
            row.Click = row.Rect.gameObject.AddComponent<Clickable>();
            row.Click.ClickSound = Sfx.Hover;
            row.Click.Clicked = () => { if (Chapter != chapter) App.Sound(Sfx.Click); ShowChapter(chapter, true); };
            row.Click.HoverChanged = _ => StyleRows();
            _rows.Add(row);
        }

        void StyleRows()
        {
            foreach (var row in _rows)
            {
                bool active = row.Chapter == Chapter;
                bool hot = row.Click.Hovered;
                row.Back.Style(Paint.Solid, active ? Palette.Surface : hot ? Palette.Surface.WithAlpha(0.6f) : Color.clear, Paint.Solid, active ? Palette.Outline : Color.clear, 1f);
                row.Bar.SetLit(0f, active ? 1f : 0f);
                row.Bar.LitAlpha = active ? 1f : 0f;
                Color text = active ? Palette.Text : hot ? Palette.Text : row.Open ? Palette.Dim : Palette.Faint;
                row.Title.color = text;
                row.Numeral.color = active ? Palette.Dim : Palette.Faint;
                if (row.Count != null)
                {
                    row.Count.color = row.Done ? Color.white : active ? Palette.Text : Palette.Faint;
                }
            }
        }

        // ------------------------------------------------------------------ the chosen chapter

        void ShowChapter(ChapterDef chapter, bool animate)
        {
            if (chapter == null) return;
            if (animate && chapter == Chapter && _page != null) return;
            Chapter = chapter;
            StyleRows();
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

            string where = $"CHAPTER {chapter.Numeral}";
            if (Game.Library.Parts.Count > 1 && chapter.Part != null) where += $"  ·  {chapter.Part.Title}";
            var label = UIKit.Text(_page, where, TextStyle.Label.WithSize(12).WithTracking(0.46f).WithColor(Palette.Faint));
            label.rectTransform.At(PanelX, 200f, 1100f, 18f);
            var name = UIKit.Text(_page, chapter.Title, TextStyle.Heading.WithSize(58).WithWeight(200).WithColor(open ? Palette.Text : Palette.Dim));
            name.rectTransform.At(PanelX, 258f, 1100f, 80f);
            var tagline = UIKit.Text(_page, chapter.Tagline, TextStyle.Body.WithSize(22).WithColor(open ? Palette.Dim : Palette.Faint));
            tagline.rectTransform.At(PanelX, 318f, 1100f, 32f);

            string signals = chapter.Mixed ? "EVERY KIND OF SIGNAL" : $"SIGNALS: {Media.Noun(chapter.Medium).ToUpperInvariant()}S";
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
                countText = before != null ? $"SOLVE {need} IN {before.Title.ToUpperInvariant()} TO OPEN  ·  {have} OF {need} SO FAR" : "LOCKED";
            }
            var count = UIKit.Text(_page, countText, TextStyle.Label.WithSize(12).WithTracking(0.3f).WithColor(open ? Palette.Faint : Palette.Dim));
            count.rectTransform.At(PanelX, 366f, 1100f, 18f);

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
            if (n == 0) return;
            const float lead = 130f;
            float spacing = n > 1 ? Mathf.Min(176f, 880f / (n - 1)) : 0f;
            float x0 = PanelX - spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
                _nodes.Add(new Node { Puzzle = chapter.Puzzles[i], At = new Vector2(x0 + i * spacing, MapY) });

            var wires = UIKit.Rect(_page, "Wires").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            Vector2 L(Vector2 frame) => new Vector2(frame.x - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - frame.y);

            // The signal enters on the left, runs through every circuit and leaves at the end.
            var first = _nodes[0].At;
            var last = _nodes[n - 1].At;
            var start = first - new Vector2(lead, 0f);
            var end = last + new Vector2(lead, 0f);
            var segments = new List<(List<Vector2> points, bool lit)>();
            segments.Add((new List<Vector2> { L(start), L(first) }, open));
            for (int i = 0; i + 1 < n; i++)
                segments.Add((new List<Vector2> { L(_nodes[i].At), L(_nodes[i + 1].At) }, open && Game.Progress.IsSolved(_nodes[i].Puzzle)));
            bool allSolved = open && Game.Progress.SolvedIn(chapter) == n;
            segments.Add((new List<Vector2> { L(last), L(end) }, allSolved));

            float delay = 0.15f;
            foreach (var (points, lit) in segments)
            {
                var path = UIKit.Add<PathGraphic>(wires, "Wire");
                path.raycastTarget = false;
                path.rectTransform.Stretch();
                path.BaseColor = Palette.Outline;
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
            startDot.rectTransform.Centered(L(start).x, L(start).y, 9f, 9f);
            startDot.Style(open ? Paint.Signal : Paint.Solid, open ? Color.white : Palette.Outline, Paint.None, Color.clear);
            startDot.Glow = open ? 0.5f : 0f;
            startDot.GlowPaint = Paint.Signal;
            startDot.GlowRadius = 10f;

            for (int i = 0; i < n; i++) BuildNode(_nodes[i], i, open);

            // A small mark under the chosen circuit.
            _caret = UIKit.Glyph(_page, "up", Palette.Text, 1.6f, "Caret");
            _caret.rectTransform.At(first.x, MapY + 56f, 14f, 14f);
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

            UIKit.Hit(rt).rectTransform.Stretch();
            node.Click = rt.gameObject.AddComponent<Clickable>();
            node.Click.HoverSound = false;
            node.Click.ClickSound = Sfx.Hover;
            node.Click.HoverChanged = on => { if (on && _focus != index) { _focus = index; App.Sound(Sfx.Hover); Restyle(); } };
            node.Click.Clicked = () => { _focus = index; Open(); };
        }

        void BuildInfo()
        {
            _infoNumber = UIKit.Text(_page, "", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _infoNumber.rectTransform.At(PanelX, 700f, 600f, 18f);
            _infoName = UIKit.Text(_page, "", TextStyle.Heading.WithSize(34).WithWeight(300));
            _infoName.rectTransform.At(PanelX, 742f, 1000f, 48f);
            _infoStatus = UIKit.Text(_page, "", TextStyle.Label.WithSize(12).WithTracking(0.3f));
            _infoStatus.rectTransform.At(PanelX, 786f, 1000f, 18f);
            _play = PillButton.Create(_page, "PLAY", PanelX, 868f, 220f, 52f, true, "play");
            _play.Click.ClickSound = Sfx.Hover;
            _play.Click.Clicked = Open;
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
            }

            if (_nodes.Count == 0) return;
            var f = _nodes[Mathf.Clamp(_focus, 0, _nodes.Count - 1)];
            var p = f.Puzzle;
            if (_caret != null)
            {
                _caret.rectTransform.At(f.At.x, MapY + 56f, 14f, 14f);
                _caret.Tint = f.Open ? Palette.Text : Palette.Faint;
            }
            _infoNumber.text = $"CIRCUIT {p.Label}  OF  {_nodes.Count:00}";
            _infoName.text = f.Open ? p.Name : "Locked";
            _infoName.color = f.Open ? Palette.Text : Palette.Faint;
            _play.Enabled = f.Open;
            _play.SetText(f.Solved ? "PLAY AGAIN" : f.Open && Game.Save.Circuits.ContainsKey(p.Id) ? "CONTINUE" : f.Open ? "PLAY" : "LOCKED");
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

        void SwitchChapter(int direction, bool wrap)
        {
            var chapters = Game.Library.Chapters;
            int i = chapters.IndexOf(Chapter);
            int next = wrap ? (i + direction + chapters.Count) % chapters.Count : Mathf.Clamp(i + direction, 0, chapters.Count - 1);
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
            if (GameInput.Down(Key.Escape) || GameInput.Down(Key.Backspace)) { App.Sound(Sfx.Close); Game.GoTitle(); return; }
            if (GameInput.Down(Key.Tab)) { SwitchChapter(GameInput.Held(Key.Shift) ? -1 : 1, true); return; }
            if (GameInput.Down(Key.Up)) { SwitchChapter(-1, false); return; }
            if (GameInput.Down(Key.Down)) { SwitchChapter(1, false); return; }
            if (GameInput.Confirm) { Open(); return; }
            if (GameInput.Down(Key.Right)) Move(1);
            else if (GameInput.Down(Key.Left)) Move(-1);
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
