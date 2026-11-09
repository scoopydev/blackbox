using System.Collections.Generic;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>An input: a circle holding the value it sends.</summary>
    public sealed class InputView : MonoBehaviour
    {
        public NodeDef Def;
        public Vector2 Centre;
        public Vector2 OutPort;
        ShapeGraphic _ring;
        VoidText _value;
        bool? _current;
        bool _stale;

        public static InputView Create(RectTransform layer, NodeDef def, BoardMetrics m)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            var rt = UIKit.Rect(layer, "Input " + def.Id).Centered(c.x, c.y, 80f * s, 80f * s);
            var view = rt.gameObject.AddComponent<InputView>();
            view.Def = def;
            view.Centre = c;
            float r = 36f * s;
            view.OutPort = c + new Vector2(r, 0f);

            var label = UIKit.Text(rt, "IN " + def.Id, TextStyle.Label);
            label.rectTransform.Centered(0f, r + 30f * s, 160f, 24f);

            view._ring = UIKit.Shape(rt, "Ring", ShapeKind.Circle);
            view._ring.rectTransform.Centered(0f, 0f, r * 2f, r * 2f);
            view._value = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(30 * s)).WithWeight(400));
            view._value.rectTransform.Centered(0f, 0f, r * 2f, r * 2f);
            view.SetValue(null);
            return view;
        }

        public void SetValue(bool? value, bool stale = false)
        {
            _current = value;
            _stale = stale;
            Restyle();
        }

        public void SetStale(bool stale)
        {
            _stale = stale;
            Restyle();
        }

        void Restyle()
        {
            bool on = _current == true;
            _ring.Style(Paint.Solid, Color.black, on ? Paint.Signal : Paint.Solid, on ? Color.white.WithAlpha(_stale ? 0.4f : 1f) : Palette.Outline, on ? 2f : 1.3f);
            _ring.Glow = on && !_stale ? 0.45f : 0f;
            _ring.GlowRadius = 18f;
            _value.text = _current.HasValue ? (_current.Value ? "1" : "0") : "";
            _value.color = on ? Palette.Text : Palette.Dim;
        }
    }

    /// <summary>An output: the value that arrived, and the value the puzzle wants.</summary>
    public sealed class OutputView : MonoBehaviour
    {
        public NodeDef Def;
        public Vector2 Centre;
        public Vector2 InPort;
        ShapeGraphic _frame;
        VoidText _value;
        VoidText _want;
        ShapeGraphic _miss;
        bool? _currentValue;
        sbyte _wantValue = -1;
        bool _showWant = true;
        bool _ok = true;
        bool _stale;
        bool _solved;
        float _pulse;
        float _flash;

        public static OutputView Create(RectTransform layer, NodeDef def, BoardMetrics m)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float w = 112f * s, h = 98f * s;
            var rt = UIKit.Rect(layer, "Output " + def.Id).Centered(c.x, c.y, w, h);
            var view = rt.gameObject.AddComponent<OutputView>();
            view.Def = def;
            view.Centre = c;
            view.InPort = c - new Vector2(w * 0.5f, 0f);

            var label = UIKit.Text(rt, "OUT " + def.Id, TextStyle.Label);
            label.rectTransform.Centered(0f, h * 0.5f + 26f * s, 180f, 24f);

            view._frame = UIKit.Shape(rt, "Frame");
            view._frame.rectTransform.Stretch();
            view._frame.Radius = 20f * s;
            view._value = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(48 * s)).WithWeight(200));
            view._value.rectTransform.Stretch();
            view._want = UIKit.Text(rt, "", TextStyle.Label);
            view._want.rectTransform.Centered(0f, -h * 0.5f - 26f * s, 200f, 24f);
            view._miss = UIKit.Shape(rt, "Miss", ShapeKind.Circle);
            view._miss.rectTransform.Centered(w * 0.5f + 26f * s, 0f, 9f, 9f);
            view._miss.Style(Paint.Solid, Palette.Violet, Paint.None, Color.clear);
            view._miss.GlowPaint = Paint.Solid;
            view._miss.GlowColor = Palette.Violet;
            view._miss.Glow = 0.7f;
            view._miss.GlowRadius = 12f;
            view.Restyle();
            return view;
        }

        public void SetValue(bool? value, sbyte want, bool ok, bool stale)
        {
            _currentValue = value;
            _wantValue = want;
            _ok = ok;
            _stale = stale;
            Restyle();
        }

        public void SetStale(bool stale)
        {
            _stale = stale;
            Restyle();
        }

        public bool Solved
        {
            get => _solved;
            set { _solved = value; Restyle(); }
        }

        /// <summary>Whether the wanted value is written under the output. Off on the title screen.</summary>
        public bool ShowWant
        {
            get => _showWant;
            set { _showWant = value; Restyle(); }
        }

        /// <summary>A brief brightening when a signal arrives.</summary>
        public void Flash(bool sound = true)
        {
            if (sound && _currentValue == true) App.Sound(Sfx.Arrive, 0.94f + Def.Index * 0.12f, 0.8f);
            if (App.ReduceMotion) return;
            Tweener.Run(this, "flash", 0.5f, t => { _flash = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        void Restyle()
        {
            bool has = _currentValue.HasValue;
            bool on = _currentValue == true;
            bool matched = has && _ok && _wantValue >= 0;
            if (_solved)
            {
                _frame.Style(Paint.Solid, Color.black, Paint.Signal, Color.white, 1.8f);
                _frame.Glow = 0.5f;
                _frame.GlowRadius = 26f;
            }
            else
            {
                Color stroke = matched && !_stale ? Palette.Faint : Palette.Outline;
                if (_flash > 0f) stroke = Color.Lerp(stroke, Palette.Dim, _flash);
                _frame.Style(Paint.Solid, Color.black, Paint.Solid, stroke, 1.3f);
                _frame.Glow = on && !_stale ? 0.12f + 0.3f * _flash : 0f;
                _frame.GlowPaint = Paint.Signal;
                _frame.GlowRadius = 20f;
            }
            _value.text = has ? (on ? "1" : "0") : "·";
            _value.color = !has ? Palette.Faint : _stale ? Palette.Dim : Palette.Text;
            _want.text = !_showWant ? "" : _wantValue < 0 ? "WANT ANY" : "WANT " + _wantValue;
            _miss.enabled = has && !_ok && !_stale && !_solved;
        }

        void Update()
        {
            if (!_miss.enabled) return;
            if (App.ReduceMotion) { _miss.canvasRenderer.SetAlpha(1f); return; }
            _pulse += Time.unscaledDeltaTime;
            _miss.canvasRenderer.SetAlpha(0.35f + 0.65f * (0.5f - 0.5f * Mathf.Cos(_pulse * Mathf.PI * 2f / 2.4f)));
        }
    }

    public enum BoxHighlight { None, Hover, Probe }

    /// <summary>
    /// One unknown box: a faint outline, a "?" or the name the player gave this
    /// behaviour, its kind code, its ports, and the dots that are its settings.
    /// </summary>
    public sealed class BoxView : MonoBehaviour
    {
        public BoxDef Def;
        public Vector2 Centre;
        public Vector2 OutPort;
        readonly List<Vector2> _inPorts = new List<Vector2>();

        IBoardHost _host;
        ShapeGraphic _outline;
        VoidText _question;
        GlyphGraphic _glyph;
        VoidText _custom;
        VoidText _code;
        GlyphGraphic _lock;
        readonly List<ShapeGraphic> _pips = new List<ShapeGraphic>();
        readonly List<ShapeGraphic> _portIn = new List<ShapeGraphic>();
        ShapeGraphic _portOut;
        float _w, _h, _scale;

        int _setting;
        bool _named;
        BoxHighlight _highlight;
        bool _solved;
        bool[] _ins;
        bool _out;
        float _portAlpha = 1f;
        float _bump;

        public Vector2 InPort(int port) => _inPorts[Mathf.Clamp(port, 0, _inPorts.Count - 1)];
        public float Width => _w;
        public float Height => _h;
        public int Setting => _setting;

        public static BoxView Create(RectTransform layer, BoxDef def, BoardMetrics m, IBoardHost host)
        {
            float s = m.Scale;
            Vector2 c = m.Position(def.At);
            float w = 168f * s, h = 128f * s;
            var rt = UIKit.Rect(layer, "Box " + def.Id).Centered(c.x, c.y, w, h);
            var view = rt.gameObject.AddComponent<BoxView>();
            view.Def = def;
            view.Centre = c;
            view._host = host;
            view._w = w;
            view._h = h;
            view._scale = s;
            view._setting = def.Setting;

            int n = def.Kind.InputCount;
            float spread = n == 1 ? 0f : n == 2 ? 30f * s : 38f * s;
            for (int p = 0; p < n; p++)
            {
                float y = n == 1 ? 0f : spread - p * (2f * spread / (n - 1));
                view._inPorts.Add(c + new Vector2(-w * 0.5f, y));
            }
            view.OutPort = c + new Vector2(w * 0.5f, 0f);

            // Hit area covers the box and its dots, a little generously.
            var hit = UIKit.Hit(rt, "Hit");
            hit.rectTransform.Centered(0f, -10f * s, w + 16f, h + 40f * s);
            var click = hit.gameObject.AddComponent<Clickable>();
            click.ClickSound = def.Locked ? Sfx.Hover : Sfx.Dial;
            click.HoverSound = false;
            click.Clicked = () => host.BoxClicked(def.Index);
            click.RightClicked = () => host.BoxRightClicked(def.Index);
            click.Scrolled = d => host.BoxScrolled(def.Index, d);
            click.HoverChanged = on => host.BoxHover(def.Index, on);
            click.Interactable = true;

            view._outline = UIKit.Shape(rt, "Outline");
            view._outline.rectTransform.Stretch();
            view._outline.Radius = 22f * s;
            if (def.Locked) view._outline.SetDash(5f, 6f);

            view._code = UIKit.Text(rt, def.Kind.Id, TextStyle.Label.WithSize(Mathf.RoundToInt(14 * s)).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleLeft);
            view._code.rectTransform.Centered(-w * 0.5f + 14f * s + 40f, h * 0.5f - 18f * s, 80f, 20f);

            if (def.Locked)
            {
                view._lock = UIKit.Glyph(rt, "lock", Palette.Faint, 1.3f);
                view._lock.rectTransform.Centered(w * 0.5f - 20f * s, h * 0.5f - 19f * s, 13f, 13f);
            }

            view._question = UIKit.Text(rt, "?", TextStyle.Value.WithSize(Mathf.RoundToInt(48 * s)).WithWeight(200).WithColor(Palette.Faint));
            view._question.rectTransform.Centered(0f, 0f, w, h);
            view._glyph = UIKit.Glyph(rt, null, Palette.Text, 2.2f * s);
            view._glyph.rectTransform.Centered(0f, 0f, 50f * s, 50f * s);
            view._custom = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(32 * s)).WithWeight(300).WithTracking(0.08f));
            view._custom.Uppercase = true;
            view._custom.rectTransform.Centered(0f, 0f, w, h);

            for (int p = 0; p < n; p++)
            {
                var dot = UIKit.Shape(rt, "In " + p, ShapeKind.Circle);
                dot.rectTransform.Centered(-w * 0.5f, view._inPorts[p].y - c.y, 9f, 9f);
                view._portIn.Add(dot);
            }
            view._portOut = UIKit.Shape(rt, "Out", ShapeKind.Circle);
            view._portOut.rectTransform.Centered(w * 0.5f, 0f, 9f, 9f);

            int count = def.Kind.SettingCount;
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 22f;
                var pipRt = UIKit.Rect(rt, "Setting " + (i + 1)).Centered(x, -h * 0.5f - 24f * s, 20f, 26f);
                var pip = UIKit.Shape(pipRt, "Dot", ShapeKind.Circle);
                pip.rectTransform.Centered(0f, 0f, 9f, 9f);
                view._pips.Add(pip);
                if (!def.Locked && count > 1)
                {
                    UIKit.Hit(pipRt).rectTransform.Stretch();
                    var pc = pipRt.gameObject.AddComponent<Clickable>();
                    int setting = i;
                    pc.ClickSound = Sfx.Dial;
                    pc.HoverSound = false;
                    pc.Clicked = () => host.PipClicked(def.Index, setting);
                    pc.HoverChanged = on => host.BoxHover(def.Index, on);
                }
            }

            view.RefreshName();
            view.Restyle();
            return view;
        }

        public void SetSetting(int setting, bool bump)
        {
            _setting = setting;
            RefreshName();
            if (bump && !App.ReduceMotion)
                Tweener.Run(this, "bump", 0.28f, t => { _bump = 1f - t; Restyle(); }, Ease.OutQuad);
        }

        public void RefreshName()
        {
            var name = _host.NameOf(Def, _setting);
            _named = !name.IsEmpty;
            _question.enabled = !_named;
            _glyph.Glyph = name.Glyph;
            _glyph.enabled = !string.IsNullOrEmpty(name.Glyph);
            _custom.text = name.Text ?? "";
            _custom.enabled = !string.IsNullOrEmpty(name.Text);
            Restyle();
        }

        public BoxHighlight Highlight
        {
            get => _highlight;
            set { if (_highlight != value) { _highlight = value; Restyle(); } }
        }

        public bool Solved
        {
            get => _solved;
            set { _solved = value; Restyle(); }
        }

        /// <summary>Lights the ports for the values passing through. Null clears them.</summary>
        public void SetPorts(bool[] ins, bool output, float alpha, float delay = 0f)
        {
            Tweener.Kill(this, "ports");
            _portAlpha = alpha;
            if (delay > 0f && ins != null)
            {
                // Inputs light as the signal reaches them; the output a beat later.
                _ins = ins;
                _out = false;
                Restyle();
                var outputValue = output;
                Tweener.After(this, "ports", delay, () => { _out = outputValue; Restyle(); });
                return;
            }
            _ins = ins;
            _out = ins != null && output;
            Restyle();
        }

        public void SetPortAlpha(float alpha)
        {
            _portAlpha = alpha;
            Restyle();
        }

        void Restyle()
        {
            Color edge;
            Paint edgePaint = Paint.Solid;
            float width = 1.4f;
            float glow = 0f;

            if (_solved)
            {
                edgePaint = Paint.Signal;
                edge = Color.white;
                width = 1.7f;
                glow = 0.28f;
            }
            else if (_highlight == BoxHighlight.Probe)
            {
                edgePaint = Paint.Signal;
                edge = Color.white;
                width = 1.8f;
                glow = 0.4f;
            }
            else
            {
                edge = _named ? Palette.Faint : Palette.Outline;
                if (_highlight == BoxHighlight.Hover) edge = Palette.Dim;
                if (_named) width = 1.6f;
            }
            if (_bump > 0f) edge = Color.Lerp(edge, Palette.Text, _bump * 0.6f);

            _outline.Style(Paint.Solid, Color.black, edgePaint, edge, width);
            _outline.Dashed = Def.Locked;
            _outline.Glow = glow;
            _outline.GlowPaint = Paint.Signal;
            _outline.GlowRadius = 22f;

            _code.color = _named || _highlight != BoxHighlight.None ? Palette.Dim : Palette.Faint;
            _question.color = _highlight != BoxHighlight.None ? Palette.Dim : Palette.Faint;

            for (int i = 0; i < _pips.Count; i++)
            {
                bool active = i == _setting;
                var pip = _pips[i];
                float size = active ? 9f : 5.5f;
                pip.rectTransform.sizeDelta = new Vector2(size, size);
                if (active)
                {
                    pip.Style(Def.Locked ? Paint.Solid : Paint.Signal, Def.Locked ? Palette.Dim : Color.white, Paint.None, Color.clear);
                    pip.Glow = Def.Locked ? 0f : 0.55f;
                    pip.GlowPaint = Paint.Signal;
                    pip.GlowRadius = 10f;
                }
                else
                {
                    pip.Style(Paint.Solid, Palette.Faint, Paint.None, Color.clear);
                    pip.Glow = 0f;
                }
            }

            for (int p = 0; p < _portIn.Count; p++)
            {
                bool lit = _ins != null && p < _ins.Length && _ins[p];
                StylePort(_portIn[p], lit);
            }
            StylePort(_portOut, _out);
        }

        void StylePort(ShapeGraphic dot, bool lit)
        {
            dot.Style(lit ? Paint.Signal : Paint.Solid, lit ? Color.white.WithAlpha(_portAlpha) : Palette.Outline, Paint.None, Color.clear);
            dot.Glow = lit ? 0.5f * _portAlpha : 0f;
            dot.GlowPaint = Paint.Signal;
            dot.GlowRadius = 10f;
        }
    }

    /// <summary>A wire: a dark hairline that lights and flows while it carries a 1.</summary>
    public sealed class WireView : MonoBehaviour
    {
        public WireDef Def;
        public int FromNode;
        PathGraphic _path;
        FlowGraphic _flow;
        bool _lit;
        bool _value;
        bool _celebrate;
        bool _stale;

        public static WireView Create(RectTransform layer, WireDef def, Vector2 from, Vector2 to, BoardMetrics m)
        {
            var rt = UIKit.Rect(layer, $"Wire {def.From.Id}>{def.To.Id}.{def.ToPort}").Stretch();
            var view = rt.gameObject.AddComponent<WireView>();
            view.Def = def;
            view._path = rt.gameObject.AddComponent<PathGraphic>();
            view._path.raycastTarget = false;

            var flowRt = UIKit.Rect(rt, "Flow").Stretch();
            view._flow = flowRt.gameObject.AddComponent<FlowGraphic>();
            view._flow.raycastTarget = false;
            view._flow.Source = view._path;

            var pts = new List<Vector2>();
            if (to.x > from.x + 24f)
            {
                MeshKit.WireCurve(pts, from, to);
            }
            else
            {
                // A loop back: out to the right, down (or up) to the return row, back left, and in.
                float row = def.ViaRow ?? Mathf.Max(def.From.At.Row, def.To.At.Row) + 0.9f;
                float y = m.RowY(row);
                var corners = new List<Vector2>
                {
                    from,
                    new Vector2(from.x + 46f, from.y),
                    new Vector2(from.x + 46f, y),
                    new Vector2(to.x - 46f, y),
                    new Vector2(to.x - 46f, to.y),
                    to,
                };
                MeshKit.RoundedPolyline(pts, corners, 28f);
            }
            view._path.SetPoints(pts);
            view._path.BaseColor = Palette.Outline;
            view._path.BaseWidth = 1.5f;
            view._path.LitWidth = 2.6f;
            view._path.SetLit(0f, 0f);
            return view;
        }

        /// <summary>Sets whether the wire carries a 1. With a step time the light travels along it.</summary>
        public void SetValue(bool value, bool celebrate, float delay, float step, bool stale = false)
        {
            _value = value;
            _celebrate = celebrate;
            _stale = stale;
            _path.LitAlpha = stale ? 0.22f : 1f;
            bool lit = value || celebrate;
            _flow.Animate = !App.ReduceMotion && !stale;

            Tweener.Kill(this, "travel");
            if (step <= 0f || App.ReduceMotion)
            {
                _lit = lit;
                _path.Spark = -1f;
                _path.SetLit(0f, lit ? 1f : 0f);
                _flow.SetVerticesDirty();
                return;
            }

            if (lit && !_lit)
            {
                _path.SetLit(0f, 0f);
                Tweener.Run(this, "travel", step, t =>
                {
                    _path.SetLit(0f, t);
                    _path.Spark = t < 1f ? t : -1f;
                    _flow.SetVerticesDirty();
                }, Ease.Linear, delay);
            }
            else if (!lit && _lit)
            {
                Tweener.Run(this, "travel", step, t =>
                {
                    _path.SetLit(t, 1f);
                    _path.Spark = -1f;
                    _flow.SetVerticesDirty();
                }, Ease.Linear, delay, () => _path.SetLit(0f, 0f));
            }
            else
            {
                _path.SetLit(0f, lit ? 1f : 0f);
                _path.Spark = -1f;
                _flow.SetVerticesDirty();
            }
            _lit = lit;
        }

        public void SetStale(bool stale)
        {
            _stale = stale;
            _path.LitAlpha = stale ? 0.22f : 1f;
            _flow.Animate = !App.ReduceMotion && !stale;
            _flow.SetVerticesDirty();
        }

        public void SetCelebrate(bool on, float delay)
        {
            if (on == _celebrate) return;
            _celebrate = on;
            if (on) SetValue(_value, true, delay, 0.22f, _stale);
            else SetValue(_value, false, 0f, 0f, _stale);
        }
    }
}
