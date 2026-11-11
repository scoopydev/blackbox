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
            var where = UIKit.Text(Root, $"CHAPTER {p.Chapter.Numeral}  ·  {p.Chapter.Title}  ·  CIRCUIT {p.Label}", TextStyle.Label.WithSize(12).WithColor(Palette.Faint));
            where.rectTransform.At(cx, 396f, 600f, 18f);

            int solvedHere = Game.Progress.SolvedIn(p.Chapter);
            Chrome.ProgressBar(Root, cx, 436f, 180f, solvedHere, p.Chapter.Puzzles.Count, "SOLVED IN THIS CHAPTER");

            _menu = new MenuList();
            float y = 520f;
            const float step = 58f;
            _menu.Add(MenuItem.Create(Root, "RESUME", cx, y), () => { App.Sound(Sfx.Close); Close(); });
            _menu.Add(MenuItem.Create(Root, "HOW IT WORKS", cx, y += step), OpenHelp);
            _menu.Add(MenuItem.Create(Root, "RESET CIRCUIT", cx, y += step), () =>
                Game.Confirm(ResetQuestion, "RESET", false, () =>
                {
                    Close();
                    Reset?.Invoke();
                }));
            _menu.Add(MenuItem.Create(Root, "SETTINGS", cx, y += step), Game.OpenSettings);
            _menu.Add(MenuItem.Create(Root, "CHAPTERS", cx, y += step + 16f), () => Game.GoChapters(Puzzle.Chapter));
            _menu.Add(MenuItem.Create(Root, "MAIN MENU", cx, y += step), Game.GoTitle);

            Chrome.KeyHintsCentred(Root, cx, 1000f, "UP DOWN", "MOVE", "ENTER", "CHOOSE", "H", "HELP", "ESC", "RESUME");
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
            const float step = 56f;
            const float gap = 30f;
            float y = 236f;
            Chrome.Section(Root, "SOUND", SectionRight, y);
            AddSlider("MASTER VOLUME", y, prefs.MasterVolume, v => { prefs.MasterVolume = v; Game.ApplyPrefs(); }, "Everything the game plays.");
            AddSlider("MUSIC", y += step, prefs.MusicVolume, v => { prefs.MusicVolume = v; Game.ApplyPrefs(); }, "The hum under every screen.");
            AddSlider("EFFECTS", y += step, prefs.EffectsVolume, v => { prefs.EffectsVolume = v; Game.ApplyPrefs(); }, "Clicks, wires and runs.");
            AddToggle("SIGNAL SOUNDS", y += step, prefs.SignalSounds, v => { prefs.SignalSounds = v; Game.MarkDirty(); }, "Plays each signal as you set inputs and run a circuit.");

            Chrome.Section(Root, "DISPLAY", SectionRight, y += step + gap);
            AddToggle("FULLSCREEN", y, prefs.Fullscreen, v => { prefs.Fullscreen = v; Game.ApplyPrefs(); }, "Fill the screen, or play in a window.");
            AddToggle("REDUCE MOTION", y += step, prefs.ReduceMotion, v => { prefs.ReduceMotion = v; Game.ApplyPrefs(); }, "Stills the flowing light and the pulsing marks.");

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
            var hitRt = UIKit.Rect(Root, "Row " + text).At((LabelLeft - 40f + ControlRight + 80f) * 0.5f, y, ControlRight + 120f - LabelLeft, 52f);
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

        protected override float VeilAlpha => 0.94f;

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

    /// <summary>How to play, and every control.</summary>
    public sealed class HelpOverlay : Overlay
    {
        /// <summary>The chapter being played, for a line about its signals. Optional.</summary>
        public ChapterDef Chapter;
        /// <summary>Help for a circuit the player wires, rather than one they set.</summary>
        public bool Wiring;

        protected internal override void Build()
        {
            BuildVeil();
            var title = UIKit.Text(Root, "HOW IT WORKS", TextStyle.Label.WithSize(15).WithTracking(0.42f).WithColor(Palette.Text));
            title.rectTransform.At(VoidFrame.Width * 0.5f, 150f, 400f, 22f);
            UIKit.Rule(Root, 200f, 1720f, 190f, Palette.Rule);
            var ideaHead = UIKit.Text(Root, "THE IDEA", TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            ideaHead.rectTransform.At(220f + 200f, 226f, 400f, 18f);
            var keysHead = UIKit.Text(Root, "CONTROLS", TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(Palette.Faint), TextAnchor.MiddleRight);
            keysHead.rectTransform.At(1230f - 200f, 226f, 400f, 18f);

            var ideas = Wiring ? new List<string>
            {
                "Wire the circuit so every output does what its target shows. Drag from a dot to a dot to run a wire; click a wire to take it off.",
                "Every block must be used, and every input of a block needs a wire. A block with an empty input sends nothing.",
                "Each block does one simple thing, the same every time you meet that kind of block, but it never says what. Their numbers, K1, K2 and so on, only tell them apart: K1 is a different block in every circuit. Wire a block between an input and an output, run it, and watch what it sends. Once you solve a circuit, each of its blocks has its rule written under it.",
                "A block reads any signal as its own type: a bit block counts a digit as 1 unless it's 0, and a wave as 1 while it's above the middle.",
                "Each output is a display: it shows the signal that reaches it, and plays it on a run. Its target is met only by the very same signal.",
                "Signals can change through the loop. Each one plays across its card, and clicking any signal plays it.",
            } : new List<string>
            {
                "The goal is written above the circuit. Press RUN to send the signals through: only then do the outputs show, and each target gets a tick or a cross.",
                "Each block keeps one hidden rule that never changes. Work it out from what comes out.",
                "Inputs with arrows can be changed; the rest are fixed. Sometimes an input has to end at a target of its own.",
                "Wires hide their signals. Click a wire to put a probe on it: it reads what the wire carries on every run. Each circuit has only so many.",
                "Every signal has a sound. A run plays each output as it arrives, and clicking any value you can see plays it again.",
                "Draw with the pen, or type with the text tool, to label blocks and note down what you learn. Notes stay with the circuit.",
            };
            if (Chapter != null && !string.IsNullOrEmpty(Chapter.Signals)) ideas.Add(Chapter.Signals);
            float y = 252f;
            foreach (var idea in ideas)
            {
                var t = UIKit.Text(Root, idea, TextStyle.Body.WithSize(21), TextAnchor.UpperLeft);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.At(250f + 340f, y + 40f, 680f, 80f);
                var dot = UIKit.Shape(Root, "Dot", ShapeKind.Circle);
                dot.rectTransform.At(226f, y + 15f, 6f, 6f);
                dot.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                y += 92f;
            }

            string[] keys = Wiring ? new[]
            {
                "SPACE", "run the circuit",
                "DRAG DOT TO DOT", "run a wire",
                "CLICK A DOT, THEN ANOTHER", "run a wire",
                "DRAG A WIRE'S END", "move it, or drop it to take it off",
                "CLICK A WIRE", "take it off",
                "RIGHT-CLICK A DOT", "take its wires off",
                "DRAG A BLOCK", "move it",
                "CLICK A SIGNAL", "hear it",
                "D  ·  E  ·  T", "pen  ·  eraser  ·  text",
                "RIGHT-DRAG", "rub out a note",
                "Z  ·  BACKSPACE", "undo",
                "ESC", "put a wire down, or menu",
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
            float ky = 270f;
            for (int i = 0; i + 1 < keys.Length; i += 2)
            {
                var k = UIKit.Text(Root, keys[i], TextStyle.Label.WithSize(13).WithTracking(0.16f).WithColor(Palette.Dim), TextAnchor.MiddleRight);
                k.rectTransform.At(1230f - 120f, ky, 240f, 22f);
                var a = UIKit.Text(Root, keys[i + 1], TextStyle.Body.WithSize(19), TextAnchor.MiddleLeft);
                a.rectTransform.At(1260f + 160f, ky, 320f, 26f);
                ky += 52f;
            }

            var close = UIKit.Hit(Root, "Close");
            close.rectTransform.Stretch();
            close.transform.SetSiblingIndex(1);
            var c = close.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Close;
            c.Clicked = Close;
            Chrome.KeyButton(Root, "ESC", "CLOSE", 1830f, 62f, true, Close);
            Chrome.KeyHintsCentred(Root, VoidFrame.Width * 0.5f, 1032f, "ESC", "CLOSE", "CLICK", "ANYWHERE TO CLOSE");
            App.Sound(Sfx.Open, 1.2f, 0.6f);
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape) || GameInput.Down(Key.H) || GameInput.Confirm)
            {
                App.Sound(Sfx.Close);
                Close();
            }
        }
    }

    /// <summary>The title card shown the first time a chapter is entered.</summary>
    public sealed class ChapterIntroOverlay : Overlay
    {
        public ChapterDef Chapter;
        float _shownAt;
        VoidText _prompt;

        protected override float VeilAlpha => 1f;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var label = UIKit.Text(Root, "CHAPTER", TextStyle.Label.WithSize(13).WithTracking(0.5f).WithColor(Palette.Faint));
            label.rectTransform.At(cx, 330f, 400f, 20f);
            var numeral = UIKit.Text(Root, Chapter.Numeral, TextStyle.Display.WithSize(120).WithTracking(0.2f).WithColor(Color.white));
            numeral.SignalFill = true;
            numeral.rectTransform.At(cx, 420f, 600f, 150f);
            var title = UIKit.Text(Root, Chapter.Title, TextStyle.Heading.WithSize(46).WithWeight(200));
            title.rectTransform.At(cx, 530f, 1000f, 64f);
            var tagline = UIKit.Text(Root, Chapter.Tagline, TextStyle.Body.WithSize(23));
            tagline.rectTransform.At(cx, 592f, 1000f, 34f);
            if (!string.IsNullOrEmpty(Chapter.Signals))
            {
                var signals = UIKit.Text(Root, Chapter.Signals, TextStyle.Body.WithSize(19).WithColor(Palette.Dim));
                signals.horizontalOverflow = HorizontalWrapMode.Wrap;
                signals.rectTransform.At(cx, 654f, 900f, 56f);
            }

            var line = UIKit.Add<PathGraphic>(Root, "Line");
            line.raycastTarget = false;
            line.rectTransform.At(cx, 716f, 480f, 10f);
            line.BaseColor = Palette.Rule;
            line.BaseWidth = 1f;
            line.LitWidth = 1.6f;
            line.Glow = 0.4f;
            line.GlowRadius = 10f;
            line.SetPoints(new List<Vector2> { new Vector2(-240f, 0f), new Vector2(240f, 0f) });
            line.SetLit(0f, 0.0001f);
            Tweener.Run(line, "draw", 1.6f, t => line.SetLit(0f, Mathf.Max(0.0001f, t)), Ease.InOutCubic, 0.35f);

            BuildSamples(cx);

            _prompt = UIKit.Text(Root, "CLICK OR PRESS ENTER TO BEGIN", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _prompt.rectTransform.At(cx, 960f, 700f, 18f);
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
            var row = UIKit.Rect(Root, "Samples").At(cx, 796f, 0f, 0f);
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
            hint.rectTransform.At(cx, 862f, 600f, 16f);

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
                (Medium.Shape, Shapes.Make(Shapes.Triangle, true)),
                (Medium.Wave, Waves.Make(Waves.Sine, 1, 6)),
            };
            const float size = 38f;
            float total = 0f;
            foreach (var (m, _) in samples) total += ValueView.WidthFor(m, size) + 56f;
            float x = -total * 0.5f;
            var row = UIKit.Rect(Root, "Samples").At(cx, 796f, 0f, 0f);
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
            hint.rectTransform.At(cx, 862f, 600f, 16f);
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
        }

        public override void HandleInput()
        {
            if (GameInput.Confirm || GameInput.Down(Key.Escape)) TryDismiss();
        }
    }
}
