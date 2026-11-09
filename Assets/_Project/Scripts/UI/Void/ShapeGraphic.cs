using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    public enum ShapeKind { RoundedRect, Circle, Pill }

    /// <summary>
    /// A rounded rectangle, circle or pill that fills its RectTransform: optional
    /// fill, outline (solid, dashed or signal gradient) and glow. Boxes, ports,
    /// buttons and the circuit-select tiles are all one of these.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ShapeGraphic : VoidGraphic
    {
        ShapeKind _shape = ShapeKind.RoundedRect;
        float _radius = 12f;
        Paint _fill = Paint.None;
        Color _fillColor = Color.black;
        Paint _stroke = Paint.Solid;
        Color _strokeColor = Palette.Outline;
        float _strokeWidth = 1.5f;
        bool _dashed;
        float _dash = 5f, _gap = 6f;
        float _glow;
        float _glowRadius = 22f;
        Paint _glowPaint = Paint.Signal;
        Color _glowColor = Color.white;
        float _inset;

        public ShapeKind Shape { get => _shape; set { if (_shape != value) { _shape = value; SetVerticesDirty(); } } }
        public float Radius { get => _radius; set { if (_radius != value) { _radius = value; SetVerticesDirty(); } } }
        public Paint FillPaint { get => _fill; set { if (_fill != value) { _fill = value; SetVerticesDirty(); } } }
        public Color FillColor { get => _fillColor; set { if (_fillColor != value) { _fillColor = value; SetVerticesDirty(); } } }
        public Paint StrokePaint { get => _stroke; set { if (_stroke != value) { _stroke = value; SetVerticesDirty(); } } }
        public Color StrokeColor { get => _strokeColor; set { if (_strokeColor != value) { _strokeColor = value; SetVerticesDirty(); } } }
        public float StrokeWidth { get => _strokeWidth; set { if (_strokeWidth != value) { _strokeWidth = value; SetVerticesDirty(); } } }
        public bool Dashed { get => _dashed; set { if (_dashed != value) { _dashed = value; SetVerticesDirty(); } } }
        public float Glow { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }
        public float GlowRadius { get => _glowRadius; set { if (_glowRadius != value) { _glowRadius = value; SetVerticesDirty(); } } }
        public Paint GlowPaint { get => _glowPaint; set { if (_glowPaint != value) { _glowPaint = value; SetVerticesDirty(); } } }
        public Color GlowColor { get => _glowColor; set { if (_glowColor != value) { _glowColor = value; SetVerticesDirty(); } } }
        /// <summary>Shrinks (positive) or grows (negative) the shape inside its rect.</summary>
        public float Inset { get => _inset; set { if (_inset != value) { _inset = value; SetVerticesDirty(); } } }

        public void SetDash(float dash, float gap)
        {
            _dash = dash;
            _gap = gap;
            SetVerticesDirty();
        }

        /// <summary>Common setup in one call.</summary>
        public ShapeGraphic Style(Paint fill, Color fillColor, Paint stroke, Color strokeColor, float strokeWidth = 1.5f)
        {
            _fill = fill;
            _fillColor = fillColor;
            _stroke = stroke;
            _strokeColor = strokeColor;
            _strokeWidth = strokeWidth;
            SetVerticesDirty();
            return this;
        }

        readonly List<Vector2> _path = new List<Vector2>(96);
        readonly List<List<Vector2>> _dashes = new List<List<Vector2>>();

        void BuildPath()
        {
            _path.Clear();
            Rect r = rectTransform.rect;
            if (_inset != 0f) r = new Rect(r.x + _inset, r.y + _inset, r.width - 2f * _inset, r.height - 2f * _inset);
            switch (_shape)
            {
                case ShapeKind.Circle:
                    MeshKit.Circle(_path, r.center, Mathf.Min(r.width, r.height) * 0.5f);
                    break;
                case ShapeKind.Pill:
                    MeshKit.RoundedRect(_path, r, Mathf.Min(r.width, r.height) * 0.5f, 16);
                    break;
                default:
                    MeshKit.RoundedRect(_path, r, _radius);
                    break;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            PrepareGradient();
            BuildPath();
            float feather = Feather;

            if (_glow > 0f && _glowPaint != Paint.None)
                MeshKit.Halo(vh, _path, _glowRadius, Colour(_glowPaint, _glowColor), _glow);

            if (_fill != Paint.None)
                MeshKit.Fill(vh, _path, feather, Colour(_fill, _fillColor));

            if (_stroke != Paint.None && _strokeWidth > 0f)
            {
                var stroke = Colour(_stroke, _strokeColor);
                if (_dashed)
                {
                    _dashes.Clear();
                    MeshKit.Dashes(_path, true, _dash, _gap, 0f, _dashes);
                    foreach (var piece in _dashes) MeshKit.Stroke(vh, piece, false, _strokeWidth, feather, stroke);
                }
                else
                {
                    MeshKit.Stroke(vh, _path, true, _strokeWidth, feather, stroke);
                }
            }
        }
    }
}
