using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>
    /// A target the player must meet, shown next to the thing it belongs to: a
    /// short label, the value wanted, and a tick or a cross. Block targets say
    /// SENDS, input targets say END AT.
    /// </summary>
    public sealed class TargetChip : MonoBehaviour
    {
        ShapeGraphic _pill;
        VoidText _label;
        ValueView _value;
        GlyphGraphic _mark;
        bool _matched;
        bool _solved;
        bool _initialised;

        public float Width { get; private set; }

        public static TargetChip Create(Transform parent, string label, Medium medium, int want, float scale, string name = "Target")
        {
            float valueSize = 20f * scale;
            float valueW = ValueView.WidthFor(medium, valueSize);
            var style = TextStyle.Label.WithSize(Mathf.Max(9, Mathf.RoundToInt(11 * scale))).WithTracking(0.22f);

            var rt = UIKit.Rect(parent, name);
            var chip = rt.gameObject.AddComponent<TargetChip>();
            chip._label = UIKit.Text(rt, label, style, TextAnchor.MiddleLeft, "Label");
            float labelW = chip._label.MeasureWidth();
            float pad = 12f * scale, gap = 9f * scale, markW = 13f * scale;
            float w = pad + labelW + gap + valueW + gap + markW + pad;
            float h = 30f * scale;
            chip.Width = w;
            rt.Centered(0f, 0f, w, h);

            chip._pill = UIKit.Shape(rt, "Pill", ShapeKind.Pill);
            chip._pill.rectTransform.Stretch();
            chip._pill.transform.SetAsFirstSibling();

            float x = -w * 0.5f + pad;
            chip._label.rectTransform.Centered(x + labelW * 0.5f, 0f, labelW + 4f, h);
            x += labelW + gap;
            chip._value = ValueView.Create(rt, medium, valueSize);
            chip._value.GetComponent<RectTransform>().Centered(x + valueW * 0.5f, 0f, valueW, valueSize);
            chip._value.Set(want, ValueLook.Target);
            x += valueW + gap;
            chip._mark = UIKit.Glyph(rt, "cross", Palette.Dim, 1.6f, "Mark");
            chip._mark.rectTransform.Centered(x + markW * 0.5f, 0f, markW, markW);
            chip.Restyle();
            chip._initialised = true;
            return chip;
        }

        public void SetMatched(bool matched)
        {
            if (_initialised && matched == _matched) return;
            _matched = matched;
            Restyle();
        }

        public bool Solved
        {
            get => _solved;
            set { _solved = value; Restyle(); }
        }

        void Restyle()
        {
            bool lit = _matched || _solved;
            _pill.Style(Paint.Solid, Color.black, lit ? Paint.Signal : Paint.Solid, lit ? Color.white : Palette.Outline, lit ? 1.4f : 1.2f);
            _pill.Glow = lit ? 0.18f : 0f;
            _pill.GlowPaint = Paint.Signal;
            _pill.GlowRadius = 14f;
            _label.color = lit ? Palette.Dim : Palette.Faint;
            _value.Look = lit ? ValueLook.Met : ValueLook.Target;
            _mark.Glyph = lit ? "check" : "cross";
            _mark.Paint = lit ? Paint.Signal : Paint.Solid;
            _mark.Tint = lit ? Color.white : Palette.Faint;
        }
    }

    /// <summary>
    /// An input: a circle holding the value it sends. Adjustable inputs are drawn
    /// brighter, with arrows above and below; fixed ones are faint and can't be
    /// clicked. An input with a target shows it underneath.
    /// </summary>
    public sealed class InputView : MonoBehaviour
    {
        public InputDef Def;
        public Vector2 Centre;
        public Vector2 OutPort;
        public float Radius;
        public float Reach;
        public TargetChip Target;

        IBoardHost _host;
        ShapeGraphic _ring;
        ValueView _value;
        VoidText _label;
        GlyphGraphic _up, _down;
        bool _hover, _focused, _solved;
        float _bump;

        public bool Adjustable => Def.Adjustable;

        public static InputView Create(RectTransform layer, InputDef def, Medium medium, BoardMetrics m, IBoardHost host)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float r = 36f * s;
            var rt = UIKit.Rect(layer, "Input " + def.Id).Centered(c.x, c.y, r * 2f, r * 2f);
            var view = rt.gameObject.AddComponent<InputView>();
            view.Def = def;
            view._host = host;
            view.Centre = c;
            view.Radius = r;
            view.OutPort = c + new Vector2(r, 0f);
            view.Reach = r;

            view._label = UIKit.Text(rt, def.Id, TextStyle.Label.WithSize(Mathf.RoundToInt(17 * s)), TextAnchor.MiddleRight, "Label");
            view._label.rectTransform.Centered(-r - 30f * s, 0f, 40f, 24f);

            var hit = UIKit.Hit(rt, "Hit");
            hit.rectTransform.Centered(0f, 0f, r * 2f + 8f, r * 2f + 8f);
            var click = hit.gameObject.AddComponent<Clickable>();
            click.HoverSound = false;
            click.ClickSound = def.Adjustable ? Sfx.Dial : Sfx.Hover;
            click.Clicked = () => host.InputStep(def.Index, +1);
            click.RightClicked = () => host.InputStep(def.Index, -1);
            click.Scrolled = d => host.InputStep(def.Index, d);
            click.HoverChanged = on => { view._hover = on; view.Restyle(); host.Hover(BoardItem.Input, def.Index, on); };

            view._ring = UIKit.Shape(rt, "Ring", ShapeKind.Circle);
            view._ring.rectTransform.Stretch();
            view._value = ValueView.Create(rt, medium, (medium == Medium.Wave ? 30f : 32f) * s, medium == Medium.Wave);
            if (medium == Medium.Wave) view._value.transform.localScale = Vector3.one * 0.82f;

            if (def.Adjustable)
            {
                view._up = Arrow(rt, "up", r + 15f * s, s, () => host.InputStep(def.Index, +1), view);
                view._down = Arrow(rt, "down", -r - 15f * s, s, () => host.InputStep(def.Index, -1), view);
                view.Reach = r + 24f * s;
            }

            if (def.Target.HasValue)
            {
                view.Target = TargetChip.Create(rt, "END AT", medium, def.Target.Value, s);
                float y = -view.Reach - 22f * s;
                view.Target.GetComponent<RectTransform>().Centered(0f, y, view.Target.Width, 30f * s);
                view.Reach = -y + 15f * s;
            }
            view.Restyle();
            return view;
        }

        static GlyphGraphic Arrow(RectTransform rt, string glyph, float y, float s, System.Action action, InputView view)
        {
            var holder = UIKit.Rect(rt, "Arrow " + glyph).Centered(0f, y, 40f * s, 24f * s);
            var g = UIKit.Glyph(holder, glyph, Palette.Dim, 1.6f, "Glyph");
            g.rectTransform.Centered(0f, 0f, 14f * s, 14f * s);
            UIKit.Hit(holder).rectTransform.Stretch();
            var c = holder.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Dial;
            c.Clicked = action;
            c.HoverChanged = on => { g.Tint = on ? Palette.Text : Palette.Dim; };
            return g;
        }

        public void SetValue(int value, bool bump)
        {
            _value.Set(value, ValueLook.Live);
            if (bump && !App.ReduceMotion)
                Tweener.Run(this, "bump", 0.3f, t => { _bump = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public bool Focused { get => _focused; set { if (_focused != value) { _focused = value; Restyle(); } } }
        public bool Solved { get => _solved; set { _solved = value; Restyle(); if (Target != null) Target.Solved = value; } }

        void Restyle()
        {
            bool adjustable = Def.Adjustable;
            Color edge = adjustable ? (_hover || _focused ? Palette.Text : Palette.Dim) : Palette.Outline;
            if (_bump > 0f) edge = Color.Lerp(edge, Color.white, _bump * 0.5f);
            if (_solved)
            {
                _ring.Style(Paint.Solid, Color.black, Paint.Signal, Color.white, 1.8f);
                _ring.Glow = 0.4f;
            }
            else
            {
                _ring.Style(Paint.Solid, Color.black, Paint.Solid, edge, adjustable ? 1.6f : 1.2f);
                _ring.Glow = _focused ? 0.3f : 0f;
            }
            _ring.Dashed = !adjustable;
            _ring.SetDash(3f, 5f);
            _ring.GlowPaint = Paint.Signal;
            _ring.GlowRadius = 18f;
            _label.color = _hover || _focused ? Palette.Text : Palette.Dim;
        }
    }

    /// <summary>
    /// One block: its kind code, its ports and the dots that are its states. A
    /// locked block has a dashed outline and a padlock. A block with a target shows
    /// it above, with the word SENDS.
    /// </summary>
    public sealed class BlockView : MonoBehaviour
    {
        public BlockDef Def;
        public Vector2 Centre;
        public Vector2 OutPort;
        public float Width, Height;
        /// <summary>How far the block and its parts reach above and below its centre.</summary>
        public float ReachUp, ReachDown;
        public TargetChip Target;
        readonly List<Vector2> _inPorts = new List<Vector2>();

        ShapeGraphic _outline;
        VoidText _code, _id;
        GlyphGraphic _lock;
        readonly List<ShapeGraphic> _pips = new List<ShapeGraphic>();
        readonly List<ShapeGraphic> _ports = new List<ShapeGraphic>();
        int _state;
        bool _hover, _focused, _solved;
        float _bump;

        public Vector2 InPort(int port) => _inPorts[Mathf.Clamp(port, 0, _inPorts.Count - 1)];
        public bool Changeable => !Def.Locked && Def.Kind.StateCount > 1;

        public static BlockView Create(RectTransform layer, BlockDef def, Medium medium, BoardMetrics m, IBoardHost host)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float w = 150f * s, h = 88f * s;
            var rt = UIKit.Rect(layer, "Block " + def.Id).Centered(c.x, c.y, w, h);
            var view = rt.gameObject.AddComponent<BlockView>();
            view.Def = def;
            view.Centre = c;
            view.Width = w;
            view.Height = h;
            view._state = def.State;

            int n = def.Kind.InputCount;
            float spread = n == 1 ? 0f : 22f * s;
            for (int p = 0; p < n; p++)
            {
                float y = n == 1 ? 0f : spread - p * (2f * spread / (n - 1));
                view._inPorts.Add(c + new Vector2(-w * 0.5f, y));
            }
            view.OutPort = c + new Vector2(w * 0.5f, 0f);

            var hit = UIKit.Hit(rt, "Hit");
            hit.rectTransform.Centered(0f, 0f, w + 12f, h + 12f);
            var click = hit.gameObject.AddComponent<Clickable>();
            click.HoverSound = false;
            click.ClickSound = view.Changeable ? Sfx.Dial : Sfx.Hover;
            click.Clicked = () => host.BlockStep(def.Index, +1);
            click.RightClicked = () => host.BlockStep(def.Index, -1);
            click.Scrolled = d => host.BlockStep(def.Index, d);
            click.HoverChanged = on => { view._hover = on; view.Restyle(); host.Hover(BoardItem.Block, def.Index, on); };

            view._outline = UIKit.Shape(rt, "Outline");
            view._outline.rectTransform.Stretch();
            view._outline.Radius = 16f * s;
            view._outline.SetDash(5f, 6f);

            view._id = UIKit.Text(rt, def.Id, TextStyle.Label.WithSize(Mathf.RoundToInt(14 * s)).WithTracking(0.1f), TextAnchor.MiddleLeft, "Id");
            view._id.Uppercase = false;
            view._id.rectTransform.Centered(-w * 0.5f + 14f * s + 30f, h * 0.5f - 15f * s, 60f, 20f);

            view._code = UIKit.Text(rt, def.Kind.Id, TextStyle.Value.WithSize(Mathf.RoundToInt(28 * s)).WithWeight(200).WithTracking(0.08f), TextAnchor.MiddleCenter, "Kind");
            view._code.rectTransform.Centered(0f, -3f * s, w, h * 0.6f);

            if (def.Locked)
            {
                view._lock = UIKit.Glyph(rt, "lock", Palette.Dim, 1.3f, "Lock");
                view._lock.rectTransform.Centered(w * 0.5f - 17f * s, h * 0.5f - 16f * s, 13f * s, 13f * s);
            }

            for (int p = 0; p < n; p++)
            {
                var dot = UIKit.Shape(rt, "In " + p, ShapeKind.Circle);
                dot.rectTransform.Centered(-w * 0.5f, view._inPorts[p].y - c.y, 7f, 7f);
                view._ports.Add(dot);
            }
            var outDot = UIKit.Shape(rt, "Out", ShapeKind.Circle);
            outDot.rectTransform.Centered(w * 0.5f, 0f, 7f, 7f);
            view._ports.Add(outDot);

            int count = def.Kind.StateCount;
            float pipY = -h * 0.5f - 14f * s;
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 20f * s;
                var pipRt = UIKit.Rect(rt, "State " + (i + 1)).Centered(x, pipY, 18f * s, 22f * s);
                var pip = UIKit.Shape(pipRt, "Dot", ShapeKind.Circle);
                pip.rectTransform.Centered(0f, 0f, 8f, 8f);
                view._pips.Add(pip);
                if (view.Changeable)
                {
                    UIKit.Hit(pipRt).rectTransform.Stretch();
                    var pc = pipRt.gameObject.AddComponent<Clickable>();
                    int state = i;
                    pc.ClickSound = Sfx.Dial;
                    pc.HoverSound = false;
                    pc.Clicked = () => host.BlockPip(def.Index, state);
                    pc.HoverChanged = on => { view._hover = on; view.Restyle(); host.Hover(BoardItem.Block, def.Index, on); };
                }
            }
            view.ReachUp = h * 0.5f;
            view.ReachDown = h * 0.5f + 24f * s;

            if (def.Target.HasValue)
            {
                view.Target = TargetChip.Create(rt, "SENDS", medium, def.Target.Value, s);
                float y = h * 0.5f + 21f * s;
                view.Target.GetComponent<RectTransform>().Centered(0f, y, view.Target.Width, 30f * s);
                view.ReachUp = y + 15f * s;
            }

            view.Restyle();
            return view;
        }

        public void SetState(int state, bool bump)
        {
            _state = state;
            Restyle();
            if (bump && !App.ReduceMotion)
                Tweener.Run(this, "bump", 0.28f, t => { _bump = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public bool Focused { get => _focused; set { if (_focused != value) { _focused = value; Restyle(); } } }
        public bool Solved { get => _solved; set { _solved = value; Restyle(); if (Target != null) Target.Solved = value; } }

        void Restyle()
        {
            bool locked = Def.Locked;
            Paint edgePaint = Paint.Solid;
            Color edge = locked ? Palette.Outline : Palette.Faint;
            float width = 1.4f, glow = 0f;
            if (_solved) { edgePaint = Paint.Signal; edge = Color.white; width = 1.7f; glow = 0.28f; }
            else if (_focused) { edge = Palette.Text; width = 1.6f; glow = 0.25f; }
            else if (_hover) edge = locked ? Palette.Faint : Palette.Dim;
            if (_bump > 0f) edge = Color.Lerp(edge, Palette.Text, _bump * 0.7f);

            _outline.Style(Paint.Solid, Color.black, edgePaint, edge, width);
            _outline.Dashed = locked;
            _outline.Glow = glow;
            _outline.GlowPaint = Paint.Signal;
            _outline.GlowRadius = 22f;

            _code.color = _hover || _focused ? Palette.Text : locked ? Palette.Faint : Palette.Dim;
            _id.color = _hover || _focused ? Palette.Dim : Palette.Faint;

            for (int i = 0; i < _pips.Count; i++)
            {
                bool active = i == _state;
                var pip = _pips[i];
                float size = active ? 8f : 5f;
                pip.rectTransform.sizeDelta = new Vector2(size, size);
                if (active)
                {
                    pip.Style(locked ? Paint.Solid : Paint.Signal, locked ? Palette.Dim : Color.white, Paint.None, Color.clear);
                    pip.Glow = locked ? 0f : 0.55f;
                    pip.GlowPaint = Paint.Signal;
                    pip.GlowRadius = 10f;
                }
                else
                {
                    pip.Style(Paint.Solid, Palette.Faint, Paint.None, Color.clear);
                    pip.Glow = 0f;
                }
            }
            foreach (var port in _ports)
                port.Style(Paint.Solid, _solved ? Palette.Dim : Palette.Outline, Paint.None, Color.clear);
        }
    }

    /// <summary>
    /// An output: what arrives now, and beside it the target it must match, joined
    /// by = or ≠. A met target lights the card. Outputs without a target only show
    /// their value.
    /// </summary>
    public sealed class OutputView : MonoBehaviour
    {
        public OutputDef Def;
        public Vector2 Centre;
        public Vector2 InPort;
        public float Width, Height;

        ShapeGraphic _frame;
        ValueView _now, _want;
        GlyphGraphic _relation;
        VoidText _label, _wantLabel;
        bool _matched, _solved, _hasTarget;
        float _flash;
        int _shown = -1;

        public bool HasTarget => _hasTarget;

        public static OutputView Create(RectTransform layer, OutputDef def, Medium medium, BoardMetrics m, IBoardHost host)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            bool target = def.Target.HasValue;
            float valueSize = (medium == Medium.Wave ? 36f : 42f) * s;
            float valueW = ValueView.WidthFor(medium, valueSize);
            float half = Mathf.Max(96f * s, valueW + 34f * s);
            float w = target ? half * 2f + 24f * s : half, h = 88f * s;
            var rt = UIKit.Rect(layer, "Output " + def.Id).Centered(c.x + (w - half) * 0.5f, c.y, w, h);
            var view = rt.gameObject.AddComponent<OutputView>();
            view.Def = def;
            view.Centre = c;
            view.Width = w;
            view.Height = h;
            view._hasTarget = target;
            view.InPort = new Vector2(c.x - half * 0.5f, c.y);

            view._frame = UIKit.Shape(rt, "Frame");
            view._frame.rectTransform.Stretch();
            view._frame.Radius = 18f * s;

            float nowX = -w * 0.5f + half * 0.5f;
            view._label = UIKit.Text(rt, def.Id, TextStyle.Label.WithSize(Mathf.RoundToInt(15 * s)), TextAnchor.MiddleLeft, "Label");
            view._label.rectTransform.Centered(-w * 0.5f + 4f + 40f, h * 0.5f + 15f * s, 80f, 20f);

            view._now = ValueView.Create(rt, medium, valueSize, medium == Medium.Wave, "Now");
            view._now.GetComponent<RectTransform>().Centered(nowX, 0f, valueW, valueSize);
            if (medium == Medium.Wave) AddPlayer(view._now, host);

            if (target)
            {
                float wantX = w * 0.5f - half * 0.5f;
                view._relation = UIKit.Glyph(rt, "notequal", Palette.Faint, 1.8f, "Relation");
                view._relation.rectTransform.Centered((nowX + wantX) * 0.5f, 0f, 20f * s, 20f * s);
                view._want = ValueView.Create(rt, medium, valueSize, medium == Medium.Wave, "Want");
                view._want.GetComponent<RectTransform>().Centered(wantX, 0f, valueW, valueSize);
                view._want.Set(def.Target.Value, ValueLook.Target);
                if (medium == Medium.Wave) AddPlayer(view._want, host);
                view._wantLabel = UIKit.Text(rt, "TARGET", TextStyle.Label.WithSize(Mathf.RoundToInt(11 * s)).WithTracking(0.26f), TextAnchor.MiddleCenter, "Target label");
                view._wantLabel.rectTransform.Centered(wantX, h * 0.5f + 15f * s, 120f, 18f);
            }
            view.Restyle();
            return view;
        }

        static void AddPlayer(ValueView value, IBoardHost host)
        {
            var hit = UIKit.Hit(value.transform, "Play");
            hit.rectTransform.Stretch();
            var c = hit.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = () => host.PlayValue(value.Value);
        }

        public void SetValue(int value, bool matched, bool flash)
        {
            bool changed = value != _shown;
            _shown = value;
            _now.Set(value, ValueLook.Live);
            _matched = matched;
            Restyle();
            if (flash && changed && !App.ReduceMotion)
                Tweener.Run(this, "flash", 0.6f, t => { _flash = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public bool Matched => _matched;

        public bool Solved { get => _solved; set { _solved = value; Restyle(); } }

        void Restyle()
        {
            bool lit = _hasTarget && (_matched || _solved);
            if (lit)
            {
                _frame.Style(Paint.Solid, Color.black, Paint.Signal, Color.white, 1.7f);
                _frame.Glow = (_solved ? 0.5f : 0.3f) + 0.25f * _flash;
                _frame.GlowRadius = 24f;
            }
            else
            {
                Color stroke = Color.Lerp(_hasTarget ? Palette.Faint : Palette.Outline, Palette.Dim, _flash);
                _frame.Style(Paint.Solid, Color.black, Paint.Solid, stroke, 1.3f);
                _frame.Glow = 0.12f * _flash;
                _frame.GlowRadius = 20f;
            }
            _frame.GlowPaint = Paint.Signal;
            if (_relation != null)
            {
                _relation.Glyph = lit ? "equals" : "notequal";
                _relation.Paint = lit ? Paint.Signal : Paint.Solid;
                _relation.Tint = lit ? Color.white : Palette.Dim;
            }
            if (_want != null) _want.Look = lit ? ValueLook.Met : ValueLook.Target;
            if (_wantLabel != null) _wantLabel.color = lit ? Palette.Dim : Palette.Faint;
            _label.color = Palette.Dim;
        }
    }
}
