using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// Line-drawn symbols. Sora has no logic symbols or arrows, and drawing them
    /// with the same strokes as the wires keeps them crisp at any size. Each glyph
    /// is a set of strokes in a box from -1 to 1.
    /// </summary>
    public static class Glyphs
    {
        public sealed class Glyph
        {
            public readonly List<Vector2[]> Open = new List<Vector2[]>();
            public readonly List<Vector2[]> Closed = new List<Vector2[]>();
        }

        static readonly Dictionary<string, Glyph> s_Glyphs = new Dictionary<string, Glyph>();

        static Glyphs()
        {
            Add("pass", Line(-0.7f, 0.28f, 0.7f, 0.28f), Line(-0.7f, -0.28f, 0.7f, -0.28f));
            Add("not", Poly(-0.7f, 0.3f, 0.65f, 0.3f, 0.65f, -0.35f));
            Add("one", Poly(-0.32f, 0.5f, 0.08f, 0.85f, 0.08f, -0.85f));
            AddClosed("zero", Ellipse(0f, 0f, 0.5f, 0.82f));
            Add("and", Poly(-0.7f, -0.75f, 0f, 0.75f, 0.7f, -0.75f));
            Add("or", Poly(-0.7f, 0.75f, 0f, -0.75f, 0.7f, 0.75f));
            AddClosed("xor", Ellipse(0f, 0f, 0.78f, 0.78f));
            Add("xor", Line(-0.78f, 0f, 0.78f, 0f), Line(0f, -0.78f, 0f, 0.78f));
            Add("xnor", Line(-0.7f, 0.45f, 0.7f, 0.45f), Line(-0.7f, 0f, 0.7f, 0f), Line(-0.7f, -0.45f, 0.7f, -0.45f));
            Add("nand", Poly(-0.62f, -0.8f, 0f, 0.42f, 0.62f, -0.8f), Line(-0.7f, 0.8f, 0.7f, 0.8f));
            Add("nor", Poly(-0.62f, 0.42f, 0f, -0.8f, 0.62f, 0.42f), Line(-0.7f, 0.8f, 0.7f, 0.8f));
            Add("gt", Poly(-0.5f, 0.72f, 0.55f, 0f, -0.5f, -0.72f));
            Add("lt", Poly(0.5f, 0.72f, -0.55f, 0f, 0.5f, -0.72f));
            AddClosed("delay", new[] { V(-0.6f, 0.8f), V(0.6f, 0.8f), V(-0.6f, -0.8f), V(0.6f, -0.8f) });
            Add("rise", Poly(-0.85f, -0.55f, 0f, -0.55f, 0f, 0.55f, 0.85f, 0.55f));
            Add("fall", Poly(-0.85f, 0.55f, 0f, 0.55f, 0f, -0.55f, 0.85f, -0.55f));
            Add("change", Poly(-0.9f, -0.55f, -0.3f, -0.55f, -0.3f, 0.55f, 0.3f, 0.55f, 0.3f, -0.55f, 0.9f, -0.55f));
            Add("toggle", Arc(0f, 0f, 0.7f, 40f, 320f), Poly(0.2f, -0.56f, 0.54f, -0.45f, 0.48f, -0.8f));
            Add("hold", Poly(-0.8f, -0.6f, -0.8f, 0.5f, 0.8f, 0.5f), Line(-0.8f, -0.6f, 0.8f, -0.6f));

            // Interface marks.
            Add("arrow", Line(-0.8f, 0f, 0.8f, 0f), Poly(0.35f, 0.42f, 0.8f, 0f, 0.35f, -0.42f));
            Add("back", Line(0.8f, 0f, -0.8f, 0f), Poly(-0.35f, 0.42f, -0.8f, 0f, -0.35f, -0.42f));
            Add("lock", Arc(0f, 0.15f, 0.42f, 0f, 180f), Line(-0.42f, 0.15f, -0.42f, -0.05f), Line(0.42f, 0.15f, 0.42f, -0.05f));
            AddClosed("lock", new[] { V(-0.62f, -0.05f), V(0.62f, -0.05f), V(0.62f, -0.85f), V(-0.62f, -0.85f) });
            Add("cross", Line(-0.6f, -0.6f, 0.6f, 0.6f), Line(-0.6f, 0.6f, 0.6f, -0.6f));
            Add("probe", Line(-0.9f, 0f, -0.35f, 0f), Line(0.35f, 0f, 0.9f, 0f), Line(0f, -0.9f, 0f, -0.35f), Line(0f, 0.35f, 0f, 0.9f));
            AddClosed("probe", Ellipse(0f, 0f, 0.55f, 0.55f));
            Add("check", Poly(-0.72f, 0.02f, -0.22f, -0.5f, 0.74f, 0.58f));
            Add("equals", Line(-0.66f, 0.26f, 0.66f, 0.26f), Line(-0.66f, -0.26f, 0.66f, -0.26f));
            Add("notequal", Line(-0.66f, 0.26f, 0.66f, 0.26f), Line(-0.66f, -0.26f, 0.66f, -0.26f), Line(0.32f, 0.78f, -0.32f, -0.78f));
            Add("up", Poly(-0.7f, -0.3f, 0f, 0.4f, 0.7f, -0.3f));
            Add("down", Poly(-0.7f, 0.3f, 0f, -0.4f, 0.7f, 0.3f));
            AddClosed("pen", new[] { V(-0.52f, -0.26f), V(0.46f, 0.72f), V(0.74f, 0.44f), V(-0.24f, -0.54f) });
            Add("pen", Poly(-0.52f, -0.26f, -0.82f, -0.84f, -0.24f, -0.54f), Line(0.3f, 0.56f, 0.58f, 0.28f));
            AddClosed("eraser", new[] { V(-0.78f, -0.12f), V(0.02f, 0.68f), V(0.72f, -0.02f), V(-0.08f, -0.82f) });
            Add("eraser", Line(-0.38f, 0.28f, 0.32f, -0.42f), Line(-0.9f, -0.92f, 0.9f, -0.92f));
            Add("undo", Arc(0.08f, -0.18f, 0.62f, 180f, 0f), Poly(-0.82f, 0.08f, -0.54f, -0.2f, -0.24f, 0.08f));
            Add("reset", Arc(0f, 0f, 0.66f, 70f, 380f), Poly(0.04f, 0.92f, 0.24f, 0.62f, -0.08f, 0.44f));
            AddClosed("sound", new[] { V(-0.85f, 0.28f), V(-0.45f, 0.28f), V(-0.02f, 0.68f), V(-0.02f, -0.68f), V(-0.45f, -0.28f), V(-0.85f, -0.28f) });
            Add("sound", Arc(0.08f, 0f, 0.36f, -50f, 50f, 12), Arc(0.08f, 0f, 0.7f, -50f, 50f, 16));
        }

        public static bool TryGet(string id, out Glyph glyph) => s_Glyphs.TryGetValue(id ?? string.Empty, out glyph);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Vector2[] Line(float x0, float y0, float x1, float y1) => new[] { V(x0, y0), V(x1, y1) };

        static Vector2[] Poly(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = V(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int segments = 40)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                pts[i] = V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return pts;
        }

        static Vector2[] Arc(float cx, float cy, float r, float fromDeg, float toDeg, int segments = 28)
        {
            var pts = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / segments) * Mathf.Deg2Rad;
                pts[i] = V(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Glyph Get(string id)
        {
            if (!s_Glyphs.TryGetValue(id, out var g)) s_Glyphs[id] = g = new Glyph();
            return g;
        }

        static void Add(string id, params Vector2[][] strokes) => Get(id).Open.AddRange(strokes);
        static void AddClosed(string id, params Vector2[][] strokes) => Get(id).Closed.AddRange(strokes);
    }

    /// <summary>Draws one glyph to fit its rect.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GlyphGraphic : VoidGraphic
    {
        string _glyph;
        float _width = 2f;
        Paint _paint = Paint.Solid;
        Color _tint = Palette.Text;
        float _glow;

        public string Glyph { get => _glyph; set { if (_glyph != value) { _glyph = value; SetVerticesDirty(); } } }
        public float LineWidth { get => _width; set { if (_width != value) { _width = value; SetVerticesDirty(); } } }
        public Paint Paint { get => _paint; set { if (_paint != value) { _paint = value; SetVerticesDirty(); } } }
        public Color Tint { get => _tint; set { if (_tint != value) { _tint = value; SetVerticesDirty(); } } }
        public float GlowAmount { get => _glow; set { if (_glow != value) { _glow = value; SetVerticesDirty(); } } }

        readonly List<Vector2> _pts = new List<Vector2>(64);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Glyphs.TryGet(_glyph, out var glyph)) return;
            PrepareGradient();
            Rect r = rectTransform.rect;
            float half = Mathf.Min(r.width, r.height) * 0.5f;
            Vector2 c = r.center;
            var colour = Colour(_paint, _tint);
            float feather = Feather;

            void Draw(Vector2[] stroke, bool closed)
            {
                _pts.Clear();
                foreach (var p in stroke) _pts.Add(c + p * half);
                if (_glow > 0f) MeshKit.Glow(vh, _pts, closed, _width * 5f, colour, _glow);
                MeshKit.Stroke(vh, _pts, closed, _width, feather, colour);
            }

            foreach (var s in glyph.Open) Draw(s, false);
            foreach (var s in glyph.Closed) Draw(s, true);
        }
    }
}
