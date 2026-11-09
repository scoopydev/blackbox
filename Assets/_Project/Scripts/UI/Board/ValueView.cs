using Blackbox.Components;
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
    /// few cycles of the waveform (wider than tall) for waves.
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
        VoidText _pitch;

        public int Value => _value;
        public Medium Medium => _medium;

        /// <summary>The width the value takes up, for laying out cards around it.</summary>
        public static float WidthFor(Medium m, float size) => m == Medium.Wave ? size * 1.7f : size;

        /// <param name="caption">For waves: write the pitch under the line.</param>
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
                    view._wave = UIKit.Add<WaveGraphic>(rt, "Wave");
                    view._wave.raycastTarget = false;
                    view._wave.rectTransform.Centered(0f, caption ? size * 0.12f : 0f, w, caption ? size * 0.7f : size * 0.86f);
                    if (caption)
                    {
                        view._pitch = UIKit.Text(rt, "", TextStyle.Label.WithSize(Mathf.Max(9, Mathf.RoundToInt(size * 0.2f))).WithTracking(0.24f), TextAnchor.MiddleCenter, "Pitch");
                        view._pitch.rectTransform.Centered(0f, -size * 0.42f, w + 40f, size * 0.26f);
                    }
                    break;
                default:
                    view._digit = UIKit.Text(rt, "", TextStyle.Value.WithSize(Mathf.RoundToInt(size)).WithWeight(size >= 36f ? 200 : 300), TextAnchor.MiddleCenter, "Digit");
                    view._digit.rectTransform.Centered(0f, 0f, w + 20f, size * 1.2f);
                    break;
            }
            return view;
        }

        public void Set(int value, ValueLook look)
        {
            if (value == _value && look == _look) return;
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
            Color quiet = _look == ValueLook.Met ? Palette.Text : Palette.Dim;
            bool has = _value >= 0 && Media.IsValid(_medium, _value);

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
                    if (live)
                    {
                        _shape.Style(solid ? Paint.Signal : Paint.None, Color.white, Paint.Signal, Color.white, stroke);
                        _shape.Glow = solid ? 0.35f : 0.25f;
                        _shape.GlowPaint = Paint.Signal;
                        _shape.GlowRadius = _size * 0.35f;
                    }
                    else
                    {
                        _shape.Style(solid ? Paint.Solid : Paint.None, quiet, Paint.Solid, quiet, stroke);
                        _shape.Glow = 0f;
                    }
                    break;
                }
                case Medium.Wave:
                {
                    _wave.enabled = has;
                    if (_pitch != null) _pitch.enabled = has;
                    if (!has) break;
                    int form = Waves.Form(_value);
                    _wave.Form = form == Waves.Sine ? WaveGraphic.Sine : form == Waves.SquareWave ? WaveGraphic.Square : WaveGraphic.Saw;
                    _wave.Cycles = 1.5f + Waves.Pitch(_value);
                    _wave.LineWidth = Mathf.Max(1.4f, _size * 0.045f);
                    _wave.Paint = live ? Paint.Signal : Paint.Solid;
                    _wave.Tint = live ? Color.white : quiet;
                    _wave.GlowAmount = live ? 0.3f : 0f;
                    if (_pitch != null)
                    {
                        _pitch.text = Waves.PitchNames[Waves.Pitch(_value)];
                        _pitch.color = live ? Palette.Dim : quiet;
                    }
                    break;
                }
                default:
                    _digit.text = has ? _value.ToString() : "·";
                    _digit.SignalFill = live && has;
                    _digit.color = live ? Color.white : has ? quiet : Palette.Faint;
                    break;
            }
        }
    }
}
