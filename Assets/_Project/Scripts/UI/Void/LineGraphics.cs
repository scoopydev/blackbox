using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// A few cycles of a sine, square or saw wave drawn across its rect. Wave
    /// signals use it: the form is the shape of the line, and a higher pitch packs
    /// in more cycles.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaveGraphic : VoidGraphic
    {
        public const int Sine = 0, Square = 1, Saw = 2;

        int _form;
        float _cycles = 2f;
        float _width = 2f;
        float _amplitude = 0.78f;
        Paint _paint = Paint.Signal;
        Color _tint = Color.white;
        float _glow;

        public int Form { get => _form; set { if (_form != value) { _form = value; SetVerticesDirty(); } } }
        public float Cycles { get => _cycles; set { if (_cycles != value) { _cycles = value; SetVerticesDirty(); } } }
        public float LineWidth { get => _width; set { if (_width != value) { _width = value; SetVerticesDirty(); } } }
        /// <summary>Peak height as a fraction of half the rect's height.</summary>
        public float Amplitude { get => _amplitude; set { if (_amplitude != value) { _amplitude = value; SetVerticesDirty(); } } }
        public Paint Paint { get => _paint; set { if (_paint != value) { _paint = value; SetVerticesDirty(); } } }
        public Color Tint { get => _tint; set { if (_tint != value) { _tint = value; SetVerticesDirty(); } } }
        public float GlowAmount { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }

        readonly List<Vector2> _pts = new List<Vector2>(200);

        void Build()
        {
            _pts.Clear();
            Rect r = rectTransform.rect;
            float amp = r.height * 0.5f * _amplitude;
            float cy = r.center.y;
            Vector2 P(float u, float y) => new Vector2(r.xMin + u * r.width, cy + y * amp);
            float c = Mathf.Max(0.25f, _cycles);

            switch (_form)
            {
                case Square:
                {
                    // High for the first half of each cycle, low for the second, with sheer edges.
                    float y = 1f;
                    _pts.Add(P(0f, y));
                    for (int k = 1; ; k++)
                    {
                        float u = k / (2f * c);
                        if (u >= 1f) break;
                        _pts.Add(P(u, y));
                        y = -y;
                        _pts.Add(P(u, y));
                    }
                    _pts.Add(P(1f, y));
                    break;
                }
                case Saw:
                {
                    // Rises from the middle, drops sheer at each peak.
                    float Phase(float u) => c * u + 0.5f;
                    _pts.Add(P(0f, 0f));
                    for (int k = 1; ; k++)
                    {
                        float u = (k - 0.5f) / c;
                        if (u >= 1f) break;
                        _pts.Add(P(u, 1f));
                        _pts.Add(P(u, -1f));
                    }
                    float end = Phase(1f);
                    _pts.Add(P(1f, 2f * (end - Mathf.Floor(end)) - 1f));
                    break;
                }
                default:
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(c * 28f), 24, 160);
                    for (int i = 0; i <= n; i++)
                    {
                        float u = (float)i / n;
                        _pts.Add(P(u, Mathf.Sin(u * c * Mathf.PI * 2f)));
                    }
                    break;
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            PrepareGradient();
            Build();
            if (_pts.Count < 2) return;
            var colour = Colour(_paint, _tint);
            if (_glow > 0f) MeshKit.Glow(vh, _pts, false, _width * 5f, colour, _glow);
            MeshKit.Stroke(vh, _pts, false, _width, Feather, colour);
        }
    }

    /// <summary>
    /// One stroke of the pen: a polyline in one flat colour. Points are in the
    /// graphic's local space; give it a stretched rect.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkGraphic : VoidGraphic
    {
        readonly List<Vector2> _points = new List<Vector2>(128);
        float _width = 3f;
        Color _ink = Color.white;

        public List<Vector2> Points => _points;
        public float LineWidth { get => _width; set { if (_width != value) { _width = value; SetVerticesDirty(); } } }
        public Color Ink { get => _ink; set { if (_ink != value) { _ink = value; SetVerticesDirty(); } } }

        public void Add(Vector2 p)
        {
            _points.Add(p);
            SetVerticesDirty();
        }

        /// <summary>The shortest distance from <paramref name="p"/> to the stroke.</summary>
        public float DistanceTo(Vector2 p)
        {
            if (_points.Count == 0) return float.MaxValue;
            if (_points.Count == 1) return Vector2.Distance(p, _points[0]);
            float best = float.MaxValue;
            for (int i = 1; i < _points.Count; i++) best = Mathf.Min(best, SegmentDistance(p, _points[i - 1], _points[i]));
            return best;
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count == 0) return;
            float feather = Feather;
            var c = new Color(_ink.r, _ink.g, _ink.b, _ink.a * color.a);
            if (_points.Count == 1)
            {
                MeshKit.Disc(vh, _points[0], _width * 0.5f, feather, c);
                return;
            }
            MeshKit.Stroke(vh, _points, false, _width, feather, _ => c);
            // Round the ends so short marks and dots read as pen strokes.
            MeshKit.Disc(vh, _points[0], _width * 0.5f, feather, c);
            MeshKit.Disc(vh, _points[_points.Count - 1], _width * 0.5f, feather, c);
        }
    }
}
