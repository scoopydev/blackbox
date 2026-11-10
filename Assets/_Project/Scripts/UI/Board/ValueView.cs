using System;
using Blackbox.Components;
using Blackbox.Game;
using UnityEngine;

namespace Blackbox.UI.Board
{
    /// <summary>How a value is drawn.</summary>
    public enum ValueLook
    {
        /// <summary>A live signal: lit in the signal gradient.</summary>
        Live,
        /// <summary>A value the player is asked for: quiet grey.</summary>
        Target,
        /// <summary>A target that is met: brighter, but still not a signal.</summary>
        Met,
    }

    /// <summary>
    /// One signal value in any medium, drawn to fit a square of side <c>Size</c>:
    /// a digit for binary and decimal, a filled or outlined form for shapes, and a
    /// few cycles of the waveform (wider than tall) for waves, smooth or in steps. A
    /// value that isn't known yet (-1), such as an output before the circuit has
    /// run, shows a faint ?.
    /// </summary>
    public sealed class ValueView : MonoBehaviour
    {
        Medium _medium;
        int _value = -1;
        ValueLook _look;
        float _size;
        bool _caption;

        VoidText _digit;
        ShapeGraphic _shape;
        WaveGraphic _wave;
        VoidText _settings;
        VoidText _unknown;
        GlyphGraphic _speaker;
        bool _styled;
        float _ping;
        Vector3 _restScale = Vector3.one;
        bool _pinging;

        public int Value => _value;
        public Medium Medium => _medium;

        /// <summary>The width the value takes up, for laying out cards around it.</summary>
        public static float WidthFor(Medium m, float size) => Media.IsWave(m) ? size * 1.7f : size;

