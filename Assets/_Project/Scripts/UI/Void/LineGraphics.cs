using System;
using System.Collections.Generic;
using Blackbox.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// A few cycles of a sine, square or saw wave drawn across its rect. Wave
    /// signals use it: the form is the shape of the line, a higher frequency packs
    /// in more cycles, a taller wave reaches further from the middle, and a later
    /// phase slides it to the right. Give it <see cref="StepsPerCycle"/> to draw a
    /// sampled wave: the line is held flat in steps, each rounded to one of
    /// <see cref="Levels"/> levels either side of the middle.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaveGraphic : VoidGraphic
    {
        public const int Sine = 0, Square = 1, Saw = 2;

        int _form;
        float _cycles = 2f;
        float _width = 2f;
        float _amplitude = 0.78f;
        float _phase;
        int _steps;
        int _levels = 7;
        Paint _paint = Paint.Signal;
        Color _tint = Color.white;
        float _glow;

        public int Form { get => _form; set { if (_form != value) { _form = value; SetVerticesDirty(); } } }
        public float Cycles { get => _cycles; set { if (_cycles != value) { _cycles = value; SetVerticesDirty(); } } }
        public float LineWidth { get => _width; set { if (_width != value) { _width = value; SetVerticesDirty(); } } }
        /// <summary>Peak height as a fraction of half the rect's height.</summary>
        public float Amplitude { get => _amplitude; set { if (_amplitude != value) { _amplitude = value; SetVerticesDirty(); } } }
        /// <summary>How late the wave starts, in cycles (0.25 is a quarter cycle).</summary>
        public float Phase { get => _phase; set { if (_phase != value) { _phase = value; SetVerticesDirty(); } } }
        /// <summary>Steps a cycle for a sampled wave; 0 draws a smooth analog wave.</summary>
        public int StepsPerCycle { get => _steps; set { if (_steps != value) { _steps = value; SetVerticesDirty(); } } }
        /// <summary>Whole levels between the middle and the peak that a sampled wave's steps round to.</summary>
        public int Levels { get => _levels; set { if (_levels != value) { _levels = value; SetVerticesDirty(); } } }
        public Paint Paint { get => _paint; set { if (_paint != value) { _paint = value; SetVerticesDirty(); } } }
        public Color Tint { get => _tint; set { if (_tint != value) { _tint = value; SetVerticesDirty(); } } }
        public float GlowAmount { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }

        readonly List<Vector2> _pts = new List<Vector2>(200);
        readonly List<float> _breaks = new List<float>(64);

        void Build()
        {
            _pts.Clear();
            Rect r = rectTransform.rect;
            float amp = r.height * 0.5f * _amplitude;
            float cy = r.center.y;
            Vector2 P(float u, float y) => new Vector2(r.xMin + u * r.width, cy + y * amp);
            float c = Mathf.Max(0.25f, _cycles);
            // Cycles into the form at a point u (0 to 1) across the rect.
            double At(float u) => c * u - _phase;

            if (_steps > 0)
            {
                // Sampled: flat steps, with sheer rises and falls between them.
                int levels = Mathf.Max(1, _levels);
                double first = Math.Floor(At(0f) * _steps);
                for (double k = first; ; k++)
                {
                    float u0 = Mathf.Max(0f, (float)((k / _steps + _phase) / c));
                    float u1 = Mathf.Min(1f, (float)(((k + 1) / _steps + _phase) / c));
                    if (u0 >= 1f) break;
                    float y = Sampled.Level(_form, levels - 1, _steps, (k + 0.5) / _steps) / (float)levels;
                    _pts.Add(P(u0, y));
                    _pts.Add(P(u1, y));
                }
                return;
            }

            if (_form == Sine)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(c * 28f), 24, 220);
                for (int i = 0; i <= n; i++)
                {
                    float u = (float)i / n;
                    _pts.Add(P(u, (float)Waves.Shape(Waves.Sine, At(u))));
                }
                return;
            }

            // Square and saw are straight between their sheer edges: square edges
            // fall at every half cycle, a saw's drop at the end of each cycle.
            _breaks.Clear();
            double step = _form == Square ? 0.5 : 1.0;
            for (double e = Math.Ceiling(At(0f) / step) * step; ; e += step)
            {
                float u = (float)((e + _phase) / c);
                if (u >= 1f) break;
                if (u > 0f) _breaks.Add(u);
            }
            int form = _form == Square ? Waves.SquareWave : Waves.Saw;
            const float eps = 1e-4f;
            _pts.Add(P(0f, (float)Waves.Shape(form, At(0f))));
            foreach (float u in _breaks)
            {
                _pts.Add(P(u, (float)Waves.Shape(form, At(u) - eps)));
                _pts.Add(P(u, (float)Waves.Shape(form, At(u) + eps)));
            }
            _pts.Add(P(1f, (float)Waves.Shape(form, At(1f) - eps)));
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
    /// A signal over one whole loop, drawn left to right across its rect between
    /// <see cref="Min"/> (bottom) and <see cref="Max"/> (top). Stepped traces
    /// (bits, digits, shapes read as numbers, sampled waves) hold flat and jump;
    /// analog waves are drawn smoothly. A playhead can mark where in the loop
    /// the signal is now.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TraceGraphic : VoidGraphic
    {
        float[] _levels;
        float _min = -1f, _max = 1f;
        bool _stepped;
        float _width = 1.8f;
        Paint _paint = Paint.Signal;
        Color _tint = Color.white;
        float _glow;
        float _playhead = -1f;
        float _reveal = 1f;
        bool _midline;

        public float Min { get => _min; set { if (_min != value) { _min = value; SetVerticesDirty(); } } }
        public float Max { get => _max; set { if (_max != value) { _max = value; SetVerticesDirty(); } } }
        public bool Stepped { get => _stepped; set { if (_stepped != value) { _stepped = value; SetVerticesDirty(); } } }
        public float LineWidth { get => _width; set { if (_width != value) { _width = value; SetVerticesDirty(); } } }
        public Paint Paint { get => _paint; set { if (_paint != value) { _paint = value; SetVerticesDirty(); } } }
        public Color Tint { get => _tint; set { if (_tint != value) { _tint = value; SetVerticesDirty(); } } }
        public float GlowAmount { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }
        /// <summary>Where in the loop the signal is now (0 to 1), or negative for no playhead.</summary>
        public float Playhead { get => _playhead; set { if (_playhead != value) { _playhead = value; SetVerticesDirty(); } } }
        /// <summary>How much of the loop is drawn, from the left (1 is all of it).</summary>
        public float Reveal { get => _reveal; set { if (_reveal != value) { _reveal = value; SetVerticesDirty(); } } }
        /// <summary>A faint line through the middle, for waves.</summary>
        public bool Midline { get => _midline; set { if (_midline != value) { _midline = value; SetVerticesDirty(); } } }

        public void SetLevels(float[] levels)
        {
            _levels = levels;
            SetVerticesDirty();
        }

        readonly List<Vector2> _pts = new List<Vector2>(320);

        Vector2 At(Rect r, float u, float level)
        {
            float span = Mathf.Max(1e-4f, _max - _min);
            float v = Mathf.Clamp01((level - _min) / span);
            float pad = _width;
            return new Vector2(r.xMin + u * r.width, r.yMin + pad + v * (r.height - 2f * pad));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_levels == null || _levels.Length < 2) return;
            PrepareGradient();
            Rect r = rectTransform.rect;
            int n = _levels.Length;
            float reveal = Mathf.Clamp01(_reveal);
            var colour = Colour(_paint, _tint);
            float feather = Feather;

            if (_midline)
            {
                _pts.Clear();
                float mid = (_min + _max) * 0.5f;
                _pts.Add(At(r, 0f, mid));
                _pts.Add(At(r, 1f, mid));
                MeshKit.Stroke(vh, _pts, false, 1f, feather, _ => Palette.Outline.WithAlpha(color.a));
            }

            _pts.Clear();
            if (_stepped)
            {
                int start = 0;
                for (int k = 1; k <= n; k++)
                {
                    if (k < n && Mathf.Abs(_levels[k] - _levels[start]) <= 1e-3f) continue;
                    float u0 = (float)start / n, u1 = (float)k / n;
                    if (u0 >= reveal) break;
                    u1 = Mathf.Min(u1, reveal);
                    _pts.Add(At(r, u0, _levels[start]));
                    _pts.Add(At(r, u1, _levels[start]));
                    start = k;
                }
            }
            else
            {
                int last = Mathf.Max(1, Mathf.CeilToInt(reveal * n));
                for (int k = 0; k <= last && k <= n; k++)
                {
                    float level = _levels[k % n];
                    // The last point closes the loop at the right edge with the first level only if the wave runs on smoothly.
                    if (k == n) level = _levels[n - 1] + (_levels[n - 1] - _levels[n - 2]);
                    _pts.Add(At(r, Mathf.Min((float)k / n, reveal), level));
                }
            }
            if (_pts.Count >= 2)
            {
                if (_glow > 0f) MeshKit.Glow(vh, _pts, false, _width * 5f, colour, _glow);
                MeshKit.Stroke(vh, _pts, false, _width, feather, colour);
            }

            if (_playhead >= 0f)
            {
                float u = Mathf.Repeat(_playhead, 1f);
                int k = Mathf.Clamp((int)(u * n), 0, n - 1);
                Vector2 p = At(r, u, _levels[k]);
                MeshKit.SoftDisc(vh, p, 10f, Palette.SignalAtX(FrameX(p)).WithAlpha(0.7f * color.a));
                MeshKit.Disc(vh, p, 2.4f, feather, Color.white.WithAlpha(color.a));
            }
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
