using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>Colour for a vertex at a local position. Lets strokes follow the signal gradient.</summary>
    public delegate Color ColorAt(Vector2 localPosition);

    /// <summary>
    /// Geometry for the Void look, written straight into uGUI meshes. Every edge is
    /// anti-aliased with a one-pixel alpha ramp and glows are soft bands of vertex
    /// alpha, so the whole game renders with the default UI shader in any render
    /// pipeline, with no textures, post-processing or extra assets.
    /// </summary>
    public static class MeshKit
    {
        // ------------------------------------------------------------------ paths

        public static void Circle(List<Vector2> into, Vector2 center, float radius, int segments = 0)
        {
            if (segments <= 0) segments = Mathf.Clamp(Mathf.RoundToInt(radius * 0.9f), 20, 96);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                into.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>A rounded rectangle, counter-clockwise, starting at the top of the right edge.</summary>
        public static void RoundedRect(List<Vector2> into, Rect r, float radius, int cornerSegments = 0)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            if (cornerSegments <= 0) cornerSegments = Mathf.Clamp(Mathf.RoundToInt(radius * 0.5f), 3, 16);
            if (radius < 0.5f)
            {
                into.Add(new Vector2(r.xMax, r.yMax));
                into.Add(new Vector2(r.xMin, r.yMax));
                into.Add(new Vector2(r.xMin, r.yMin));
                into.Add(new Vector2(r.xMax, r.yMin));
                return;
            }
            Corner(into, new Vector2(r.xMax - radius, r.yMax - radius), radius, 0f, cornerSegments);
            Corner(into, new Vector2(r.xMin + radius, r.yMax - radius), radius, 90f, cornerSegments);
            Corner(into, new Vector2(r.xMin + radius, r.yMin + radius), radius, 180f, cornerSegments);
            Corner(into, new Vector2(r.xMax - radius, r.yMin + radius), radius, 270f, cornerSegments);
        }

        static void Corner(List<Vector2> into, Vector2 c, float radius, float startDeg, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float a = (startDeg + 90f * i / segments) * Mathf.Deg2Rad;
                into.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        public static void CubicBezier(List<Vector2> into, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments, u = 1f - t;
                into.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
        }

        /// <summary>The wire curve from the concept: leaves and arrives horizontally.</summary>
        public static void WireCurve(List<Vector2> into, Vector2 from, Vector2 to, int segments = 0)
        {
            float k = Mathf.Max(30f, Mathf.Abs(to.x - from.x) * 0.55f);
            if (segments <= 0) segments = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(from, to) / 9f), 12, 64);
            CubicBezier(into, from, from + new Vector2(k, 0), to - new Vector2(k, 0), to, segments);
        }

        /// <summary>A path through corner points with each corner rounded off.</summary>
        public static void RoundedPolyline(List<Vector2> into, IList<Vector2> corners, float radius)
        {
            if (corners.Count < 2) return;
            into.Add(corners[0]);
            for (int i = 1; i < corners.Count - 1; i++)
            {
                Vector2 p = corners[i];
                Vector2 a = corners[i - 1], b = corners[i + 1];
                float r = Mathf.Min(radius, Vector2.Distance(a, p) * 0.5f, Vector2.Distance(p, b) * 0.5f);
                Vector2 p0 = p + (a - p).normalized * r;
                Vector2 p1 = p + (b - p).normalized * r;
                const int segments = 8;
                for (int s = 0; s <= segments; s++)
                {
                    float t = (float)s / segments, u = 1f - t;
                    into.Add(u * u * p0 + 2f * u * t * p + t * t * p1); // quadratic through the corner
                }
            }
            into.Add(corners[corners.Count - 1]);
        }

        public static float Length(List<Vector2> pts, bool closed = false)
        {
            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
            if (closed && pts.Count > 1) len += Vector2.Distance(pts[pts.Count - 1], pts[0]);
            return len;
        }

        /// <summary>The point a fraction <paramref name="t"/> of the way along an open path.</summary>
        public static Vector2 PointAt(List<Vector2> pts, float t, out Vector2 direction)
        {
            direction = Vector2.right;
            if (pts.Count == 0) return Vector2.zero;
            if (pts.Count == 1) return pts[0];
            float target = Mathf.Clamp01(t) * Length(pts);
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (seg <= 1e-5f) continue;
                if (target <= seg || i == pts.Count - 1)
                {
                    direction = (pts[i] - pts[i - 1]) / seg;
                    return Vector2.Lerp(pts[i - 1], pts[i], Mathf.Clamp01(target / seg));
                }
                target -= seg;
            }
            return pts[pts.Count - 1];
        }

        /// <summary>Copies the part of an open path between two fractions of its length.</summary>
        public static void Slice(List<Vector2> src, float from, float to, List<Vector2> into)
        {
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);
            if (to <= from || src.Count < 2) return;
            float total = Length(src);
            float a = from * total, b = to * total, walked = 0f;
            for (int i = 1; i < src.Count; i++)
            {
                Vector2 p0 = src[i - 1], p1 = src[i];
                float seg = Vector2.Distance(p0, p1);
                float s0 = walked, s1 = walked + seg;
                walked = s1;
                if (s1 < a || seg <= 1e-5f) continue;
                if (s0 > b) break;
                if (into.Count == 0) into.Add(Vector2.Lerp(p0, p1, Mathf.Clamp01((a - s0) / seg)));
                if (s1 <= b) into.Add(p1);
                else { into.Add(Vector2.Lerp(p0, p1, Mathf.Clamp01((b - s0) / seg))); break; }
            }
        }

        /// <summary>Splits a path into dashes. Each dash is added to <paramref name="into"/> as its own open path.</summary>
        public static void Dashes(List<Vector2> pts, bool closed, float dash, float gap, float phase, List<List<Vector2>> into)
        {
            var path = new List<Vector2>(pts);
            if (closed && pts.Count > 0) path.Add(pts[0]);
            float total = Length(path);
            if (total <= 0f) return;
            float period = dash + gap;
            float start = -Mathf.Repeat(phase, period);
            for (float s = start; s < total; s += period)
            {
                float a = Mathf.Max(0f, s), b = Mathf.Min(total, s + dash);
                if (b - a < 0.5f) continue;
                var piece = new List<Vector2>();
                Slice(path, a / total, b / total, piece);
                if (piece.Count >= 2) into.Add(piece);
            }
        }

        // ------------------------------------------------------------------ drawing

        static readonly List<Vector2> s_Clean = new List<Vector2>(256);
        static readonly List<Vector2> s_Normals = new List<Vector2>(256);
        static readonly List<float> s_Miter = new List<float>(256);

        static readonly float[] k_StrokeAlpha = { 0f, 1f, 1f, 0f };
        static readonly float[] k_StrokeOffset = new float[4];

        // A gaussian-ish falloff, sampled at fractions of the glow radius.
        static readonly float[] k_GlowFraction = { -1f, -0.62f, -0.36f, -0.16f, 0f, 0.16f, 0.36f, 0.62f, 1f };
        static readonly float[] k_GlowAlpha = { 0f, 0.07f, 0.24f, 0.62f, 1f, 0.62f, 0.24f, 0.07f, 0f };
        static readonly float[] k_GlowOffset = new float[9];

        /// <summary>An anti-aliased line along a path.</summary>
        public static void Stroke(VertexHelper vh, List<Vector2> pts, bool closed, float width, float feather, ColorAt color, float alpha = 1f)
        {
            if (pts.Count < 2 || alpha <= 0f) return;
            // Lines thinner than a pixel get fainter instead of thinner, which looks smoother.
            float w = Mathf.Max(width, feather);
            float a = alpha * Mathf.Clamp01(width / feather);
            float half = w * 0.5f, f = feather * 0.5f;
            k_StrokeOffset[0] = -(half + f);
            k_StrokeOffset[1] = -Mathf.Max(0f, half - f);
            k_StrokeOffset[2] = Mathf.Max(0f, half - f);
            k_StrokeOffset[3] = half + f;
            Bands(vh, pts, closed, k_StrokeOffset, k_StrokeAlpha, color, a, feather);
        }

        /// <summary>A soft glow along a path. Draw it under the stroke.</summary>
        public static void Glow(VertexHelper vh, List<Vector2> pts, bool closed, float radius, ColorAt color, float intensity)
        {
            if (pts.Count < 2 || intensity <= 0f || radius <= 0f) return;
            for (int i = 0; i < k_GlowFraction.Length; i++) k_GlowOffset[i] = k_GlowFraction[i] * radius;
            Bands(vh, pts, closed, k_GlowOffset, k_GlowAlpha, color, intensity, radius * 0.6f);
        }

        static void Bands(VertexHelper vh, List<Vector2> pts, bool closed, float[] offsets, float[] alphas, ColorAt color, float alpha, float cap)
        {
            Clean(pts, closed);
            int n = s_Clean.Count;
            if (n < 2) return;
            ComputeNormals(closed);

            int rows = offsets.Length;
            int first = vh.currentVertCount;

            // Open paths get a faded cap at each end so lines don't stop with a hard edge.
            int sections = 0;
            if (!closed)
            {
                Vector2 dir = (s_Clean[0] - s_Clean[1]).normalized;
                AddSection(vh, s_Clean[0] + dir * cap, s_Normals[0], 1f, offsets, alphas, color, 0f);
                sections++;
            }
            for (int i = 0; i < n; i++)
            {
                AddSection(vh, s_Clean[i], s_Normals[i], s_Miter[i], offsets, alphas, color, alpha);
                sections++;
            }
            if (closed)
            {
                AddSection(vh, s_Clean[0], s_Normals[0], s_Miter[0], offsets, alphas, color, alpha);
                sections++;
            }
            else
            {
                Vector2 dir = (s_Clean[n - 1] - s_Clean[n - 2]).normalized;
                AddSection(vh, s_Clean[n - 1] + dir * cap, s_Normals[n - 1], 1f, offsets, alphas, color, 0f);
                sections++;
            }

            for (int s = 0; s < sections - 1; s++)
            {
                int a = first + s * rows, b = a + rows;
                for (int r = 0; r < rows - 1; r++)
                {
                    vh.AddTriangle(a + r, b + r, b + r + 1);
                    vh.AddTriangle(a + r, b + r + 1, a + r + 1);
                }
            }
        }

        static void AddSection(VertexHelper vh, Vector2 p, Vector2 normal, float miter, float[] offsets, float[] alphas, ColorAt color, float alpha)
        {
            Color c = color(p);
            for (int r = 0; r < offsets.Length; r++)
            {
                var col = c;
                col.a *= alphas[r] * alpha;
                vh.AddVert(p + normal * (offsets[r] * miter), col, Vector2.zero);
            }
        }

        static void Clean(List<Vector2> pts, bool closed)
        {
            s_Clean.Clear();
            for (int i = 0; i < pts.Count; i++)
            {
                if (s_Clean.Count > 0 && (pts[i] - s_Clean[s_Clean.Count - 1]).sqrMagnitude < 0.0004f) continue;
                s_Clean.Add(pts[i]);
            }
            if (closed && s_Clean.Count > 2 && (s_Clean[0] - s_Clean[s_Clean.Count - 1]).sqrMagnitude < 0.0004f)
                s_Clean.RemoveAt(s_Clean.Count - 1);
        }

        static void ComputeNormals(bool closed)
        {
            s_Normals.Clear();
            s_Miter.Clear();
            int n = s_Clean.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = s_Clean[i];
                Vector2 prev = i > 0 ? s_Clean[i - 1] : closed ? s_Clean[n - 1] : p;
                Vector2 next = i < n - 1 ? s_Clean[i + 1] : closed ? s_Clean[0] : p;
                Vector2 d0 = p - prev, d1 = next - p;
                if (d0.sqrMagnitude < 1e-8f) d0 = d1;
                if (d1.sqrMagnitude < 1e-8f) d1 = d0;
                d0.Normalize();
                d1.Normalize();
                Vector2 t = d0 + d1;
                if (t.sqrMagnitude < 1e-6f) t = d1;
                t.Normalize();
                var normal = new Vector2(-t.y, t.x);
                float dot = Vector2.Dot(normal, new Vector2(-d1.y, d1.x));
                s_Normals.Add(normal);
                s_Miter.Add(Mathf.Min(1f / Mathf.Max(0.4f, dot), 2.2f));
            }
        }

        /// <summary>Fills a convex shape, with an anti-aliased rim.</summary>
        public static void Fill(VertexHelper vh, List<Vector2> convex, float feather, ColorAt color)
        {
            Clean(convex, true);
            int n = s_Clean.Count;
            if (n < 3) return;

            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++) centre += s_Clean[i];
            centre /= n;

            // Which way is outward depends on winding.
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = s_Clean[i], b = s_Clean[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            float outward = area >= 0f ? -1f : 1f;
            ComputeNormals(true);

            int first = vh.currentVertCount;
            vh.AddVert(centre, color(centre), Vector2.zero);
            float f = feather * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector2 normal = s_Normals[i] * (outward * s_Miter[i]);
                Vector2 inner = s_Clean[i] - normal * f;
                Vector2 outer = s_Clean[i] + normal * f;
                vh.AddVert(inner, color(inner), Vector2.zero);
                var c = color(outer);
                c.a = 0f;
                vh.AddVert(outer, c, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int ii = first + 1 + i * 2, jj = first + 1 + j * 2;
                vh.AddTriangle(first, ii, jj);
                vh.AddTriangle(ii, ii + 1, jj + 1);
                vh.AddTriangle(ii, jj + 1, jj);
            }
        }

        static readonly List<Vector2> s_Scratch = new List<Vector2>(128);

        public static void Disc(VertexHelper vh, Vector2 centre, float radius, float feather, Color color)
        {
            s_Scratch.Clear();
            Circle(s_Scratch, centre, radius);
            Fill(vh, s_Scratch, feather, _ => color);
        }

        /// <summary>A round glow that fades from the centre.</summary>
        public static void SoftDisc(VertexHelper vh, Vector2 centre, float radius, Color color, int segments = 32)
        {
            int first = vh.currentVertCount;
            vh.AddVert(centre, color, Vector2.zero);
            float[] rings = { 0.3f, 0.6f, 1f };
            float[] alphas = { 0.55f, 0.18f, 0f };
            for (int r = 0; r < rings.Length; r++)
            {
                var c = color;
                c.a *= alphas[r];
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vh.AddVert(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * rings[r], c, Vector2.zero);
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                vh.AddTriangle(first, first + 1 + i, first + 1 + j);
                for (int r = 0; r < rings.Length - 1; r++)
                {
                    int a0 = first + 1 + r * segments;
                    int b0 = a0 + segments;
                    vh.AddTriangle(a0 + i, b0 + i, b0 + j);
                    vh.AddTriangle(a0 + i, b0 + j, a0 + j);
                }
            }
        }

        /// <summary>A glowing halo around a closed path, for highlighted boxes.</summary>
        public static void Halo(VertexHelper vh, List<Vector2> closedPath, float radius, ColorAt color, float intensity)
        {
            Glow(vh, closedPath, true, radius, color, intensity);
        }
    }
}
