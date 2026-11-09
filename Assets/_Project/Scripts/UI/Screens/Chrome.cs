using System;
using Blackbox.Game;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>Small pieces shared by every screen: the wordmark, key hints and quiet text buttons.</summary>
    public static class Chrome
    {
        /// <summary>The BLACKBOX wordmark, left-aligned at (x, y). Clicking it runs <paramref name="clicked"/>.</summary>
        public static VoidText Wordmark(Transform parent, float x, float y, int size, Action clicked)
        {
            var style = TextStyle.Wordmark.WithSize(size);
            var t = UIKit.Text(parent, "BLACKBOX", style, TextAnchor.MiddleLeft, "Wordmark");
            float w = t.MeasureWidth();
            t.rectTransform.At(x + w * 0.5f, y, w, size * 1.6f);
            t.alignment = TextAnchor.MiddleCenter;
            if (clicked != null)
            {
                var hit = UIKit.Hit(t.rectTransform);
                hit.rectTransform.Stretch();
                var c = t.gameObject.AddComponent<Clickable>();
                c.Clicked = clicked;
                c.HoverChanged = on => t.color = on ? Palette.Text : style.Color;
            }
            return t;
        }

        /// <summary>
        /// A quiet text button such as MENU or BACK. <paramref name="anchor"/> is
        /// the x of its left edge, or its right edge when <paramref name="alignRight"/>.
        /// </summary>
        public static Clickable TextButton(Transform parent, string text, float anchor, float y, bool alignRight, Action clicked, string icon = null)
        {
            var style = TextStyle.Label.WithSize(15);
            var t = UIKit.Text(parent, text, style, TextAnchor.MiddleCenter, "Button " + text);
            float w = t.MeasureWidth();
            float iconSpace = icon != null ? 24f : 0f;
            float total = w + iconSpace;
            float left = alignRight ? anchor - total : anchor;
            var rt = UIKit.Rect(parent, "Text button " + text).At(left + total * 0.5f, y, total + 24f, 40f);
            t.rectTransform.SetParent(rt, false);
            t.rectTransform.Centered(iconSpace * 0.5f, 0f, w + 4f, 30f);
            GlyphGraphic glyph = null;
            if (icon != null)
            {
                glyph = UIKit.Glyph(rt, icon, Palette.Dim, 1.5f);
                glyph.rectTransform.Centered(-total * 0.5f + 7f, 0f, 14f, 14f);
            }
            UIKit.Hit(rt).rectTransform.Stretch();
            var c = rt.gameObject.AddComponent<Clickable>();
            c.Clicked = clicked;
            c.HoverChanged = on =>
            {
                t.color = on ? Palette.Text : style.Color;
                if (glyph != null) glyph.Tint = t.color;
            };
            return c;
        }

        /// <summary>
        /// A row of key hints such as [SPACE] RUN  [P] PROBE. Pairs are key then action.
        /// <paramref name="anchor"/> is the left edge, or the right edge when <paramref name="alignRight"/>.
        /// </summary>
        public static RectTransform KeyHints(Transform parent, float anchor, float y, bool alignRight, params string[] pairs)
        {
            var row = UIKit.Rect(parent, "Key hints").At(0f, 0f, 0f, 0f);
            var keyStyle = TextStyle.Label.WithSize(11).WithTracking(0.14f).WithColor(Palette.Dim);
            var actionStyle = TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint);
            float x = 0f;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                if (i > 0) x += 26f;
                var key = UIKit.Text(row, pairs[i], keyStyle, TextAnchor.MiddleCenter, "Key");
                float kw = Mathf.Max(22f, key.MeasureWidth() + 16f);
                var cap = UIKit.Shape(row, "Cap");
                cap.Radius = 5f;
                cap.Style(Paint.None, Color.clear, Paint.Solid, Palette.Outline, 1f);
                cap.rectTransform.At(x + kw * 0.5f, 0f, kw, 22f);
                key.rectTransform.At(x + kw * 0.5f, 0f, kw, 22f);
                key.transform.SetAsLastSibling();
                x += kw + 9f;
                var action = UIKit.Text(row, pairs[i + 1], actionStyle, TextAnchor.MiddleCenter, "Action");
                float aw = action.MeasureWidth();
                action.rectTransform.At(x + aw * 0.5f, 0f, aw + 4f, 22f);
                x += aw;
            }
            // A zero-size row: its children are laid out from its own position.
            float left = alignRight ? anchor - x : anchor;
            row.At(left, y, 0f, 0f);
            return row;
        }

        /// <summary>A short label with a value, like TESTS 04.</summary>
        public static VoidText Counter(Transform parent, string label, float right, float y, out VoidText value)
        {
            value = UIKit.Text(parent, "00", TextStyle.Digit.WithSize(22).WithWeight(300).WithColor(Palette.Text), TextAnchor.MiddleRight, "Value");
            value.rectTransform.At(right - 30f, y, 60f, 30f);
            var l = UIKit.Text(parent, label, TextStyle.Label.WithSize(13), TextAnchor.MiddleRight, "Label");
            l.rectTransform.At(right - 72f - 80f, y, 160f, 20f);
            return l;
        }
    }
}