        /// <param name="caption">For waves: write the settings (frequency, amplitude, phase or steps) under the line.</param>
        public static ValueView Create(Transform parent, Medium medium, float size, bool caption = false, string name = "Value")
        {
            float w = WidthFor(medium, size);
            var rt = UIKit.Rect(parent, name).Centered(0f, 0f, w, size);
            var view = rt.gameObject.AddComponent<ValueView>();
            view._medium = medium;
            view._size = size;
            view._caption = caption;

            switch (medium)
            {
                case Medium.Shape:
                    view._shape = UIKit.Shape(rt, "Form", ShapeKind.Circle);
                    view._shape.rectTransform.Stretch();
                    break;
                case Medium.Wave:
                case Medium.Sampled:
                    view._wave = UIKit.Add<WaveGraphic>(rt, "Wave");
                    view._wave.raycastTarget = false;
                    view._wave.rectTransform.Centered(0f, caption ? size * 0.12f : 0f, w, caption ? size * 0.7f : size * 0.86f);
                    if (caption)
                    {
                        view._settings = UIKit.Text(rt, "", TextStyle.Label.WithSize(Mathf.Max(9, Mathf.RoundToInt(size * 0.2f))).WithTracking(0.16f), TextAnchor.MiddleCenter, "Settings");
                        view._settings.rectTransform.Centered(0f, -size * 0.42f, w + 60f, size * 0.26f);
                    }
                    break;
                default:
                    view._digit = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(size)).WithWeight(size >= 36f ? 200 : 300), TextAnchor.MiddleCenter, "Digit");
                    view._digit.rectTransform.Centered(0f, 0f, w + 20f, size * 1.2f);
                    break;
            }
            if (medium == Medium.Shape || Media.IsWave(medium))
            {
                view._unknown = UIKit.Text(rt, "?", TextStyle.Value.WithSize(Mathf.RoundToInt(size * 0.8f)).WithWeight(200).WithColor(Palette.Faint), TextAnchor.MiddleCenter, "Unknown");
                view._unknown.rectTransform.Centered(0f, 0f, w, size * 1.2f);
                view._unknown.enabled = false;
            }
            return view;
        }

        /// <summary>
        /// Lets the player click the value to hear it. A small speaker appears
        /// beside it on hover, so it reads as something to listen to.
        /// </summary>
        public Clickable MakePlayable(Action<int> play)
        {
            float w = WidthFor(_medium, _size);
            var hit = UIKit.Hit(transform, "Listen");
            hit.rectTransform.Centered(0f, 0f, w + 14f, _size + 12f);
            float icon = Mathf.Max(11f, _size * 0.3f);
            _speaker = UIKit.Glyph(transform, "sound", Palette.Dim, 1.3f, "Speaker");
            _speaker.rectTransform.Centered(w * 0.5f + icon * 0.4f, _size * 0.5f, icon, icon);
            _speaker.enabled = false;
            var c = hit.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = () =>
            {
                if (!Known) return;
                play(_value);
                Ping();
            };
            c.HoverChanged = on => _speaker.enabled = on;
            return c;
        }

        /// <summary>A short swell and brightening, to show this is the value sounding.</summary>
        public void Ping()
        {
            if (!_pinging) _restScale = transform.localScale;
            _pinging = true;
            bool motion = !App.ReduceMotion;
            Tweener.Run(this, "ping", 0.45f, t =>
            {
                _ping = 1f - t;
                if (motion) transform.localScale = _restScale * (1f + 0.16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.6f)));
                Restyle();
            }, Ease.Linear, 0f, () =>
            {
                _pinging = false;
                _ping = 0f;
                transform.localScale = _restScale;
                Restyle();
            });
        }

        /// <summary>True when the value is a real signal, not the ? of one still unknown.</summary>
        public bool Known => _value >= 0 && Media.IsValid(_medium, _value);

        /// <summary>Shows a value, or -1 for one that isn't known yet.</summary>
        public void Set(int value, ValueLook look)
        {
            if (_styled && value == _value && look == _look) return;
            _styled = true;
            _value = value;
            _look = look;
            Restyle();
        }

        public ValueLook Look
        {
            get => _look;
            set { if (_look != value) { _look = value; Restyle(); } }
        }

        void Restyle()
        {
            bool live = _look == ValueLook.Live;
            Color quiet = Color.Lerp(_look == ValueLook.Met ? Palette.Text : Palette.Dim, Color.white, _ping);
            bool has = Known;
            if (_unknown != null) _unknown.enabled = !has;

            switch (_medium)
            {
                case Medium.Shape:
                {
                    _shape.enabled = has;
                    if (!has) break;
                    int form = Shapes.Form(_value);
                    _shape.Shape = form == 0 ? ShapeKind.Circle : form == 1 ? ShapeKind.Triangle : form == 2 ? ShapeKind.Square : ShapeKind.Hexagon;
                    bool solid = Shapes.Solid(_value);
                    float stroke = Mathf.Max(1.4f, _size * 0.05f);
                    _shape.Inset = form == 0 ? _size * 0.06f : 0f;
                    int colour = Shapes.Colour(_value);
                    if (live)
                    {
                        Paint paint = colour == 0 ? Paint.Signal : Paint.Solid;
                        Color tint = colour == 0 ? Color.white : Palette.ShapeColours[colour];
                        _shape.Style(solid ? paint : Paint.None, tint, paint, tint, stroke);
                        _shape.Glow = (solid ? 0.35f : 0.25f) + 0.4f * _ping;
                        _shape.GlowPaint = paint;
                        _shape.GlowColor = tint;
                        _shape.GlowRadius = _size * 0.35f;
                    }
                    else
                    {
                        Color tint = colour == 0 ? quiet : Color.Lerp(quiet, Palette.ShapeColours[colour], 0.55f);
                        _shape.Style(solid ? Paint.Solid : Paint.None, tint, Paint.Solid, tint, stroke);
                        _shape.Glow = 0f;
                    }
                    break;
                }
                case Medium.Wave:
                case Medium.Sampled:
                {
                    _wave.enabled = has;
                    if (_settings != null) _settings.enabled = has;
                    if (!has) break;
                    bool sampled = _medium == Medium.Sampled;
                    int form = sampled ? Sampled.Form(_value) : Waves.Form(_value);
                    int frequency = sampled ? Sampled.Frequency(_value) : Waves.Frequency(_value);
                    int height = sampled ? Sampled.Height(_value) : Waves.Height(_value);
                    _wave.Form = form == Waves.Sine ? WaveGraphic.Sine : form == Waves.SquareWave ? WaveGraphic.Square : WaveGraphic.Saw;
                    // A small picture, not the whole loop: faster waves pack in more
                    // cycles and taller ones reach further, enough to tell apart, and
                    // the caption gives the exact steps.
                    _wave.Cycles = sampled ? 1f + 0.25f * frequency : 1f + 0.42f * frequency;
                    _wave.Amplitude = 0.25f + 0.75f * height / (Waves.Heights - 1);
                    _wave.Phase = sampled ? 0f : Waves.Phase(_value) * 0.25f;
                    _wave.StepsPerCycle = sampled ? Sampled.StepsPerCycle[Sampled.Rate(_value)] : 0;
                    _wave.Levels = height + 1;
                    _wave.LineWidth = Mathf.Max(1.4f, _size * 0.045f);
                    _wave.Paint = live ? Paint.Signal : Paint.Solid;
                    _wave.Tint = live ? Color.white : quiet;
                    _wave.GlowAmount = live ? 0.3f + 0.4f * _ping : 0.3f * _ping;
                    if (_settings != null)
                    {
                        _settings.text = sampled ? Sampled.Settings(_value) : Waves.Settings(_value);
                        _settings.color = live ? Palette.Dim : quiet;
                    }
                    break;
                }
                default:
                    _digit.text = has ? _value.ToString() : "?";
                    _digit.SignalFill = live && has;
                    _digit.color = !has ? Palette.Faint : live ? Color.white : quiet;
                    break;
            }
        }
    }
}
