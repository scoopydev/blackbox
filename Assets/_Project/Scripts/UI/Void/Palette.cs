using UnityEngine;

namespace Blackbox.UI
{
    /// <summary>
    /// The Void palette. Black is the canvas, greys are structure, and the
    /// cyan-to-violet gradient is reserved for live signals.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Void = Hex(0x000000);
        public static readonly Color Surface = Hex(0x07070A);
        public static readonly Color Rule = Hex(0x111217);
        public static readonly Color Outline = Hex(0x1D1E26);
        public static readonly Color Faint = Hex(0x3D404D);
        public static readonly Color Dim = Hex(0x6B6F7D);
        public static readonly Color Text = Hex(0xD8DBE6);
        public static readonly Color Cyan = Hex(0x36E2FF);
        public static readonly Color Violet = Hex(0xA46BFF);

        /// <summary>
        /// The colours a shape can be, by <c>Shapes.Colour</c>. Cyan (0) is drawn
        /// in the signal gradient like every other signal; the rest are flat.
        /// </summary>
        public static readonly Color[] ShapeColours = { Hex(0x36E2FF), Hex(0xFF5FD2), Hex(0xFFB23F), Hex(0x6BFF8E) };

        /// <summary>Where the signal gradient starts and ends, in frame x (0..1920).</summary>
        public const float GradientStartX = 160f;
        public const float GradientEndX = 1760f;

        /// <summary>The signal colour at a horizontal position in the frame.</summary>
        public static Color SignalAtX(float frameX)
        {
            return Signal((frameX - GradientStartX) / (GradientEndX - GradientStartX));
        }

        /// <summary>The signal colour at t in 0..1 along the gradient.</summary>
        public static Color Signal(float t)
        {
            return Color.Lerp(Cyan, Violet, Mathf.Clamp01(t));
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
