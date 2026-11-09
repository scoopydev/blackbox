using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// Builds the interface in code. Every screen is laid out on the fixed
    /// 1920 x 1080 frame, so positions read like the concept board: x from the
    /// left, y down from the top.
    /// </summary>
    public static class UIKit
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static T Add<T>(Transform parent, string name) where T : Component
        {
            return Rect(parent, name).gameObject.AddComponent<T>();
        }

        /// <summary>Fills the parent.</summary>
        public static RectTransform Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Centres the rect at (x, y) measured from the parent's top-left corner, y down.</summary>
        public static RectTransform At(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        /// <summary>Centres the rect at (x, y) measured from the parent's centre, y up.</summary>
        public static RectTransform Centered(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        public static VoidText Text(Transform parent, string text, TextStyle style, TextAnchor align = TextAnchor.MiddleCenter, string name = "Text")
        {
            var t = Add<VoidText>(parent, name);
            t.font = VoidFonts.ForWeight(style.Weight);
            t.fontSize = style.Size;
            t.Tracking = style.Tracking;
            t.Uppercase = style.Upper;
            t.color = style.Color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.text = text;
            return t;
        }

        public static void Apply(this VoidText t, TextStyle style)
        {
            t.font = VoidFonts.ForWeight(style.Weight);
            t.fontSize = style.Size;
            t.Tracking = style.Tracking;
            t.Uppercase = style.Upper;
            t.color = style.Color;
        }

        public static ShapeGraphic Shape(Transform parent, string name, ShapeKind kind = ShapeKind.RoundedRect)
        {
            var s = Add<ShapeGraphic>(parent, name);
            s.Shape = kind;
            s.raycastTarget = false;
            return s;
        }

        public static GlyphGraphic Glyph(Transform parent, string glyph, Color tint, float lineWidth = 2f, string name = "Glyph")
        {
            var g = Add<GlyphGraphic>(parent, name);
            g.Glyph = glyph;
            g.Tint = tint;
            g.LineWidth = lineWidth;
            g.raycastTarget = false;
            return g;
        }

        /// <summary>A horizontal hairline, like the rules under the header.</summary>
        public static PathGraphic Rule(Transform parent, float x0, float x1, float y, Color color, float width = 1f)
        {
            var p = Add<PathGraphic>(parent, "Rule");
            p.raycastTarget = false;
            float w = x1 - x0;
            p.rectTransform.At((x0 + x1) * 0.5f, y, w, 4f);
            p.BaseColor = color;
            p.BaseWidth = width;
            p.SetPoints(new System.Collections.Generic.List<Vector2> { new Vector2(-w * 0.5f, 0f), new Vector2(w * 0.5f, 0f) });
            return p;
        }

        public static HitArea Hit(Transform parent, string name = "Hit")
        {
            var h = Add<HitArea>(parent, name);
            h.color = Color.clear;
            return h;
        }

        public static CanvasGroup Group(this Component c)
        {
            var g = c.GetComponent<CanvasGroup>();
            return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
