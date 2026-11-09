using System;
using System.Collections.Generic;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>Esc during a circuit.</summary>
    public sealed class PauseOverlay : Overlay
    {
        public PuzzleScreen Owner;
        MenuList _menu;

        protected override float VeilAlpha => 0.88f;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var label = UIKit.Text(Root, "PAUSED", TextStyle.Label.WithSize(14).WithTracking(0.42f).WithColor(Palette.Dim));
            label.rectTransform.At(cx, 300f, 400f, 20f);
            var p = Owner.Puzzle;
            var name = UIKit.Text(Root, p.Name, TextStyle.Heading.WithSize(40).WithWeight(200));
            name.rectTransform.At(cx, 350f, 900f, 56f);
            var where = UIKit.Text(Root, $"CHAPTER {p.Chapter.Numeral}  ·  CIRCUIT {p.Label}", TextStyle.Label.WithSize(12).WithColor(Palette.Faint));
            where.rectTransform.At(cx, 396f, 600f, 18f);

            _menu = new MenuList();
            float y = 490f;
            const float step = 58f;
            _menu.Add(MenuItem.Create(Root, "RESUME", cx, y), () => { App.Sound(Sfx.Close); Close(); });
            _menu.Add(MenuItem.Create(Root, "RESET CIRCUIT", cx, y += step), () =>
                Game.Confirm("Put every box back where it started?", "RESET", false, () =>
                {
                    Close();
                    Owner.ResetCircuit();
                }));
            _menu.Add(MenuItem.Create(Root, "CIRCUITS", cx, y += step), () => Game.GoChapters(Owner.Puzzle.Chapter));
            _menu.Add(MenuItem.Create(Root, "SETTINGS", cx, y += step), Game.OpenSettings);
            _menu.Add(MenuItem.Create(Root, "TITLE", cx, y += step), Game.GoTitle);

            Chrome.KeyHints(Root, cx - 190f, 1000f, false, "ARROWS", "MOVE", "ENTER", "CHOOSE", "ESC", "RESUME");
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape))
            {
                App.Sound(Sfx.Close);
                Close();
                return;
            }
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
        }

        readonly List<Row> _rows = new List<Row>();
        int _focus;
        float _pulse;

        const float LabelLeft = 680f;
        const float ControlRight = 1240f;

        protected internal override void Build()
        {
            BuildVeil();
            float cx = VoidFrame.Width * 0.5f;
            var title = UIKit.Text(Root, "SETTINGS", TextStyle.Label.WithSize(15).WithTracking(0.42f).WithColor(Palette.Text));
            title.rectTransform.At(cx, 220f, 400f, 22f);
            UIKit.Rule(Root, LabelLeft - 40f, ControlRight + 80f, 262f, Palette.Rule);

            var prefs = Game.Save.Prefs;
            float y = 320f;
            const float step = 62f;
            AddSlider("MASTER VOLUME", y, prefs.MasterVolume, v => { prefs.MasterVolume = v; Game.ApplyPrefs(); });
            AddSlider("MUSIC", y += step, prefs.MusicVolume, v => { prefs.MusicVolume = v; Game.ApplyPrefs(); });
            AddSlider("EFFECTS", y += step, prefs.EffectsVolume, v => { prefs.EffectsVolume = v; Game.ApplyPrefs(); });
            y += 22f;
            AddToggle("FULLSCREEN", y += step, prefs.Fullscreen, v => { prefs.Fullscreen = v; Game.ApplyPrefs(); });
            AddToggle("REDUCE MOTION", y += step, prefs.ReduceMotion, v => { prefs.ReduceMotion = v; Game.ApplyPrefs(); });
            AddToggle("FAST RUNS", y += step, prefs.FastRuns, v => { prefs.FastRuns = v; Game.ApplyPrefs(); });
            var note = UIKit.Text(Root, "SKIPS THE RUN ANIMATION", TextStyle.Label.WithSize(11).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            note.rectTransform.At(LabelLeft + 200f, y + 24f, 400f, 16f);
            y += 40f;
            UIKit.Rule(Root, LabelLeft - 40f, ControlRight + 80f, y + 30f, Palette.Rule);
            AddAction("ERASE PROGRESS", y += step + 10f, true, () =>
                Game.Confirm("Erase every solved circuit and every name you've given? Settings are kept.", "ERASE", true, () =>
                {
                    Game.EraseProgress();
                }));
            AddAction("BACK", y += step, false, () => { App.Sound(Sfx.Close); Close(); });

            Chrome.KeyHints(Root, cx - 300f, 1000f, false, "ARROWS", "MOVE", "LEFT RIGHT", "ADJUST", "ENTER", "SWITCH", "ESC", "BACK");
            Focus(0, false);
        }

        Row AddRow(string text, float y)
        {
            var row = new Row();
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

        void AddSlider(string text, float y, float value, Action<float> changed)
        {
            var row = AddRow(text, y);
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

        void AddToggle(string text, float y, bool on, Action<bool> changed)
        {
            var row = AddRow(text, y);
            row.Toggle = VoidToggle.Create(Root, ControlRight - 29f, y);
            row.Toggle.On = on;
            row.Toggle.Changed = changed;
            var r = row;
            row.Activate = () => { App.Sound(Sfx.Dial); r.Toggle.Flip(); };
        }

        void AddAction(string text, float y, bool danger, Action action)
        {
            var row = AddRow(text, y);
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

            Chrome.KeyHints(Root, cx - 190f, 1000f, false, "LEFT RIGHT", "CHOOSE", "ENTER", "OK", "ESC", "CANCEL");
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
        public bool Trace;

        protected internal override void Build()
        {
            BuildVeil();
            var title = UIKit.Text(Root, "HOW IT WORKS", TextStyle.Label.WithSize(15).WithTracking(0.42f).WithColor(Palette.Text));
            title.rectTransform.At(VoidFrame.Width * 0.5f, 170f, 400f, 22f);

            string[] ideas =
            {
                "Every box hides a rule. Its dots are its settings: click the box, or a dot, to change it.",
                "Run tests the whole circuit. A row that gives what's wanted lights up.",
                "Probe a box to see what it did with each input it was given.",
                "Name a behaviour once you've worked it out. The name follows it into every circuit.",
                Trace
                    ? "Some boxes remember. Time runs left to right along the bottom: click any moment to see the circuit then."
                    : "A dashed box is locked. You can't change it, but you can probe it.",
            };
            float y = 270f;
            foreach (var idea in ideas)
            {
                var t = UIKit.Text(Root, idea, TextStyle.Body.WithSize(21), TextAnchor.UpperLeft);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.At(250f + 330f, y + 40f, 660f, 80f);
                var dot = UIKit.Shape(Root, "Dot", ShapeKind.Circle);
                dot.rectTransform.At(226f, y + 15f, 6f, 6f);
                dot.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                y += 96f;
            }

            string[] keys =
            {
                "CLICK", "next setting",
                "SCROLL", "turn a box either way",
                "RIGHT-CLICK  ·  N", "name a behaviour",
                "SPACE  ·  ENTER", "run",
                "P  ·  HOLD SHIFT", "probe",
                "Z  ·  BACKSPACE", "undo",
                "TAB  ·  R", "choose a box, turn it",
                Trace ? "LEFT  ·  RIGHT" : "UP  ·  DOWN", Trace ? "move through time" : "choose a row",
                "ESC", "menu",
            };
            float ky = 280f;
            for (int i = 0; i + 1 < keys.Length; i += 2)
            {
                var k = UIKit.Text(Root, keys[i], TextStyle.Label.WithSize(13).WithTracking(0.16f).WithColor(Palette.Dim), TextAnchor.MiddleRight);
                k.rectTransform.At(1220f - 120f, ky, 240f, 22f);
                var a = UIKit.Text(Root, keys[i + 1], TextStyle.Body.WithSize(19), TextAnchor.MiddleLeft);
                a.rectTransform.At(1250f + 160f, ky, 320f, 26f);
                ky += 50f;
            }

            Chrome.KeyHints(Root, VoidFrame.Width * 0.5f - 60f, 1000f, false, "ESC", "CLOSE");
            var close = UIKit.Hit(Root, "Close");
            close.rectTransform.Stretch();
            close.transform.SetSiblingIndex(1);
            var c = close.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Close;
            c.Clicked = Close;
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

            var line = UIKit.Add<PathGraphic>(Root, "Line");
            line.raycastTarget = false;
            line.rectTransform.At(cx, 660f, 480f, 10f);
            line.BaseColor = Palette.Rule;
            line.BaseWidth = 1f;
            line.LitWidth = 1.6f;
            line.Glow = 0.4f;
            line.GlowRadius = 10f;
            line.SetPoints(new List<Vector2> { new Vector2(-240f, 0f), new Vector2(240f, 0f) });
            line.SetLit(0f, 0.0001f);
            Tweener.Run(line, "draw", 1.6f, t => line.SetLit(0f, Mathf.Max(0.0001f, t)), Ease.InOutCubic, 0.35f);

            _prompt = UIKit.Text(Root, "PRESS ANY KEY", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _prompt.rectTransform.At(cx, 960f, 400f, 18f);
            _prompt.canvasRenderer.SetAlpha(0f);
            Tweener.Run(_prompt, "show", 0.6f, t => _prompt.canvasRenderer.SetAlpha(t), Ease.OutQuad, 1.4f);

            var c = Veil.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = TryDismiss;

            _shownAt = Time.unscaledTime;
            App.Sound(Sfx.Unlock, 0.75f, 0.8f);
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
