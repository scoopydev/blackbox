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
            => KeyHintsAligned(parent, anchor, y, alignRight ? 1f : 0f, pairs);

        /// <summary>A row of key hints centred on <paramref name="cx"/>.</summary>
        public static RectTransform KeyHintsCentred(Transform parent, float cx, float y, params string[] pairs)
            => KeyHintsAligned(parent, cx, y, 0.5f, pairs);

        static RectTransform KeyHintsAligned(Transform parent, float anchor, float y, float align, string[] pairs)
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
            row.At(anchor - x * align, y, 0f, 0f);
            return row;
        }

        /// <summary>
        /// A clickable shortcut: a key cap and what it does, such as [ESC] MENU.
        /// It shows the key and works as a button, so the player can click it or
        /// learn the key. <paramref name="anchor"/> is the left edge, or the right
        /// edge when <paramref name="alignRight"/>. <paramref name="width"/> is how
        /// wide it came out, for laying out a row of them.
        /// </summary>
        public static Clickable KeyButton(Transform parent, string key, string label, float anchor, float y, bool alignRight, Action clicked, out float width)
        {
            var keyStyle = TextStyle.Label.WithSize(11).WithTracking(0.14f).WithColor(Palette.Dim);
            var labelStyle = TextStyle.Label.WithSize(14).WithTracking(0.3f);
            var rt = UIKit.Rect(parent, "Key button " + label);
            var k = UIKit.Text(rt, key, keyStyle, TextAnchor.MiddleCenter, "Key");
            float kw = Mathf.Max(26f, k.MeasureWidth() + 16f);
            var l = UIKit.Text(rt, label, labelStyle, TextAnchor.MiddleCenter, "Label");
            float lw = l.MeasureWidth();
            const float gap = 11f;
            const float pad = 12f;
            float total = kw + gap + lw;
            width = total;
            float left = alignRight ? anchor - total : anchor;
            rt.At(left + total * 0.5f, y, total + pad * 2f, 40f);

            var cap = UIKit.Shape(rt, "Cap");
            cap.Radius = 5f;
            cap.rectTransform.Centered(-total * 0.5f + kw * 0.5f, 0f, kw, 24f);
            k.rectTransform.Centered(-total * 0.5f + kw * 0.5f, 0f, kw, 24f);
            k.transform.SetAsLastSibling();
            l.rectTransform.Centered(total * 0.5f - lw * 0.5f, 0f, lw + 4f, 24f);

            UIKit.Hit(rt).rectTransform.Stretch();
            var c = rt.gameObject.AddComponent<Clickable>();
            c.Clicked = clicked;
            void Look(bool hot)
            {
                cap.Style(Paint.Solid, hot ? Palette.Surface : Color.black, Paint.Solid, hot ? Palette.Dim : Palette.Outline, 1f);
                k.color = hot ? Palette.Text : Palette.Dim;
                l.color = hot ? Palette.Text : Palette.Dim;
            }
            c.HoverChanged = Look;
            Look(false);
            return c;
        }

        public static Clickable KeyButton(Transform parent, string key, string label, float anchor, float y, bool alignRight, Action clicked)
            => KeyButton(parent, key, label, anchor, y, alignRight, clicked, out _);

        /// <summary>
        /// The bar along the top of a menu screen: the wordmark (back to the title),
        /// where the player is, such as  /  CHAPTERS, and the way back on the right.
        /// </summary>
        public static void Header(Transform parent, string place, string backLabel, Action back)
        {
            var mark = Wordmark(parent, 90f, 62f, 22, BlackboxApp.Instance != null ? (Action)BlackboxApp.Instance.GoTitle : null);
            if (!string.IsNullOrEmpty(place))
            {
                float x = 90f + mark.MeasureWidth() + 26f;
                var slash = UIKit.Text(parent, "/", TextStyle.Label.WithSize(15).WithColor(Palette.Faint), TextAnchor.MiddleLeft, "Slash");
                slash.rectTransform.At(x + 20f, 62f, 40f, 24f);
                var crumb = UIKit.Text(parent, place, TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Text), TextAnchor.MiddleLeft, "Place");
                crumb.rectTransform.At(x + 30f + 200f, 62f, 400f, 24f);
            }
            if (back != null) KeyButton(parent, "ESC", backLabel, 1830f, 62f, true, back);
            UIKit.Rule(parent, 90f, 1830f, 122f, Palette.Rule);
        }

        /// <summary>
        /// A hairline that lights as far as <paramref name="done"/> of <paramref name="total"/>,
        /// with the count beside it, like  12 / 55  CIRCUITS SOLVED. Centred on <paramref name="cx"/>.
        /// </summary>
        public static RectTransform ProgressBar(Transform parent, float cx, float y, float width, int done, int total, string caption)
        {
            var row = UIKit.Rect(parent, "Progress").At(cx, y, 0f, 0f);
            var count = UIKit.Text(row, $"{done} / {total}", TextStyle.Digit.WithSize(13).WithColor(Palette.Text), TextAnchor.MiddleRight, "Count");
            var label = caption != null ? UIKit.Text(row, caption, TextStyle.Label.WithSize(11).WithTracking(0.3f).WithColor(Palette.Faint), TextAnchor.MiddleLeft, "Caption") : null;
            float cw = count.MeasureWidth();
            float lw = label != null ? label.MeasureWidth() : 0f;
            float full = cw + 18f + width + (label != null ? 18f + lw : 0f);
            float x = -full * 0.5f;
            count.rectTransform.Centered(x + cw * 0.5f, 0f, cw + 4f, 20f);
            x += cw + 18f;
            var bar = UIKit.Add<PathGraphic>(row, "Bar");
            bar.raycastTarget = false;
            bar.rectTransform.Centered(x + width * 0.5f, 0f, width, 8f);
            bar.BaseColor = Palette.Outline;
            bar.BaseWidth = 2f;
            bar.LitWidth = 2f;
            bar.Glow = 0.35f;
            bar.GlowRadius = 8f;
            bar.SetPoints(new System.Collections.Generic.List<Vector2> { new Vector2(-width * 0.5f, 0f), new Vector2(width * 0.5f, 0f) });
            float f = total > 0 ? Mathf.Clamp01((float)done / total) : 0f;
            bar.SetLit(0f, Mathf.Max(0.0001f, f));
            bar.LitAlpha = f > 0f ? 1f : 0f;
            x += width + 18f;
            if (label != null) label.rectTransform.Centered(x + lw * 0.5f, 0f, lw + 4f, 20f);
            return row;
        }

        /// <summary>A small heading over a group of rows, like SOUND in Settings.</summary>
        public static VoidText Section(Transform parent, string text, float right, float y)
        {
            var t = UIKit.Text(parent, text, TextStyle.Label.WithSize(11).WithTracking(0.42f).WithColor(Palette.Faint), TextAnchor.MiddleRight, "Section " + text);
            t.rectTransform.At(right - 150f, y, 300f, 18f);
            return t;
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
