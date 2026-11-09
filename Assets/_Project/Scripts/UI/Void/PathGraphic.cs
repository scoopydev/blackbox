using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// A path drawn in two layers: a quiet base line along its whole length, and a
    /// lit stretch (signal gradient plus glow) between two fractions of its length.
    /// Wires use it to light up progressively as a signal travels along them.
    /// Points are in the graphic's local space; give it a stretched rect.
    /// </summary>
    public class PathGraphic : VoidGraphic
    {
        readonly List<Vector2> _points = new List<Vector2>(96);
        readonly List<Vector2> _slice = new List<Vector2>(96);

        float _baseWidth = 1.5f;
        Color _baseColor = Palette.Outline;
        float _litWidth = 2.6f;
        float _litFrom, _litTo;
        float _litAlpha = 1f;
        float _glow = 0.42f;
        float _glowRadius = 13f;
        Paint _litPaint = Paint.Signal;
        Color _litColor = Color.white;
        float _spark = -1f;

        public List<Vector2> Points => _points;

        public void SetPoints(List<Vector2> points)
        {
            _points.Clear();
            _points.AddRange(points);
            SetVerticesDirty();
        }

        public float BaseWidth { get => _baseWidth; set { if (_baseWidth != value) { _baseWidth = value; SetVerticesDirty(); } } }
        public Color BaseColor { get => _baseColor; set { if (_baseColor != value) { _baseColor = value; SetVerticesDirty(); } } }
        public float LitWidth { get => _litWidth; set { if (_litWidth != value) { _litWidth = value; SetVerticesDirty(); } } }
        public float LitAlpha { get => _litAlpha; set { if (_litAlpha != value) { _litAlpha = value; SetVerticesDirty(); } } }
        public float Glow { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }
        public float GlowRadius { get => _glowRadius; set { if (_glowRadius != value) { _glowRadius = value; SetVerticesDirty(); } } }
        public Paint LitPaint { get => _litPaint; set { if (_litPaint != value) { _litPaint = value; SetVerticesDirty(); } } }
        public Color LitColor { get => _litColor; set { if (_litColor != value) { _litColor = value; SetVerticesDirty(); } } }
        /// <summary>A bright point at this fraction of the path (the head of a travelling signal), or negative for none.</summary>
        public float Spark { get => _spark; set { if (_spark != value) { _spark = value; SetVerticesDirty(); } } }

        public void SetLit(float from, float to)
        {
            if (_litFrom == from && _litTo == to) return;
            _litFrom = from;
            _litTo = to;
            SetVerticesDirty();
        }

        public bool IsLit => _litTo > _litFrom && _litAlpha > 0f;
        public bool FullyLit => _litFrom <= 0f && _litTo >= 1f && _litAlpha > 0f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;
            PrepareGradient();
            float feather = Feather;

            if (_baseWidth > 0f && _baseColor.a > 0f)
                MeshKit.Stroke(vh, _points, false, _baseWidth, feather, Colour(Paint.Solid, _baseColor));

            if (_litTo > _litFrom && _litAlpha > 0f)
            {
                _slice.Clear();
                if (_litFrom <= 0f && _litTo >= 1f) _slice.AddRange(_points);
                else MeshKit.Slice(_points, _litFrom, _litTo, _slice);
                if (_slice.Count >= 2)
                {
                    var lit = Colour(_litPaint, _litColor, _litAlpha);
                    MeshKit.Glow(vh, _slice, false, _glowRadius, lit, _glow);
                    MeshKit.Stroke(vh, _slice, false, _litWidth, feather, lit);
                }
            }

            if (_spark >= 0f)
            {
                Vector2 p = MeshKit.PointAt(_points, _spark, out _);
                var c = Palette.SignalAtX(FrameX(p));
                c.a = _litAlpha * color.a;
                MeshKit.SoftDisc(vh, p, 16f, c);
                MeshKit.Disc(vh, p, 2.6f, feather, Color.white.WithAlpha(0.9f * _litAlpha * color.a));
            }
        }
    }

    /// <summary>
    /// The moving specks of light on a live wire: short bright dashes that drift
    /// along the path. Kept separate from the wire so only this cheap mesh is
    /// rebuilt every frame.
    /// </summary>
    public class FlowGraphic : VoidGraphic
    {
        public PathGraphic Source;
        public float Speed = 46f;
        public float Spacing = 30f;
        public float Length = 2.5f;
        public float Opacity = 0.55f;
        public bool Animate = true;

        float _phase;
        readonly List<List<Vector2>> _pieces = new List<List<Vector2>>();

        void Update()
        {
            if (!Animate || Source == null || !Source.FullyLit) return;
            _phase += Time.unscaledDeltaTime * Speed;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Source == null || !Source.FullyLit || Source.Points.Count < 2) return;
            _pieces.Clear();
            // Negative phase moves the dashes forwards along the path.
            MeshKit.Dashes(Source.Points, false, Length, Spacing - Length, -_phase, _pieces);
            var c = Color.white.WithAlpha(Opacity * Source.LitAlpha * color.a);
            float feather = Feather;
            foreach (var piece in _pieces) MeshKit.Stroke(vh, piece, false, Source.LitWidth, feather, _ => c);
        }
    }
}
