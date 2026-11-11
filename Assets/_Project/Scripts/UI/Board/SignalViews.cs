using System;
using System.Collections.Generic;
using System.Linq;
using Blackbox.Components;
using Blackbox.Game;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>
    /// A signal over a whole loop, of any type, fitted to a box. A steady bit,
    /// digit or shape shows as its value; a short pattern of digits or shapes as
    /// the values in a row, each as wide as its share of the loop; anything else as
    /// a line across the loop: pulses for bits, steps for digits and sampled waves,
    /// a smooth line for waves. Nothing (a block that sends nothing, an output with
    /// no wire) shows as a faint dash.
    /// </summary>
    public sealed class SignalView : MonoBehaviour
    {
        /// <summary>Patterns of up to this many runs show their values in a row.</summary>
        public const int MaxListed = 8;

        float _w, _h;
        Trace _trace;
        bool _hasTrace;
        ValueLook _look;
        float _ping;
        float _playhead = -1f;

        RectTransform _rt;
        ValueView _value;
        Medium _valueMedium;
        TraceGraphic _graph;
        RectTransform _strip;
        readonly List<ShapeGraphic> _shapes = new List<ShapeGraphic>();
        readonly List<VoidText> _digits = new List<VoidText>();
        VoidText _nothing, _caption;
        GlyphGraphic _speaker;

        public Trace Trace => _trace;
        public float Width => _w;

        public static SignalView Create(Transform parent, float w, float h, string name = "Signal")
        {
            var rt = UIKit.Rect(parent, name).Centered(0f, 0f, w, h);
            var view = rt.gameObject.AddComponent<SignalView>();
            view._rt = rt;
            view._w = w;
            view._h = h;

            view._graph = UIKit.Add<TraceGraphic>(rt, "Graph");
            view._graph.raycastTarget = false;
            view._graph.rectTransform.Centered(0f, 0f, w, h);
            view._graph.enabled = false;

            view._strip = UIKit.Rect(rt, "Strip").Centered(0f, 0f, w, h);

            view._nothing = UIKit.Text(rt, "—", TextStyle.Value.WithSize(Mathf.RoundToInt(h * 0.5f)).WithWeight(200).WithColor(Palette.Faint), TextAnchor.MiddleCenter, "Nothing");
            view._nothing.rectTransform.Centered(0f, 0f, w, h);
            view._nothing.enabled = false;
            return view;
        }

        /// <summary>A line of small text under the signal, such as a wave's settings.</summary>
        public string Caption
        {
            set
            {
                if (_caption == null)
                {
                    _caption = UIKit.Text(_rt, "", TextStyle.Label.WithSize(Mathf.Max(9, Mathf.RoundToInt(_h * 0.2f))).WithTracking(0.16f), TextAnchor.MiddleCenter, "Caption");
                    _caption.rectTransform.Centered(0f, -_h * 0.5f - 9f, _w + 60f, 16f);
                }
                _caption.text = value ?? "";
                Restyle();
            }
        }

        /// <summary>Lets the player click the signal to hear it.</summary>
        public Clickable MakePlayable(Action play)
        {
            var hit = UIKit.Hit(_rt, "Listen");
            hit.rectTransform.Centered(0f, 0f, _w + 12f, _h + 10f);
            float icon = Mathf.Clamp(_h * 0.3f, 11f, 15f);
            _speaker = UIKit.Glyph(_rt, "sound", Palette.Dim, 1.3f, "Speaker");
            _speaker.rectTransform.Centered(_w * 0.5f + icon * 0.5f, _h * 0.5f, icon, icon);
            _speaker.enabled = false;
            var c = hit.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = () =>
            {
                if (_trace == null) return;
                play();
                Ping();
            };
            c.HoverChanged = on => _speaker.enabled = on && _trace != null;
            return c;
        }

        /// <summary>Shows a signal, or null for nothing.</summary>
        public void Set(Trace trace, ValueLook look)
        {
            if (_hasTrace && ReferenceEquals(trace, _trace) && look == _look) return;
            bool sameShape = _hasTrace && trace != null && _trace != null && trace.Key == _trace.Key;
            _hasTrace = true;
            _trace = trace;
            _look = look;
            if (!sameShape) Rebuild();
            Restyle();
        }

        /// <summary>Where in the loop the signal is now, or negative to hide the playhead.</summary>
        public float Playhead
        {
            get => _playhead;
            set
            {
                if (Mathf.Abs(_playhead - value) < 1e-4f) return;
                _playhead = value;
                if (_graph.enabled) _graph.Playhead = value;
                if (_strip.gameObject.activeSelf && (_shapes.Count > 0 || _digits.Count > 0)) Restyle();
            }
        }

        public ValueLook Look
        {
            get => _look;
            set { if (_look != value) { _look = value; Restyle(); } }
        }

        public void Ping()
        {
            Tweener.Run(this, "ping", 0.45f, t => { _ping = 1f - t; Restyle(); }, Ease.Linear, 0f, () => { _ping = 0f; Restyle(); });
            if (_value != null && _value.gameObject.activeSelf) _value.Ping();
        }

        void Rebuild()
        {
            foreach (var s in _shapes) Destroy(s.gameObject);
            foreach (var d in _digits) Destroy(d.gameObject);
            _shapes.Clear();
            _digits.Clear();
            _graph.enabled = false;
            _strip.gameObject.SetActive(false);
            if (_value != null) _value.gameObject.SetActive(false);
            _nothing.enabled = _trace == null;
            if (_trace == null) return;

            var t = _trace;
            var m = t.Medium;
            if (t.IsFlat && !Media.IsWave(m))
            {
                if (_value == null || _valueMedium != m)
                {
                    if (_value != null) Destroy(_value.gameObject);
                    float size = Mathf.Min(_h, _w * 0.8f);
                    _value = ValueView.Create(_rt, m, size);
                    _valueMedium = m;
                }
                _value.gameObject.SetActive(true);
                _value.Set((int)Mathf.Round(t.First), ValueLook.Live);
                return;
            }

            var runs = t.Runs();
            if ((m == Medium.Shape || m == Medium.Decimal) && runs.Count <= MaxListed)
            {
                _strip.gameObject.SetActive(true);
                // Every digit or shape in a strip is drawn the same size, from a
                // typical run, so a value held longer isn't drawn bigger; its place
                // along the strip still shows when it comes and how long it lasts.
                var lengths = runs.Select(r => r.length).OrderBy(n => n).ToList();
                float typical = _w * lengths[lengths.Count / 2] / Trace.Steps;
                foreach (var (start, length, level) in runs)
                {
                    float x0 = -_w * 0.5f + _w * start / Trace.Steps;
                    float span = _w * length / Trace.Steps;
                    float cx = x0 + span * 0.5f;
                    if (m == Medium.Shape)
                    {
                        float size = Mathf.Min(_h * 0.62f, typical * 0.72f);
                        int v = (int)level;
                        int form = Shapes.Form(v);
                        var s = UIKit.Shape(_strip, "Shape", form == 0 ? ShapeKind.Circle : form == 1 ? ShapeKind.Triangle : form == 2 ? ShapeKind.Square : ShapeKind.Hexagon);
                        s.raycastTarget = false;
                        s.rectTransform.Centered(cx, 0f, size, size);
                        _shapes.Add(s);
                    }
                    else
                    {
                        int size = Mathf.RoundToInt(Mathf.Min(_h * 0.7f, typical * 1.1f));
                        var d = UIKit.Text(_strip, ((int)level).ToString(), TextStyle.Value.WithSize(Mathf.Max(10, size)).WithWeight(300), TextAnchor.MiddleCenter, "Digit");
                        d.raycastTarget = false;
                        d.rectTransform.Centered(cx, 0f, Mathf.Max(span, typical) + 10f, _h);
                        _digits.Add(d);
                    }
                }
                return;
            }

            _graph.enabled = true;
            _graph.Playhead = _playhead;
            switch (m)
            {
                case Medium.Binary:
                    _graph.SetLevels(t.Levels);
                    _graph.Min = -0.2f; _graph.Max = 1.2f; _graph.Stepped = true; _graph.Midline = false;
                    break;
                case Medium.Decimal:
                    _graph.SetLevels(t.Levels);
                    _graph.Min = -0.5f; _graph.Max = 9.5f; _graph.Stepped = true; _graph.Midline = false;
                    break;
                case Medium.Shape:
                    _graph.SetLevels(t.As(Medium.Binary).Levels);
                    _graph.Min = -0.2f; _graph.Max = 1.2f; _graph.Stepped = true; _graph.Midline = false;
                    break;
                default:
                    _graph.SetLevels(t.Levels);
                    _graph.Min = -Trace.Peak - 0.5f; _graph.Max = Trace.Peak + 0.5f;
                    _graph.Stepped = m == Medium.Sampled;
                    _graph.Midline = true;
                    break;
            }
        }

        void Restyle()
        {
            bool live = _look == ValueLook.Live;
            Color quiet = Color.Lerp(_look == ValueLook.Met ? Palette.Text : Palette.Dim, Color.white, _ping);
            if (_caption != null) _caption.color = live ? Palette.Dim : quiet;
            if (_value != null && _value.gameObject.activeSelf) _value.Look = _look;
            if (_graph.enabled)
            {
                _graph.Paint = live ? Paint.Signal : Paint.Solid;
                _graph.Tint = live ? Color.white : quiet;
                _graph.GlowAmount = live ? 0.3f + 0.4f * _ping : 0.3f * _ping;
                _graph.LineWidth = Mathf.Clamp(_h * 0.05f, 1.4f, 2.4f);
            }
            if (_trace == null) return;
            var runs = _shapes.Count > 0 || _digits.Count > 0 ? _trace.Runs() : null;
            int now = _playhead >= 0f ? Mathf.Clamp((int)(Mathf.Repeat(_playhead, 1f) * Trace.Steps), 0, Trace.Steps - 1) : -1;
            for (int i = 0; i < _shapes.Count; i++)
            {
                var (start, length, level) = runs[i];
                bool current = now >= start && now < start + length;
                bool solid = Shapes.Solid((int)level);
                int colour = Shapes.Colour((int)level);
                var s = _shapes[i];
                float stroke = Mathf.Max(1.3f, _h * 0.035f);
                if (live)
                {
                    Paint paint = colour == 0 ? Paint.Signal : Paint.Solid;
                    Color tint = colour == 0 ? Color.white : Palette.ShapeColours[colour];
                    s.Style(solid ? paint : Paint.None, tint, paint, tint, stroke);
                    s.Glow = (current ? 0.45f : 0.15f) + 0.4f * _ping;
                    s.GlowPaint = paint;
                    s.GlowColor = tint;
                    s.GlowRadius = _h * 0.25f;
                }
                else
                {
                    Color tint = colour == 0 ? quiet : Color.Lerp(quiet, Palette.ShapeColours[colour], 0.55f);
                    s.Style(solid ? Paint.Solid : Paint.None, tint, Paint.Solid, tint, stroke);
                    s.Glow = 0f;
                }
            }
            for (int i = 0; i < _digits.Count; i++)
            {
                var (start, length, _) = runs[i];
                bool current = now >= start && now < start + length;
                var d = _digits[i];
                d.SignalFill = live;
                d.color = live ? (current || now < 0 ? Color.white : Palette.Dim) : quiet;
            }
        }
    }

    /// <summary>
    /// An output device doing what its signal tells it, through the loop: a
    /// light glows, a fan spins, a servo swings its arm, a digit display counts,
    /// a speaker pulses with its sound, a display draws the signal. Under every
    /// device but the display, a small line shows what it does over the whole
    /// loop, so two devices are easy to compare. See <see cref="Devices"/>.
    /// </summary>
    public sealed class DeviceView : MonoBehaviour
    {

        Device _device;
        Trace _trace;
        float[] _reading;
        ValueLook _look;
        float _size, _w;
        float _loop = -1f;
        float _angle;
        float _ping;

        RectTransform _rt, _spinner, _arm;
        ShapeGraphic _bulb, _armShape, _ring;
        GlyphGraphic _fan, _base, _speaker;
        VoidText _number, _nothing;
        SignalView _display;
        TraceGraphic _graph;

        public Device Device => _device;
        public Trace Trace => _trace;

        /// <summary>The width a device takes up, for laying out cards around it.</summary>
        public static float WidthFor(Device d, float size) => d == Device.Display ? size * 2.3f : size * 1.7f;

        public static DeviceView Create(Transform parent, Device device, float size, string name = "Device")
        {
            float w = WidthFor(device, size);
            var rt = UIKit.Rect(parent, name).Centered(0f, 0f, w, size);
            var view = rt.gameObject.AddComponent<DeviceView>();
            view._rt = rt;
            view._device = device;
            view._size = size;
            view._w = w;

            float icon = size * 0.62f;
            float iconY = device == Device.Display ? 0f : size * 0.16f;
            float graphH = size * 0.24f;
            float graphY = -size * 0.5f + graphH * 0.5f;

            switch (device)
            {
                case Device.Display:
                    view._display = SignalView.Create(rt, w, size * 0.82f, "Display");
                    break;
                case Device.Light:
                    view._bulb = UIKit.Shape(rt, "Bulb", ShapeKind.Circle);
                    view._bulb.raycastTarget = false;
                    view._bulb.rectTransform.Centered(0f, iconY, icon * 0.8f, icon * 0.8f);
                    break;
                case Device.Fan:
                    view._spinner = UIKit.Rect(rt, "Spinner").Centered(0f, iconY, icon, icon);
                    view._fan = UIKit.Glyph(view._spinner, "fan", Palette.Dim, 1.4f, "Fan");
                    view._fan.rectTransform.Stretch();
                    view._ring = UIKit.Shape(rt, "Guard", ShapeKind.Circle);
                    view._ring.raycastTarget = false;
                    view._ring.rectTransform.Centered(0f, iconY, icon * 1.08f, icon * 1.08f);
                    break;
                case Device.Servo:
                    view._base = UIKit.Glyph(rt, "servo", Palette.Dim, 1.3f, "Base");
                    view._base.rectTransform.Centered(0f, iconY - icon * 0.08f, icon, icon);
                    view._arm = UIKit.Rect(rt, "Arm");
                    view._arm.anchorMin = view._arm.anchorMax = new Vector2(0.5f, 0.5f);
                    view._arm.pivot = new Vector2(0.5f, 0f);
                    view._arm.sizeDelta = new Vector2(Mathf.Max(3f, icon * 0.07f), icon * 0.44f);
                    view._arm.anchoredPosition = new Vector2(0f, iconY - icon * 0.08f - icon * 0.15f);
                    view._armShape = UIKit.Shape(view._arm, "Bar", ShapeKind.Pill);
                    view._armShape.raycastTarget = false;
                    view._armShape.rectTransform.Stretch();
                    break;
                case Device.Digits:
                    view._number = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(icon * 0.95f)).WithWeight(200), TextAnchor.MiddleCenter, "Number");
                    view._number.raycastTarget = false;
                    view._number.rectTransform.Centered(0f, iconY, w, icon * 1.1f);
                    view._ring = UIKit.Shape(rt, "Frame");
                    view._ring.raycastTarget = false;
                    view._ring.Radius = 6f;
                    view._ring.rectTransform.Centered(0f, iconY, icon * 1.25f, icon * 0.98f);
                    break;
                default:
                    view._speaker = UIKit.Glyph(rt, "sound", Palette.Dim, 1.4f, "Speaker");
                    view._speaker.rectTransform.Centered(0f, iconY, icon, icon);
                    break;
            }

            if (device != Device.Display)
            {
                view._graph = UIKit.Add<TraceGraphic>(rt, "Reading");
                view._graph.raycastTarget = false;
                view._graph.rectTransform.Centered(0f, graphY, w * 0.92f, graphH);
                view._graph.LineWidth = 1.4f;
            }

            view._nothing = UIKit.Text(rt, "—", TextStyle.Value.WithSize(Mathf.RoundToInt(size * 0.4f)).WithWeight(200).WithColor(Palette.Faint), TextAnchor.MiddleCenter, "Nothing");
            view._nothing.rectTransform.Centered(0f, iconY, w, size * 0.6f);
            view._nothing.enabled = false;
            view.Set(null, ValueLook.Live);
            return view;
        }

        /// <summary>Lets the player click the device to hear its signal.</summary>
        public void MakePlayable(Action play)
        {
            var hit = UIKit.Hit(_rt, "Listen");
            hit.rectTransform.Centered(0f, 0f, _w + 8f, _size + 8f);
            var c = hit.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = () =>
            {
                if (_trace == null) return;
                play();
                Ping();
            };
        }

        public void Ping()
        {
            Tweener.Run(this, "ping", 0.45f, t => { _ping = 1f - t; Restyle(); }, Ease.Linear, 0f, () => { _ping = 0f; Restyle(); });
            if (_display != null) _display.Ping();
        }

        /// <summary>Shows what the device does with a signal, or null for nothing.</summary>
        public void Set(Trace trace, ValueLook look)
        {
            _trace = trace;
            _look = look;
            _reading = trace != null && _device != Device.Display ? Devices.Reading(_device, trace) : null;
            if (_display != null) _display.Set(trace, look);
            _nothing.enabled = trace == null && _device != Device.Display;

            if (_graph != null)
            {
                _graph.enabled = trace != null;
                if (trace != null)
                {
                    if (_device == Device.Speaker)
                    {
                        // A speaker's line is its sound: the signal read as a wave.
                        _graph.SetLevels(trace.As(Medium.Wave).Levels);
                        _graph.Min = -Trace.Peak - 0.5f; _graph.Max = Trace.Peak + 0.5f;
                        _graph.Stepped = trace.Medium != Medium.Wave;
                        _graph.Midline = true;
                    }
                    else
                    {
                        _graph.SetLevels(_reading);
                        float low = 0f, high = 1f;
                        if (_device == Device.Digits) { low = 0f; high = 9f; foreach (float r in _reading) { low = Mathf.Min(low, r); high = Mathf.Max(high, r); } }
                        else if (_device == Device.Fan || _device == Device.Servo) low = -1f;
                        float pad = (high - low) * 0.1f;
                        _graph.Min = low - pad; _graph.Max = high + pad;
                        _graph.Stepped = trace.Medium != Medium.Wave;
                        _graph.Midline = low < 0f;
                    }
                }
            }
            Restyle();
        }

        public ValueLook Look
        {
            get => _look;
            set { if (_look != value) { _look = value; if (_display != null) _display.Look = value; Restyle(); } }
        }

        /// <summary>
        /// Moves the device to a point in the loop (0 to 1), or a negative number
        /// to hold it still at the start without a playhead.
        /// </summary>
        public void Tick(float loop, float dt)
        {
            _loop = loop;
            if (_display != null) _display.Playhead = loop;
            if (_graph != null) _graph.Playhead = _trace != null ? loop : -1f;
            if (_device == Device.Fan && _reading != null && loop >= 0f && !App.ReduceMotion)
                _angle -= ReadingNow() * 540f * dt;
            Restyle();
        }

        float ReadingNow()
        {
            if (_reading == null) return 0f;
            int k = _loop >= 0f ? Mathf.Clamp((int)(Mathf.Repeat(_loop, 1f) * Trace.Steps), 0, Trace.Steps - 1) : 0;
            return _reading[k];
        }

        float LevelNow()
        {
            if (_trace == null) return 0f;
            int k = _loop >= 0f ? Mathf.Clamp((int)(Mathf.Repeat(_loop, 1f) * Trace.Steps), 0, Trace.Steps - 1) : 0;
            return _trace[k];
        }

        void Restyle()
        {
            bool live = _look == ValueLook.Live;
            bool has = _trace != null;
            Color quiet = Color.Lerp(_look == ValueLook.Met ? Palette.Text : Palette.Dim, Color.white, _ping);
            Paint paint = live ? Paint.Signal : Paint.Solid;
            Color tint = live ? Color.white : quiet;

            if (_graph != null)
            {
                _graph.Paint = paint;
                _graph.Tint = live ? Color.white : quiet;
                _graph.GlowAmount = live ? 0.25f : 0f;
            }

            switch (_device)
            {
                case Device.Light:
                {
                    float bright = has ? ReadingNow() : 0f;
                    int colour = has ? Devices.LightColour(_trace.Medium, LevelNow()) : -1;
                    Color glow = colour >= 0 ? Palette.ShapeColours[colour] : Palette.Cyan;
                    if (!live) glow = Color.Lerp(Palette.Dim, glow, _look == ValueLook.Met ? 0.6f : 0.35f);
                    var fill = glow.WithAlpha(Mathf.Lerp(0f, live ? 0.95f : 0.6f, bright));
                    _bulb.Style(Paint.Solid, fill, Paint.Solid, bright > 0.01f ? glow.WithAlpha(0.9f) : (live ? Palette.Faint : quiet.WithAlpha(0.7f)), 1.4f);
                    _bulb.Glow = (live ? 0.8f : 0.3f) * bright + 0.3f * _ping;
                    _bulb.GlowPaint = Paint.Solid;
                    _bulb.GlowColor = glow;
                    _bulb.GlowRadius = _size * 0.4f;
                    break;
                }
                case Device.Fan:
                    _spinner.localEulerAngles = new Vector3(0f, 0f, _angle);
                    _fan.Paint = has ? paint : Paint.Solid;
                    _fan.Tint = has ? tint : Palette.Faint;
                    _fan.GlowAmount = live && has ? 0.2f + 0.4f * Mathf.Abs(ReadingNow()) : 0f;
                    _ring.Style(Paint.None, Color.clear, Paint.Solid, live ? Palette.Outline : quiet.WithAlpha(0.4f), 1.2f);
                    break;
                case Device.Servo:
                {
                    float position = has ? ReadingNow() : 0f;
                    _arm.localEulerAngles = new Vector3(0f, 0f, -position * 90f);
                    _base.Tint = live ? Palette.Dim : quiet;
                    _armShape.Style(has ? paint : Paint.Solid, has ? tint : Palette.Faint, Paint.None, Color.clear);
                    _armShape.Glow = live && has ? 0.35f : 0f;
                    _armShape.GlowPaint = Paint.Signal;
                    _armShape.GlowRadius = 10f;
                    break;
                }
                case Device.Digits:
                    _number.text = has ? Mathf.RoundToInt(ReadingNow()).ToString() : "";
                    _number.SignalFill = live && has;
                    _number.color = live ? Color.white : quiet;
                    _ring.Style(Paint.None, Color.clear, Paint.Solid, live ? Palette.Outline : quiet.WithAlpha(0.4f), 1.2f);
                    break;
                case Device.Speaker:
                {
                    float loud = has ? Mathf.Abs(_trace.Medium == Medium.Wave || _trace.Medium == Medium.Sampled ? LevelNow() / Trace.Peak : Devices.Amount(_trace.Medium, LevelNow())) : 0f;
                    _speaker.Paint = has ? paint : Paint.Solid;
                    _speaker.Tint = has ? tint : Palette.Faint;
                    _speaker.GlowAmount = live && has ? 0.15f + 0.6f * loud : 0f;
                    float scale = 1f + (live && has && !App.ReduceMotion ? 0.12f * loud : 0f);
                    _speaker.rectTransform.localScale = new Vector3(scale, scale, 1f);
                    break;
                }
            }
        }
    }
}
