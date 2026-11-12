using System;
using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using Blackbox.UI.Board;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>Esc during a circuit.</summary>
    public sealed class PauseOverlay : Overlay
    {
        public PuzzleDef Puzzle;
        /// <summary>Puts the circuit back where it started.</summary>
        public Action Reset;
        /// <summary>The question asked before resetting.</summary>
        public string ResetQuestion = "Put every input and block back where it started? Probes and notes stay.";
        MenuList _menu;

        protected override float VeilAlpha => 0.88f;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var label = UIKit.Text(Root, "PAUSED", TextStyle.Label.WithSize(14).WithTracking(0.42f).WithColor(Palette.Dim));
            label.rectTransform.At(cx, 300f, 400f, 20f);
            var p = Puzzle;
            var name = UIKit.Text(Root, p.Name, TextStyle.Heading.WithSize(40).WithWeight(200));
            name.rectTransform.At(cx, 350f, 900f, 56f);
            var where = UIKit.Text(Root, $"CHAPTER {p.Chapter.Numeral}  ·  CIRCUIT {p.Label}  OF  {p.Chapter.Puzzles.Count:00}", TextStyle.Label.WithSize(12).WithColor(Palette.Faint));
            where.rectTransform.At(cx, 396f, 600f, 18f);

            int solvedHere = Game.Progress.SolvedIn(p.Chapter);
            Chrome.ProgressBar(Root, cx, 436f, 180f, solvedHere, p.Chapter.Puzzles.Count, "SOLVED IN THIS CHAPTER");

            _menu = new MenuList();
            float y = 520f;
            const float step = 58f;
            _menu.Add(MenuItem.Create(Root, "RESUME", cx, y), () => { App.Sound(Sfx.Close); Close(); });
            _menu.Add(MenuItem.Create(Root, "HOW IT WORKS", cx, y += step), OpenHelp);
            if (SignalGuideOverlay.Has(p.Chapter)) _menu.Add(MenuItem.Create(Root, "SIGNAL GUIDE", cx, y += step), OpenGuide);
            _menu.Add(MenuItem.Create(Root, "RESET CIRCUIT", cx, y += step), () =>
                Game.Confirm(ResetQuestion, "RESET", false, () =>
                {
                    Close();
                    Reset?.Invoke();
                }));
            _menu.Add(MenuItem.Create(Root, "SETTINGS", cx, y += step), Game.OpenSettings);
            _menu.Add(MenuItem.Create(Root, "CHAPTERS", cx, y += step + 16f), () => Game.GoChapters(Puzzle.Chapter));
            _menu.Add(MenuItem.Create(Root, "MAIN MENU", cx, y += step), Game.GoTitle);

            if (SignalGuideOverlay.Has(p.Chapter))
                Chrome.KeyHintsCentred(Root, cx, 1000f, "UP DOWN", "MOVE", "ENTER", "CHOOSE", "H", "HELP", "G", "SIGNALS", "ESC", "RESUME");
            else
                Chrome.KeyHintsCentred(Root, cx, 1000f, "UP DOWN", "MOVE", "ENTER", "CHOOSE", "H", "HELP", "ESC", "RESUME");
        }

        void OpenGuide()
        {
            var help = Game.Screens.Create<HelpOverlay>();
            help.Chapter = Puzzle.Chapter;
            help.Wiring = Puzzle.Wiring;
            help.StartOnGuide = true;
            Game.Screens.Push(help);
        }

        void OpenHelp()
        {
            var help = Game.Screens.Create<HelpOverlay>();
            help.Chapter = Puzzle.Chapter;
            help.Wiring = Puzzle.Wiring;
            Game.Screens.Push(help);
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape))
            {
                App.Sound(Sfx.Close);
                Close();
                return;
            }
            if (GameInput.Down(Key.H)) { OpenHelp(); return; }
            if (GameInput.Down(Key.G) && SignalGuideOverlay.Has(Puzzle.Chapter)) { OpenGuide(); return; }
            _menu.HandleKeys();
        }
    }

    /// <summary>Volume, display and comfort settings, and erasing progress.</summary>
    public sealed class SettingsOverlay : Overlay
    {
        sealed class Row
        {
            public VoidText Label;
            public ShapeGraphic Dot;
            public VoidSlider Slider;
            public VoidText Amount;
            public VoidToggle Toggle;
            /// <summary>A row of options, such as 1X to 4X; one is chosen.</summary>
            public List<VoidText> Options;
            public int Chosen;
            public Action<int> Choose;
            public Action Activate;
            public bool Danger;
            public string About;
        }

        readonly List<Row> _rows = new List<Row>();
        int _focus;
        float _pulse;

        const float SectionRight = 640f;
        const float LabelLeft = 700f;
        const float ControlRight = 1240f;

        VoidText _about;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var title = UIKit.Text(Root, "SETTINGS", TextStyle.Label.WithSize(15).WithTracking(0.42f).WithColor(Palette.Text));
            title.rectTransform.At(cx, 150f, 400f, 22f);
            Chrome.KeyButton(Root, "ESC", "BACK", 1830f, 62f, true, () => { App.Sound(Sfx.Close); Close(); });
            UIKit.Rule(Root, SectionRight - 160f, ControlRight + 80f, 190f, Palette.Rule);

            var prefs = Game.Save.Prefs;
            const float step = 48f;
            const float gap = 22f;
            float y = 228f;
            Chrome.Section(Root, "SOUND", SectionRight, y);
            AddSlider("MASTER VOLUME", y, prefs.MasterVolume, v => { prefs.MasterVolume = v; Game.ApplyPrefs(); }, "Everything the game plays.");
            AddSlider("MUSIC", y += step, prefs.MusicVolume, v => { prefs.MusicVolume = v; Game.ApplyPrefs(); }, "The hum under every screen.");
            AddSlider("EFFECTS", y += step, prefs.EffectsVolume, v => { prefs.EffectsVolume = v; Game.ApplyPrefs(); }, "Clicks, wires and runs.");
            AddToggle("SIGNAL SOUNDS", y += step, prefs.SignalSounds, v => { prefs.SignalSounds = v; Game.MarkDirty(); }, "Plays each signal as you set inputs and run a circuit.");

            Chrome.Section(Root, "DISPLAY", SectionRight, y += step + gap);
            AddToggle("FULLSCREEN", y, prefs.Fullscreen, v => { prefs.Fullscreen = v; Game.ApplyPrefs(); }, "Fill the screen, or play in a window.");
            AddToggle("REDUCE MOTION", y += step, prefs.ReduceMotion, v => { prefs.ReduceMotion = v; Game.ApplyPrefs(); }, "Stills the flowing light and the pulsing marks.");

            Chrome.Section(Root, "SPEED", SectionRight, y += step + gap);
            AddChoice("RUN SPEED", y, prefs.RunSpeed - 1, Speeds, i => { prefs.RunSpeed = i + 1; Game.ApplyPrefs(); }, "How fast a run sends the signals through. Each output's sound still plays in full.");
            AddChoice("LOOP SPEED", y += step, prefs.LoopSpeed - 1, Speeds, i => { prefs.LoopSpeed = i + 1; Game.ApplyPrefs(); }, "How fast every signal's cursor sweeps, and how fast a changing signal plays.");

            Chrome.Section(Root, "DEBUG", SectionRight, y += step + gap);
            AddToggle("SHOW BLOCK RULES", y, prefs.DebugRules, v => { prefs.DebugRules = v; Game.MarkDirty(); }, "Writes what every block does under it. Spoils the puzzles.");
            AddToggle("OPEN EVERYTHING", y += step, prefs.DebugOpenAll, v => { prefs.DebugOpenAll = v; Game.MarkDirty(); }, "Every part, chapter and circuit can be played.");

            Chrome.Section(Root, "PROGRESS", SectionRight, y += step + gap);
            AddAction("ERASE PROGRESS", y, true, () =>
                Game.Confirm("Erase every solved circuit, every circuit in progress and every note you've written? Settings are kept.", "ERASE", true, () =>
                {
                    Game.EraseProgress();
                }), "Start again from the first circuit. Settings are kept.");

            UIKit.Rule(Root, SectionRight - 160f, ControlRight + 80f, y + 48f, Palette.Rule);
            AddAction("DONE", y += step + 36f, false, () => { App.Sound(Sfx.Close); Close(); }, "Back to where you were.");

            _about = UIKit.Text(Root, "", TextStyle.Body.WithSize(19).WithColor(Palette.Dim));
            _about.rectTransform.At(cx, 952f, 1200f, 28f);

            Chrome.KeyHintsCentred(Root, cx, 1032f, "UP DOWN", "MOVE", "LEFT RIGHT", "ADJUST", "ENTER", "SWITCH", "ESC", "BACK");
            Focus(0, false);
        }

        Row AddRow(string text, float y, string about)
        {
            var row = new Row { About = about };
            int index = _rows.Count;
            var hitRt = UIKit.Rect(Root, "Row " + text).At((LabelLeft - 40f + ControlRight + 80f) * 0.5f, y, ControlRight + 120f - LabelLeft, 46f);
            UIKit.Hit(hitRt).rectTransform.Stretch();
            var click = hitRt.gameObject.AddComponent<Clickable>();
            click.HoverSound = false;
            click.ClickSound = Sfx.Hover;
            click.HoverChanged = on => { if (on) Focus(index, true); };
            click.Clicked = () => _rows[index].Activate?.Invoke();

            row.Label = UIKit.Text(Root, text, TextStyle.Menu.WithSize(18), TextAnchor.MiddleLeft, "Label");
            row.Label.rectTransform.At(LabelLeft + 200f, y, 400f, 30f);
            row.Dot = UIKit.Shape(Root, "Focus", ShapeKind.Circle);
            row.Dot.rectTransform.At(LabelLeft - 24f, y, 7f, 7f);
            row.Dot.Style(Paint.Solid, Palette.Cyan, Paint.None, Color.clear);
            row.Dot.Glow = 0.5f;
            row.Dot.GlowPaint = Paint.Solid;
            row.Dot.GlowColor = Palette.Cyan;
            row.Dot.GlowRadius = 10f;
            _rows.Add(row);
            return row;
        }

        void AddSlider(string text, float y, float value, Action<float> changed, string about)
        {
            var row = AddRow(text, y, about);
            const float width = 240f;
            row.Slider = VoidSlider.Create(Root, ControlRight - width * 0.5f, y, width);
            row.Slider.Value = value;
            row.Amount = UIKit.Text(Root, Percent(value), TextStyle.Digit.WithSize(15).WithColor(Palette.Dim), TextAnchor.MiddleRight, "Amount");
            row.Amount.rectTransform.At(ControlRight + 60f, y, 50f, 24f);
            var r = row;
            row.Slider.Changed = v =>
            {
                r.Amount.text = Percent(v);
                changed(v);
            };
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f).ToString();

        void AddToggle(string text, float y, bool on, Action<bool> changed, string about)
        {
            var row = AddRow(text, y, about);
            row.Toggle = VoidToggle.Create(Root, ControlRight - 29f, y);
            row.Toggle.On = on;
            row.Toggle.Changed = changed;
            var r = row;
            row.Activate = () => { App.Sound(Sfx.Dial); r.Toggle.Flip(); };
        }

        static readonly string[] Speeds = { "1X", "2X", "3X", "4X" };

        void AddChoice(string text, float y, int chosen, string[] options, Action<int> changed, string about)
        {
            var row = AddRow(text, y, about);
            row.Options = new List<VoidText>();
            const float each = 62f;
            for (int i = 0; i < options.Length; i++)
            {
                float x = ControlRight - (options.Length - 1 - i) * each - 20f;
                var t = UIKit.Text(Root, options[i], TextStyle.Digit.WithSize(17), TextAnchor.MiddleCenter, "Option " + options[i]);
                t.rectTransform.At(x, y, each, 30f);
                var hit = UIKit.Hit(t.rectTransform);
                hit.rectTransform.Stretch();
                var click = t.gameObject.AddComponent<Clickable>();
                click.HoverSound = false;
                click.ClickSound = Sfx.Dial;
                int index = i, rowIndex = _rows.Count - 1;
                click.HoverChanged = on => { if (on) Focus(rowIndex, true); };
                click.Clicked = () => _rows[rowIndex].Choose(index);
                row.Options.Add(t);
            }
            var r = row;
            row.Chosen = Mathf.Clamp(chosen, 0, options.Length - 1);
            row.Choose = i =>
            {
                r.Chosen = Mathf.Clamp(i, 0, r.Options.Count - 1);
                StyleChoice(r);
                changed(r.Chosen);
            };
            // Enter or a click on the row steps to the next option, round to the first.
            row.Activate = () => { App.Sound(Sfx.Dial); r.Choose((r.Chosen + 1) % r.Options.Count); };
            StyleChoice(row);
        }

        static void StyleChoice(Row row)
        {
            for (int i = 0; i < row.Options.Count; i++)
                row.Options[i].color = i == row.Chosen ? Palette.Cyan : Palette.Faint;
        }

        void AddAction(string text, float y, bool danger, Action action, string about)
        {
            var row = AddRow(text, y, about);
            row.Danger = danger;
            row.Activate = () => { App.Sound(Sfx.Click); action(); };
        }

        void Focus(int index, bool sound)
        {
            index = Mathf.Clamp(index, 0, _rows.Count - 1);
            if (sound && index != _focus) App.Sound(Sfx.Hover);
            _focus = index;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool f = i == _focus;
                row.Label.color = f ? (row.Danger ? Palette.Violet : Palette.Text) : Palette.Dim;
                row.Dot.enabled = f;
                row.Dot.FillColor = row.Danger ? Palette.Violet : Palette.Cyan;
                row.Dot.GlowColor = row.Dot.FillColor;
                if (row.Slider != null) row.Slider.Focused = f;
                if (row.Toggle != null) row.Toggle.Focused = f;
            }
            if (_about != null)
            {
                var focused = _rows[_focus];
                _about.text = focused.About ?? "";
                _about.color = focused.Danger ? Palette.Violet : Palette.Dim;
            }
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape))
            {
                App.Sound(Sfx.Close);
                Close();
                return;
            }
            if (GameInput.Down(Key.Down)) Focus(_focus + 1, true);
            else if (GameInput.Down(Key.Up)) Focus(_focus - 1, true);

            var row = _rows[_focus];
            int dir = GameInput.Down(Key.Right) ? 1 : GameInput.Down(Key.Left) ? -1 : 0;
            if (dir != 0)
            {
                if (row.Slider != null) row.Slider.Nudge(dir * 0.1f);
                else if (row.Toggle != null && row.Toggle.On != dir > 0) { App.Sound(Sfx.Dial); row.Toggle.Flip(); }
                else if (row.Options != null)
                {
                    int next = Mathf.Clamp(row.Chosen + dir, 0, row.Options.Count - 1);
                    if (next != row.Chosen) { App.Sound(Sfx.Dial); row.Choose(next); }
                }
            }
            if (GameInput.Confirm) row.Activate?.Invoke();
        }

        void Update()
        {
            if (_rows.Count == 0) return;
            var dot = _rows[_focus].Dot;
            if (App.ReduceMotion) { dot.canvasRenderer.SetAlpha(1f); return; }
            _pulse += Time.unscaledDeltaTime;
            dot.canvasRenderer.SetAlpha(0.45f + 0.55f * (0.5f + 0.5f * Mathf.Cos(_pulse * Mathf.PI * 2f / 2.4f)));
        }
    }

    /// <summary>A yes-or-no question.</summary>
    public sealed class ConfirmOverlay : Overlay
    {
        public string Question;
        public string YesText = "YES";
        public bool Danger;
        public Action Yes;

        PillButton _yes, _no;
        bool _focusYes;


        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var panel = UIKit.Shape(Root, "Panel");
            panel.Radius = 18f;
            panel.rectTransform.At(cx, 515f, 980f, 330f);
            panel.Style(Paint.Solid, Palette.Surface, Paint.Solid, Danger ? Palette.Violet.WithAlpha(0.45f) : Palette.Outline, 1.2f);
            var q = UIKit.Text(Root, Question, TextStyle.Body.WithSize(26).WithColor(Palette.Text));
            q.horizontalOverflow = HorizontalWrapMode.Wrap;
            q.rectTransform.At(cx, 460f, 820f, 120f);

            _no = PillButton.Create(Root, "CANCEL", cx - 105f, 590f, 190f, 52f, false);
            _no.Click.ClickSound = Sfx.Close;
            _no.Click.Clicked = Close;
            _yes = PillButton.Create(Root, YesText, cx + 105f, 590f, 190f, 52f, false);
            _yes.Click.Clicked = Accept;
            _no.Click.HoverChanged += on => { if (on) SetFocus(false); };
            _yes.Click.HoverChanged += on => { if (on) SetFocus(true); };
            SetFocus(!Danger);

            Chrome.KeyHintsCentred(Root, cx, 1032f, "LEFT RIGHT", "CHOOSE", "ENTER", "OK", "ESC", "CANCEL");
            App.Sound(Sfx.Open, 0.9f, 0.6f);
        }

        void SetFocus(bool yes)
        {
            _focusYes = yes;
            _yes.Active = yes;
            _no.Active = !yes;
        }

        void Accept()
        {
            Close();
            Yes?.Invoke();
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape)) { App.Sound(Sfx.Close); Close(); return; }
            if (GameInput.Down(Key.Left) || GameInput.Down(Key.Right) || GameInput.Down(Key.Tab))
            {
                App.Sound(Sfx.Hover);
                SetFocus(!_focusYes);
            }
            if (GameInput.Confirm)
            {
                if (_focusYes) { App.Sound(Sfx.Click); Accept(); }
                else { App.Sound(Sfx.Close); Close(); }
            }
        }
    }

    /// <summary>
    /// How to play, every control, and the signal guides, one tab each. A guide's
    /// tab opens once the player has reached a chapter that uses it; the rest
    /// show a padlock. Left and right, or Tab, move between tabs.
    /// </summary>
    public sealed class HelpOverlay : Overlay
    {
        /// <summary>The chapter being played, if any: its guide counts as reached.</summary>
        public ChapterDef Chapter;
        /// <summary>Help for a circuit the player wires, rather than one they set.</summary>
        public bool Wiring = true;
        /// <summary>Open on this chapter's signal guide instead of How to Play.</summary>
        public bool StartOnGuide;

        sealed class Tab
        {
            public string Title;
            /// <summary>The first chapter with this guide, whose page is shown; null for How to Play.</summary>
            public ChapterDef Chapter;
            public bool Open;
            public VoidText Label;
            public PathGraphic Underline;
            public Clickable Click;
        }

        readonly List<Tab> _tabs = new List<Tab>();
        int _current = -1;
        RectTransform _page;

        static string GuideTitle(SignalGuide g)
        {
            switch (g)
            {
                case SignalGuide.Bits: return "BITS";
                case SignalGuide.Digits: return "DIGITS";
                case SignalGuide.ChangingDigits: return "CHANGING DIGITS";
                case SignalGuide.Waves: return "WAVES";
                case SignalGuide.Readings: return "MIXING KINDS";
                default: return "EVERY SIGNAL";
            }
        }

        /// <summary>
        /// A chapter counts as reached once its title card has been seen, a circuit
        /// in it solved, or it's the one being played. Every chapter can be played
        /// from the start, so "unlocked" alone would open every guide.
        /// </summary>
        bool Reached(ChapterDef c) =>
            c == Chapter || Game.Save.Prefs.DebugOpenAll || Game.Save.SeenIntros.Contains(c.Id) || Game.Progress.SolvedIn(c) > 0;

        protected internal override void Build()
        {
            BuildVeil();

            _tabs.Add(new Tab { Title = "HOW TO PLAY", Open = true });
            var seen = new List<SignalGuide>();
            int start = 0;
            foreach (var c in Game.Library.Chapters)
            {
                if (!SignalGuideOverlay.Has(c)) continue;
                int i = seen.IndexOf(c.Guide);
                if (i < 0)
                {
                    seen.Add(c.Guide);
                    _tabs.Add(new Tab { Title = GuideTitle(c.Guide), Chapter = c });
                    i = seen.Count - 1;
                }
                var tab = _tabs[i + 1];
                if (Reached(c)) tab.Open = true;
                if (StartOnGuide && c == Chapter) start = i + 1;
            }
            BuildTabs();

            Chrome.KeyButton(Root, "ESC", "CLOSE", 1830f, 44f, true, Close);
            Chrome.KeyHintsCentred(Root, VoidFrame.Width * 0.5f, 1040f, "LEFT RIGHT", "TAB", "ESC", "CLOSE");
            Show(start, false);
            App.Sound(Sfx.Open, 1.2f, 0.6f);
        }

        void BuildTabs()
        {
            var style = TextStyle.Label.WithSize(13).WithTracking(0.3f);
            var widths = new List<float>();
            float total = 0f;
            const float gap = 48f;
            foreach (var t in _tabs)
            {
                t.Label = UIKit.Text(Root, t.Title, style, TextAnchor.MiddleCenter, "Tab " + t.Title);
                float w = t.Label.MeasureWidth() + (t.Open ? 0f : 22f);
                widths.Add(w);
                total += w;
            }
            total += gap * (_tabs.Count - 1);
            float x = VoidFrame.Width * 0.5f - total * 0.5f;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                float w = widths[i];
                var rt = UIKit.Rect(Root, "Tab button " + t.Title).At(x + w * 0.5f, 44f, w + 24f, 44f);
                t.Label.rectTransform.SetParent(rt, false);
                t.Label.rectTransform.Centered(t.Open ? 0f : 11f, 0f, w, 24f);
                if (!t.Open)
                {
                    var lockGlyph = UIKit.Glyph(rt, "lock", Palette.Faint, 1.2f);
                    lockGlyph.rectTransform.Centered(-w * 0.5f + 6f, 1f, 12f, 12f);
                }
                t.Underline = UIKit.Add<PathGraphic>(rt, "Underline");
                t.Underline.raycastTarget = false;
                t.Underline.rectTransform.Centered(0f, -20f, w, 6f);
                t.Underline.BaseColor = Color.clear;
                t.Underline.LitWidth = 1.6f;
                t.Underline.Glow = 0.4f;
                t.Underline.GlowRadius = 8f;
                t.Underline.SetPoints(new List<Vector2> { new Vector2(-w * 0.5f, 0f), new Vector2(w * 0.5f, 0f) });
                UIKit.Hit(rt).rectTransform.Stretch();
                t.Click = rt.gameObject.AddComponent<Clickable>();
                t.Click.ClickSound = Sfx.Hover;
                int index = i;
                t.Click.Clicked = () => Show(index, true);
                t.Click.HoverChanged = _ => StyleTabs();
                x += w + gap;
            }
            UIKit.Rule(Root, 90f, 1830f, 72f, Palette.Rule);
        }

        void StyleTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                bool active = i == _current;
                t.Label.color = active ? Palette.Text : !t.Open ? Palette.Faint : t.Click.Hovered ? Palette.Text : Palette.Dim;
                t.Underline.SetLit(0f, active ? 1f : 0f);
                t.Underline.LitAlpha = active ? 1f : 0f;
            }
        }

        void Show(int index, bool sound)
        {
            if (index == _current) return;
            var tab = _tabs[index];
            if (!tab.Open)
            {
                App.Sound(Sfx.Miss, 1.4f, 0.4f);
                return;
            }
            if (sound) App.Sound(Sfx.Click);
            App.Audio?.StopSignals();
            _current = index;
            if (_page != null) Destroy(_page.gameObject);
            _page = UIKit.Rect(Root, "Page").Stretch();
            _page.SetSiblingIndex(1);
            if (tab.Chapter == null) BuildHowTo(_page);
            else SignalGuideOverlay.Embed(_page, tab.Chapter);
            StyleTabs();
        }

        void Step(int direction)
        {
            for (int k = 1; k < _tabs.Count; k++)
            {
                int i = (_current + direction * k + _tabs.Count * 4) % _tabs.Count;
                if (!_tabs[i].Open) continue;
                Show(i, true);
                return;
            }
        }

        void BuildHowTo(RectTransform page)
        {
            var title = UIKit.Text(page, "HOW IT WORKS", TextStyle.Label.WithSize(15).WithTracking(0.42f).WithColor(Palette.Text));
            title.rectTransform.At(VoidFrame.Width * 0.5f, 150f, 400f, 22f);
            UIKit.Rule(page, 200f, 1720f, 190f, Palette.Rule);
            var ideaHead = UIKit.Text(page, "HOW TO PLAY", TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            ideaHead.rectTransform.At(220f + 200f, 226f, 400f, 18f);
            var keysHead = UIKit.Text(page, "CONTROLS", TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(Palette.Faint), TextAnchor.MiddleRight);
            keysHead.rectTransform.At(1230f - 200f, 226f, 400f, 18f);

            var ideas = Wiring ? new List<string>
            {
                "Match the outputs on the right using the inputs on the left.",
                "Blocks in the middle change signals using hidden rules.",
                "Test blocks with different inputs to figure out each block's rule.",
                "Record findings with the pen or text tool, or double-click a block to attach a note.",
                "Block letters determine output type: B = Bit, D = Digit, W = Wave.",
                "Solve a circuit to reveal its block rules.",
                "Click and drag to move blocks around.",
                "A solid outline must be used. A dotted input or block may be left out.",
                "Speaker blocks are only heard: click to listen.",
            } : new List<string>
            {
                "The goal is written above the circuit. Press RUN to send the signals through: only then do the outputs show, and each target gets a tick or a cross.",
                "Each block keeps one hidden rule that never changes. Work it out from what comes out.",
                "Inputs with arrows can be changed; the rest are fixed. Sometimes an input has to end at a target of its own.",
                "Wires hide their signals. Click a wire to put a probe on it: it reads what the wire carries on every run. Each circuit has only so many.",
                "Every signal has a sound. A run plays each output as it arrives, and clicking any value you can see plays it again.",
                "Draw with the pen, or type with the text tool, to label blocks and note down what you learn. Notes stay with the circuit.",
            };
            // Each idea is as tall as its wrapped lines, so long ones never run into the next.
            const float ideaWidth = 680f;
            const int ideaSize = 21;
            const float lineHeight = 33f;
            float y = 252f;
            foreach (var idea in ideas)
            {
                var t = UIKit.Text(page, idea, TextStyle.Body.WithSize(ideaSize), TextAnchor.UpperLeft);
                int lines = Mathf.Max(1, Mathf.CeilToInt(t.MeasureWidth() / (ideaWidth * 0.92f)));
                float h = lines * lineHeight;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.At(250f + ideaWidth * 0.5f, y + h * 0.5f, ideaWidth, h + 8f);
                var dot = UIKit.Shape(page, "Dot", ShapeKind.Circle);
                dot.rectTransform.At(226f, y + 15f, 6f, 6f);
                dot.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                y += h + 26f;
            }

            string[] keys = Wiring ? new[]
            {
                "SPACE", "run the circuit",
                "DRAG DOT TO DOT", "add a wire",
                "CLICK A WIRE", "remove it",
                "DRAG A BOX", "select wires, then delete",
                "RIGHT-DRAG A BOX", "remove every wire in it",
                "DRAG A BLOCK", "move it",
                "DOUBLE-CLICK A BLOCK", "attach a note",
                "CLICK A SIGNAL", "hear it",
                "D  ·  E  ·  T", "pen  ·  eraser  ·  text",
                "Z", "undo",
                "ESC", "menu",
            } : new[]
            {
                "SPACE", "run the circuit",
                "CLICK  ·  SCROLL", "change a block or input",
                "RIGHT-CLICK", "change it the other way",
                "CLICK A WIRE", "place a probe",
                "CLICK A VALUE", "hear it",
                "CLICK A PROBE'S DOT", "take it off",
                "D  ·  E  ·  T", "pen  ·  eraser  ·  text",
                "RIGHT-DRAG", "rub out a note",
                "Z  ·  BACKSPACE", "undo",
                "TAB  ·  LEFT RIGHT", "choose something, change it",
                "ESC", "menu",
            };
            float ky = 272f;
            for (int i = 0; i + 1 < keys.Length; i += 2)
            {
                var k = UIKit.Text(page, keys[i], TextStyle.Label.WithSize(13).WithTracking(0.16f).WithColor(Palette.Dim), TextAnchor.MiddleRight);
                k.rectTransform.At(1230f - 120f, ky, 240f, 22f);
                var a = UIKit.Text(page, keys[i + 1], TextStyle.Body.WithSize(19), TextAnchor.MiddleLeft);
                a.rectTransform.At(1260f + 160f, ky, 320f, 26f);
                ky += 52f;
            }

        }

        public override void OnClosing()
        {
            App.Audio?.StopSignals();
            base.OnClosing();
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape) || GameInput.Down(Key.H))
            {
                App.Sound(Sfx.Close);
                Close();
                return;
            }
            if (GameInput.Down(Key.Right) || (GameInput.Down(Key.Tab) && !GameInput.Held(Key.Shift))) Step(1);
            else if (GameInput.Down(Key.Left) || (GameInput.Down(Key.Tab) && GameInput.Held(Key.Shift))) Step(-1);
        }
    }

    /// <summary>The title card shown the first time a chapter is entered.</summary>
    public sealed class ChapterIntroOverlay : Overlay
    {
        public ChapterDef Chapter;
        float _shownAt;
        float _samplesY = 796f;
        VoidText _prompt;

        protected override float VeilAlpha => 1f;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            // A chapter with a signal guide leaves its signals to the guide, which
            // comes next, so its card is just the title, lower on the screen.
            bool guide = SignalGuideOverlay.Has(Chapter);
            float shift = guide ? 70f : 0f;
            var label = UIKit.Text(Root, "CHAPTER", TextStyle.Label.WithSize(13).WithTracking(0.5f).WithColor(Palette.Faint));
            label.rectTransform.At(cx, 290f + shift, 400f, 20f);
            var numeral = UIKit.Text(Root, Chapter.Numeral, TextStyle.Display.WithSize(120).WithTracking(0.2f).WithColor(Color.white));
            numeral.SignalFill = true;
            numeral.rectTransform.At(cx, 380f + shift, 600f, 150f);
            var title = UIKit.Text(Root, Chapter.Title, TextStyle.Heading.WithSize(46).WithWeight(200));
            title.rectTransform.At(cx, 490f + shift, 1200f, 64f);
            var tagline = UIKit.Text(Root, Chapter.Tagline, TextStyle.Body.WithSize(23));
            tagline.rectTransform.At(cx, 552f + shift, 1200f, 34f);
            // The note on the chapter's signals takes as many lines as it needs; the rest sits below it.
            float y = 600f + shift;
            if (!guide && !string.IsNullOrEmpty(Chapter.Signals))
            {
                const float width = 1100f;
                var signals = UIKit.Text(Root, Chapter.Signals, TextStyle.Body.WithSize(18).WithColor(Palette.Dim));
                int lines = Mathf.Max(1, Mathf.CeilToInt(signals.MeasureWidth() / (width * 0.9f)));
                float h = lines * 18f * signals.LineHeight;
                signals.horizontalOverflow = HorizontalWrapMode.Wrap;
                signals.rectTransform.At(cx, y + h * 0.5f, width, h + 6f);
                y += h;
            }
            float lineY = y + 30f;
            _samplesY = Mathf.Max(796f, lineY + 76f);

            var line = UIKit.Add<PathGraphic>(Root, "Line");
            line.raycastTarget = false;
            line.rectTransform.At(cx, lineY, 480f, 10f);
            line.BaseColor = Palette.Rule;
            line.BaseWidth = 1f;
            line.LitWidth = 1.6f;
            line.Glow = 0.4f;
            line.GlowRadius = 10f;
            line.SetPoints(new List<Vector2> { new Vector2(-240f, 0f), new Vector2(240f, 0f) });
            line.SetLit(0f, 0.0001f);
            Tweener.Run(line, "draw", 1.6f, t => line.SetLit(0f, Mathf.Max(0.0001f, t)), Ease.InOutCubic, 0.35f);

            if (!guide) BuildSamples(cx);

            _prompt = UIKit.Text(Root, guide ? "CLICK OR PRESS ENTER TO CONTINUE" : "CLICK OR PRESS ENTER TO BEGIN", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _prompt.rectTransform.At(cx, guide ? 960f : Mathf.Max(960f, _samplesY + 120f), 700f, 18f);
            _prompt.canvasRenderer.SetAlpha(0f);
            Tweener.Run(_prompt, "show", 0.6f, t => _prompt.canvasRenderer.SetAlpha(t), Ease.OutQuad, 1.4f);

            var c = Veil.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = TryDismiss;

            _shownAt = Time.unscaledTime;
            App.Sound(Sfx.Unlock, 0.75f, 0.8f);
        }

        /// <summary>A few of the chapter's signals, played in turn so the player hears what they sound like.</summary>
        void BuildSamples(float cx)
        {
            if (Chapter.Mixed)
            {
                BuildMixedSamples(cx);
                return;
            }
            var m = Chapter.Medium;
            int[] values;
            switch (m)
            {
                case Medium.Binary: values = new[] { 1, 0, 1, 1 }; break;
                case Medium.Decimal: values = new[] { 1, 3, 5 }; break;
                case Medium.Shape: values = new[] { Shapes.Make(0, true), Shapes.Make(1, false, 1), Shapes.Make(2, true, 2), Shapes.Make(3, false, 3) }; break;
                case Medium.Wave: values = new[] { Waves.Make(Waves.Sine, 0, 6), Waves.Make(Waves.SquareWave, 2, 3), Waves.Make(Waves.Saw, 4, 5), Waves.Make(Waves.Sine, 3, 6, 1) }; break;
                default: values = new[] { Sampled.Make(Waves.Sine, 0, 6, 2), Sampled.Make(Waves.Sine, 2, 4, 1), Sampled.Make(Waves.Saw, 3, 6, 2), Sampled.Make(Waves.Sine, 4, 2, 0) }; break;
            }
            float size = Media.IsWave(m) ? 38f : 40f;
            float pitch = ValueView.WidthFor(m, size) + 56f;
            var row = UIKit.Rect(Root, "Samples").At(cx, _samplesY, 0f, 0f);
            var views = new List<ValueView>();
            for (int i = 0; i < values.Length; i++)
            {
                var v = ValueView.Create(row, m, size, Media.IsWave(m));
                v.GetComponent<RectTransform>().Centered((i - (values.Length - 1) * 0.5f) * pitch, 0f, ValueView.WidthFor(m, size), size);
                v.Set(values[i], ValueLook.Live);
                v.MakePlayable(value => App.Audio?.PlaySignal(m, value));
                views.Add(v);
            }
            var hint = UIKit.Text(Root, "CLICK ANY SIGNAL TO HEAR IT", TextStyle.Label.WithSize(11).WithTracking(0.34f).WithColor(Palette.Faint));
            hint.rectTransform.At(cx, _samplesY + 66f, 600f, 16f);

            var group = row.Group();
            group.alpha = 0f;
            Tweener.Run(row, "show", 0.6f, t => group.alpha = t, Ease.OutQuad, 1.2f);
            Tweener.After(this, "samples", 1.9f, () =>
            {
                if (App.Audio != null) App.Audio.PlaySignals(m, values, i => { if (views[i] != null) views[i].Ping(); });
            });
        }

        /// <summary>One signal of every type, for chapters where they all meet.</summary>
        void BuildMixedSamples(float cx)
        {
            var samples = new (Medium m, int v)[]
            {
                (Medium.Binary, 1),
                (Medium.Decimal, 5),
                (Medium.Wave, Waves.Make(Waves.Sine, 1, 6)),
            };
            const float size = 38f;
            float total = 0f;
            foreach (var (m, _) in samples) total += ValueView.WidthFor(m, size) + 56f;
            float x = -total * 0.5f;
            var row = UIKit.Rect(Root, "Samples").At(cx, _samplesY, 0f, 0f);
            var views = new List<ValueView>();
            foreach (var (m, v) in samples)
            {
                float w = ValueView.WidthFor(m, size);
                var view = ValueView.Create(row, m, size, Media.IsWave(m));
                view.GetComponent<RectTransform>().Centered(x + (w + 56f) * 0.5f, 0f, w, size);
                view.Set(v, ValueLook.Live);
                var medium = m;
                view.MakePlayable(value => App.Audio?.PlaySignal(medium, value));
                views.Add(view);
                x += w + 56f;
            }
            var hint = UIKit.Text(Root, "CLICK ANY SIGNAL TO HEAR IT", TextStyle.Label.WithSize(11).WithTracking(0.34f).WithColor(Palette.Faint));
            hint.rectTransform.At(cx, _samplesY + 66f, 600f, 16f);
            var group = row.Group();
            group.alpha = 0f;
            Tweener.Run(row, "show", 0.6f, t => group.alpha = t, Ease.OutQuad, 1.2f);
        }

        public override void OnClosing()
        {
            Tweener.Kill(this);
            App.Audio?.StopSignals();
            base.OnClosing();
        }

        void TryDismiss()
        {
            if (Time.unscaledTime - _shownAt < 0.8f) return;
            App.Sound(Sfx.Close);
            Close();
            // Then the chapter's signals, before its first circuit.
            if (SignalGuideOverlay.Has(Chapter))
            {
                var guide = Game.Screens.Create<SignalGuideOverlay>();
                guide.Chapter = Chapter;
                Game.Screens.Push(guide);
            }
        }

        public override void HandleInput()
        {
            if (GameInput.Confirm || GameInput.Down(Key.Escape)) TryDismiss();
        }
    }
}
