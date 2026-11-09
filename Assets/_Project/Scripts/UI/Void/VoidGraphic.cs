using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// The 1920 x 1080 frame every screen is laid out in. The canvas scaler fits it
    /// to the window, and the signal gradient is measured across it.
    /// </summary>
    public static class VoidFrame
    {
        public const float Width = 1920f;
        public const float Height = 1080f;
        public static RectTransform Current;
    }

    public enum Paint
    {
        None,
        Solid,
        /// <summary>The cyan-to-violet signal gradient, sampled by horizontal position on screen.</summary>
        Signal,
    }

    /// <summary>Base for the procedural Void graphics.</summary>
    public abstract class VoidGraphic : MaskableGraphic
    {
        Matrix4x4 _toFrame = Matrix4x4.identity;

        /// <summary>One screen pixel in canvas units: the width of an anti-aliased edge.</summary>
        protected float Feather
        {
            get
            {
                var c = canvas;
                float scale = c != null ? c.scaleFactor : 1f;
                return 1f / Mathf.Max(0.05f, scale);
            }
        }

        /// <summary>Call at the start of OnPopulateMesh before using <see cref="Colour"/> with Paint.Signal.</summary>
        protected void PrepareGradient()
        {
            var frame = VoidFrame.Current;
            _toFrame = frame != null ? frame.worldToLocalMatrix * rectTransform.localToWorldMatrix : rectTransform.localToWorldMatrix;
        }

        /// <summary>Horizontal position in the frame (0 at the left edge, 1920 at the right).</summary>
        protected float FrameX(Vector2 local) => _toFrame.MultiplyPoint3x4(local).x + VoidFrame.Width * 0.5f;

        protected ColorAt Colour(Paint paint, Color solid, float alpha = 1f)
        {
            float a = alpha * color.a;
            switch (paint)
            {
                case Paint.Signal:
                    return p =>
                    {
                        var c = Palette.SignalAtX(FrameX(p));
                        c.a *= a * solid.a;
                        return c;
                    };
                case Paint.Solid:
                    return p => new Color(solid.r, solid.g, solid.b, solid.a * a);
                default:
                    return p => Color.clear;
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }

    /// <summary>An invisible graphic that only catches pointer events.</summary>
    public sealed class HitArea : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
