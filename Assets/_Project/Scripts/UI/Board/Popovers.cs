using System;
using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>Places a panel beside a box on the board without leaving the board area.</summary>
    static class Popover
    {
        // The board area in centred frame coordinates (y up), with a little room below for the hint line.
        const float Left = -VoidFrame.Width * 0.5f + 90f;
        const float Right = VoidFrame.Width * 0.5f - 90f;
        const float Top = VoidFrame.Height * 0.5f - 132f;
        const float Bottom = VoidFrame.Height * 0.5f - 790f;

        public static void Place(RectTransform panel, BoxView box, float w, float h)
        {
            float gap = 26f;
            float x = box.Centre.x + box.Width * 0.5f + gap + w * 0.5f;
            if (x + w * 0.5f > Right) x = box.Centre.x - box.Width * 0.5f - gap - w * 0.5f;
            x = Mathf.Clamp(x, Left + w * 0.5f, Right - w * 0.5f);
            float y = Mathf.Clamp(box.Centre.y, Bottom + h * 0.5f, Top - h * 0.5f);
            panel.Centered(x, y, w, h);
        }

        public static ShapeGraphic Backing(RectTransform panel)
        {
            var bg = UIKit.Shape(panel, "Backing");
            bg.rectTransform.Stretch();
            bg.Radius = 16f;
            bg.Style(Paint.Solid, Color.black, Paint.Solid, Palette.Outline, 1.2f);
            bg.raycastTarget = true;
            return bg;
        }
    }

    /// <summary>
    /// What the probe saw: for a Chapter I box, every input it could get and what
    /// it gave back for each one observed so far; for a Chapter II box, its inputs
    /// and output over the last run.
    /// </summary>
    public sealed class ProbeCard : MonoBehaviour
    {
        RectTransform _panel;
        RectTransform _content;
        CanvasGroup _group;
        BoxView _box;
        bool _visible;

        const float Width = 268f;

        public static ProbeCard Create(Transform layer)
        {
            var rt = UIKit.Rect(layer, "Probe").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            var card = rt.gameObject.AddComponent<ProbeCard>();
            card._panel = UIKit.Rect(rt, "Panel");
            card._group = card._panel.Group();
            card._group.alpha = 0f;
            card._group.blocksRaycasts = false;
            card._group.interactable = false;
            return card;
        }

        public bool Visible => _visible;
        public BoxView Box => _box;

        void Begin(BoxView box, float height)
        {
            if (_content != null) Destroy(_content.gameObject);
            _box = box;
            Popover.Place(_panel, box, Width, height);
            if (_panel.Find("Backing") == null) Popover.Backing(_panel).raycastTarget = false;
            _content = UIKit.Rect(_panel, "Content").Stretch();
            if (!_visible)
            {
                _visible = true;
                Tweener.Run(this, "fade", 0.14f, t => _group.alpha = t, Ease.OutQuad);
            }
        }

        void Header(BoxDef def, int setting, RuleName name)
        {
            var code = UIKit.Text(_content, "PROBE  ·  " + def.Kind.Id, TextStyle.Label.WithSize(13).WithColor(Palette.Dim), TextAnchor.MiddleLeft);
            code.rectTransform.At(20f + 80f, 26f, 160f, 18f);
            string settingText = def.Kind.SettingCount > 1 ? $"{setting + 1} / {def.Kind.SettingCount}" : "FIXED";
            if (def.Locked) settingText = "LOCKED · " + settingText;
            var s = UIKit.Text(_content, settingText, TextStyle.Label.WithSize(13).WithTracking(0.16f).WithColor(Palette.Faint), TextAnchor.MiddleRight);
            s.rectTransform.At(Width - 20f - 80f, 26f, 160f, 18f);

            // The player's name for this behaviour, or an invitation to give one.
            if (!string.IsNullOrEmpty(name.Glyph))
            {
                var g = UIKit.Glyph(_content, name.Glyph, Palette.Text, 1.8f);
                g.rectTransform.At(32f, 58f, 22f, 22f);
                Small("NAMED", 56f, 58f, Palette.Dim);
            }
            else if (!string.IsNullOrEmpty(name.Text))
            {
                var t = UIKit.Text(_content, name.Text, TextStyle.Label.WithSize(17).WithTracking(0.1f).WithColor(Palette.Text), TextAnchor.MiddleLeft);
                t.rectTransform.At(20f + 60f, 58f, 120f, 22f);
            }
            else Small("UNNAMED  ·  N OR RIGHT-CLICK TO NAME", 20f, 58f, Palette.Faint);

            UIKit.Rule(_content, 20f, Width - 20f, 80f, Palette.Rule);
        }

        VoidText Small(string text, float left, float y, Color color, int size = 12)
        {
            var t = UIKit.Text(_content, text, TextStyle.Label.WithSize(size).WithTracking(0.18f).WithColor(color), TextAnchor.MiddleLeft);
            t.rectTransform.At(left + 120f, y, 240f, 18f);
            return t;
        }

        /// <summary>
        /// A logic box: one row per input combination. <paramref name="seen"/> maps a
        /// combination (port 0 as the high bit) to what the box gave for it.
        /// </summary>
        public void ShowTable(BoxView box, int setting, RuleName name, IReadOnlyDictionary<int, bool> seen, int current)
        {
            var def = box.Def;
            int n = def.Kind.InputCount;
            int combos = 1 << n;
            const float row = 24f;
            float height = 96f + combos * row + 40f;
            Begin(box, height);
            Header(def, setting, name);

            float digitsX = 48f;
            for (int c = 0; c < combos; c++)
            {
                float y = 104f + c * row;
                bool known = seen != null && seen.ContainsKey(c);
                bool isCurrent = c == current;
                if (isCurrent)
                {
                    var hl = UIKit.Shape(_content, "Current", ShapeKind.Pill);
                    hl.rectTransform.At(Width * 0.5f, y, Width - 28f, row - 2f);
                    hl.Style(Paint.Solid, Palette.Surface, Paint.Solid, Palette.Outline, 1f);
                    var dot = UIKit.Shape(_content, "Dot", ShapeKind.Circle);
                    dot.rectTransform.At(28f, y, 5f, 5f);
                    dot.Style(Paint.Solid, Palette.Dim, Paint.None, Color.clear);
                }
                for (int p = 0; p < n; p++)
                {
                    bool bit = ((c >> (n - 1 - p)) & 1) == 1;
                    var d = UIKit.Text(_content, bit ? "1" : "0", TextStyle.Digit.WithSize(17).WithColor(bit ? Palette.Cyan : Palette.Dim));
                    d.rectTransform.At(digitsX + p * 26f, y, 24f, row);
                }
                var arrow = UIKit.Glyph(_content, "arrow", Palette.Faint, 1.2f);
                arrow.rectTransform.At(digitsX + n * 26f + 18f, y, 13f, 13f);
                string outText = known ? (seen[c] ? "1" : "0") : "?";
                Color outColor = !known ? Palette.Faint : seen[c] ? Palette.Cyan : Palette.Text;
                var o = UIKit.Text(_content, outText, TextStyle.Digit.WithSize(17).WithColor(outColor));
                o.rectTransform.At(digitsX + n * 26f + 50f, y, 24f, row);
                if (known && seen[c])
                {
                    var glow = UIKit.Shape(_content, "Lit", ShapeKind.Circle);
                    glow.rectTransform.At(Width - 40f, y, 6f, 6f);
                    glow.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                    glow.Glow = 0.5f;
                    glow.GlowPaint = Paint.Signal;
                    glow.GlowRadius = 9f;
                }
            }

            int count = seen?.Count ?? 0;
            string footer = count == 0 ? "RUN TO OBSERVE THIS SETTING" : count == combos ? "EVERY INPUT SEEN" : $"SEEN {count} OF {combos}";
            Small(footer, 20f, height - 22f, count == combos ? Palette.Dim : Palette.Faint);
        }

        /// <summary>
        /// A box in a timed circuit: its inputs and output over the whole last run,
        /// or a note that it hasn't run with this setting yet.
        /// </summary>
        public void ShowTrace(BoxView box, int setting, RuleName name, CaseResult result, int boxIndex, int tick)
        {
            var def = box.Def;
            int n = def.Kind.InputCount;
            const float lane = 34f;
            float height = 96f + (n + 1) * lane + 40f;
            Begin(box, height);
            Header(def, setting, name);

            float left = 64f, right = Width - 20f;
            float laneWidth = right - left;
            if (result == null)
            {
                Small("RUN TO OBSERVE THIS SETTING", 20f, 96f + (n + 1) * lane * 0.5f, Palette.Faint);
                return;
            }

            int ticks = result.Ticks;
            for (int l = 0; l <= n; l++)
            {
                float y = 104f + l * lane + 6f;
                bool isOut = l == n;
                string label = isOut ? "OUT" : n == 1 ? "IN" : "IN " + (l + 1);
                var t = UIKit.Text(_content, label, TextStyle.Label.WithSize(11).WithTracking(0.14f).WithColor(isOut ? Palette.Dim : Palette.Faint), TextAnchor.MiddleLeft);
                t.rectTransform.At(20f + 40f, y, 80f, 16f);
                var wave = UIKit.Add<WaveGraphic>(_content, "Wave " + label);
                wave.raycastTarget = false;
                wave.rectTransform.At(left + laneWidth * 0.5f, y, laneWidth, 22f);
                var values = new bool[ticks];
                for (int k = 0; k < ticks; k++) values[k] = isOut ? result.BoxOutput(boxIndex, k) : result.BoxInputs(boxIndex, k)[l];
                wave.Set(ticks, values, null, int.MaxValue, false, false);
            }

            // The moment the board is showing.
            float tw = laneWidth / ticks;
            float cx = left + (Mathf.Clamp(tick, 0, ticks - 1) + 0.5f) * tw;
            var cursor = UIKit.Add<PathGraphic>(_content, "Cursor");
            cursor.raycastTarget = false;
            cursor.rectTransform.At(cx, 104f + n * lane * 0.5f + 6f, 4f, (n + 1) * lane);
            cursor.BaseColor = Palette.Dim.WithAlpha(0.5f);
            cursor.BaseWidth = 1f;
            float half = (n + 1) * lane * 0.5f;
            cursor.SetPoints(new List<Vector2> { new Vector2(0f, half), new Vector2(0f, -half) });

            Small("LAST RUN  ·  TICK " + (tick + 1).ToString("00"), 20f, height - 22f, Palette.Faint);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            _box = null;
            Tweener.Run(this, "fade", 0.12f, t => _group.alpha = 1f - t, Ease.OutQuad);
        }
    }

    /// <summary>
    /// Names a behaviour: one of eighteen symbols, or up to four typed letters.
    /// The name belongs to the kind and setting, so it appears on every box that
    /// behaves the same way, in every circuit.
    /// </summary>
    public sealed class NamingPalette : MonoBehaviour
    {
        public Action<RuleName> Chosen;
        public Action Closed;

        RectTransform _panel;
        CanvasGroup _group;
        RectTransform _blocker;
        readonly List<Cell> _cells = new List<Cell>();
        VoidText _title, _subtitle, _typed;
        ShapeGraphic _caret;
        PillButton _clear;
        string _text = "";
        int _focus;
        RuleName _current;
        float _blink;

        sealed class Cell
        {
            public string Glyph;
            public ShapeGraphic Frame;
            public GlyphGraphic Icon;
            public Clickable Click;
        }

        const int Columns = 6;
        const float CellSize = 48f;
        const float Pad = 24f;
        const int MaxLetters = 4;

        public bool IsOpen { get; private set; }

        static float PanelWidth => Pad * 2f + Columns * CellSize;
        static float PanelHeight => 92f + Rows * CellSize + 78f;
        static int Rows => (Glyphs.Palette.Length + Columns - 1) / Columns;

        public static NamingPalette Create(Transform layer)
        {
            var rt = UIKit.Rect(layer, "Naming").Centered(0f, 0f, VoidFrame.Width, VoidFrame.Height);
            var np = rt.gameObject.AddComponent<NamingPalette>();
            np.Build();
            rt.gameObject.SetActive(false);
            return np;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            // Clicking anywhere else closes the palette.
            _blocker = UIKit.Hit(root, "Blocker").rectTransform.Stretch();
            var blockClick = _blocker.gameObject.AddComponent<Clickable>();
            blockClick.HoverSound = false;
            blockClick.ClickSound = Sfx.Hover;
            blockClick.Clicked = Close;
            blockClick.RightClicked = Close;

            _panel = UIKit.Rect(root, "Panel");
            _group = _panel.Group();
            Popover.Backing(_panel);

            _title = UIKit.Text(_panel, "NAME THIS BEHAVIOUR", TextStyle.Label.WithSize(13).WithColor(Palette.Dim), TextAnchor.MiddleLeft);
            _title.rectTransform.At(Pad + 120f, 30f, 240f, 18f);
            _subtitle = UIKit.Text(_panel, "", TextStyle.Label.WithSize(12).WithTracking(0.16f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            _subtitle.rectTransform.At(Pad + 120f, 54f, 240f, 18f);

            for (int i = 0; i < Glyphs.Palette.Length; i++)
            {
                int col = i % Columns, row = i / Columns;
                float x = Pad + col * CellSize + CellSize * 0.5f;
                float y = 92f + row * CellSize + CellSize * 0.5f - 8f;
                var cellRt = UIKit.Rect(_panel, "Cell " + Glyphs.Palette[i]).At(x, y, CellSize - 6f, CellSize - 6f);
                var cell = new Cell { Glyph = Glyphs.Palette[i] };
                cell.Frame = UIKit.Shape(cellRt, "Frame");
                cell.Frame.rectTransform.Stretch();
                cell.Frame.Radius = 10f;
                cell.Frame.raycastTarget = true;
                cell.Icon = UIKit.Glyph(cellRt, cell.Glyph, Palette.Text, 1.7f);
                cell.Icon.rectTransform.Centered(0f, 0f, 24f, 24f);
                cell.Click = cellRt.gameObject.AddComponent<Clickable>();
                cell.Click.ClickSound = Sfx.Name;
                int index = i;
                cell.Click.Clicked = () => Choose(RuleName.FromGlyph(_cells[index].Glyph));
                cell.Click.HoverChanged = on => { if (on) { _focus = index; Restyle(); } };
                _cells.Add(cell);
            }

            float rowY = 92f + Rows * CellSize + 30f;
            var typeLabel = UIKit.Text(_panel, "OR TYPE", TextStyle.Label.WithSize(12).WithTracking(0.18f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            typeLabel.rectTransform.At(Pad + 60f, rowY, 120f, 18f);
            _typed = UIKit.Text(_panel, "", TextStyle.Label.WithSize(20).WithWeight(300).WithTracking(0.16f).WithColor(Palette.Text), TextAnchor.MiddleLeft);
            _typed.rectTransform.At(Pad + 96f + 60f, rowY, 120f, 26f);
            _caret = UIKit.Shape(_panel, "Caret");
            _caret.Radius = 0.5f;
            _caret.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
            _clear = PillButton.Create(_panel, "CLEAR", PanelWidth - Pad - 50f, rowY, 100f, 34f, false);
            _clear.Label.fontSize = 13;
            _clear.Click.ClickSound = Sfx.Close;
            _clear.Click.Clicked = () => Choose(default);
        }

        public void Open(BoxView box, RuleName current)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsOpen = true;
            _current = current;
            _text = current.Text ?? "";
            int index = Array.IndexOf(Glyphs.Palette, current.Glyph);
            _focus = index >= 0 ? index : 0;
            var kind = box.Def.Kind;
            _subtitle.text = kind.SettingCount > 1 ? $"{kind.Id}  ·  SETTING {box.Setting + 1} OF {kind.SettingCount}" : kind.Id;
            Popover.Place(_panel, box, PanelWidth, PanelHeight);
            _clear.Enabled = !current.IsEmpty;
            Restyle();
            App.Sound(Sfx.Open, 1.1f, 0.7f);
            _group.alpha = 0f;
            Tweener.Run(this, "fade", 0.14f, t => _group.alpha = t, Ease.OutQuad);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Tweener.Kill(this, "fade");
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Choose(RuleName name)
        {
            if (!IsOpen) return;
            Chosen?.Invoke(name);
            Close();
        }

        /// <summary>Keys go here while the palette is open.</summary>
        public void HandleInput()
        {
            if (GameInput.Down(Key.Escape)) { App.Sound(Sfx.Close); Close(); return; }
            if (GameInput.Down(Key.Enter))
            {
                App.Sound(Sfx.Name);
                Choose(_text.Length > 0 ? RuleName.FromText(_text) : RuleName.FromGlyph(_cells[_focus].Glyph));
                return;
            }
            if (GameInput.Down(Key.Delete)) { App.Sound(Sfx.Close); Choose(default); return; }
            if (GameInput.Down(Key.Backspace) && _text.Length > 0)
            {
                _text = _text.Substring(0, _text.Length - 1);
                App.Sound(Sfx.Tick, 0.9f);
                Restyle();
            }

            int move = 0;
            if (GameInput.Down(Key.Right)) move = 1;
            else if (GameInput.Down(Key.Left)) move = -1;
            else if (GameInput.Down(Key.Down)) move = Columns;
            else if (GameInput.Down(Key.Up)) move = -Columns;
            if (move != 0)
            {
                int next = _focus + move;
                if (next >= 0 && next < _cells.Count) { _focus = next; App.Sound(Sfx.Hover); Restyle(); }
            }

            foreach (char ch in GameInput.Typed)
            {
                if (_text.Length >= MaxLetters) break;
                if (char.IsLetterOrDigit(ch) || "+-*/=<>!?&|^~#%".IndexOf(ch) >= 0)
                {
                    _text += char.ToUpperInvariant(ch);
                    App.Sound(Sfx.Tick, 1.1f + _text.Length * 0.05f);
                    Restyle();
                }
            }
        }

        void Restyle()
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                bool focused = i == _focus && _text.Length == 0;
                bool current = c.Glyph == _current.Glyph;
                bool hot = focused || c.Click.Hovered;
                c.Frame.Style(Paint.Solid, hot ? Palette.Surface : Color.black,
                    current ? Paint.Signal : Paint.Solid, current ? Color.white : hot ? Palette.Faint : Palette.Rule, current ? 1.4f : 1f);
                c.Frame.Glow = current ? 0.25f : 0f;
                c.Frame.GlowPaint = Paint.Signal;
                c.Frame.GlowRadius = 12f;
                c.Icon.Paint = current ? Paint.Signal : Paint.Solid;
                c.Icon.Tint = current || hot ? Palette.Text : Palette.Dim;
            }
            _typed.text = _text;
            float w = _typed.MeasureWidth();
            float rowY = 92f + Rows * CellSize + 30f;
            _caret.rectTransform.At(Pad + 96f + w + 4f, rowY, 1.5f, 20f);
        }

        void Update()
        {
            if (!IsOpen) return;
            _blink += Time.unscaledDeltaTime;
            _caret.enabled = _text.Length < MaxLetters && (App.ReduceMotion || (_blink % 1.06f) < 0.6f);
        }
    }
}
