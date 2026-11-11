using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Blackbox.UI
{
    /// <summary>
    /// Text with letter spacing. The Void style leans on widely tracked uppercase
    /// labels, which legacy Text can't do, so this keeps Text's font handling and
    /// material but lays out the glyphs itself. Vertical centring uses the cap
    /// height, so uppercase labels sit optically centred in their rect.
    /// </summary>
    public class VoidText : Text
    {
        [SerializeField] float m_Tracking;
        [SerializeField] bool m_Uppercase;
        [SerializeField] bool m_SignalFill;
        [SerializeField] float m_LineHeight = 1.4f;

        /// <summary>Extra space after each character, in ems (0.28 is the Void label style).</summary>
        public float Tracking { get => m_Tracking; set { if (m_Tracking != value) { m_Tracking = value; SetVerticesDirty(); SetLayoutDirty(); } } }
        public bool Uppercase { get => m_Uppercase; set { if (m_Uppercase != value) { m_Uppercase = value; SetVerticesDirty(); SetLayoutDirty(); } } }
        /// <summary>Colour each glyph by the signal gradient instead of a flat colour.</summary>
        public bool SignalFill { get => m_SignalFill; set { if (m_SignalFill != value) { m_SignalFill = value; SetVerticesDirty(); } } }
        /// <summary>Distance between baselines, in ems.</summary>
        public float LineHeight { get => m_LineHeight; set { if (m_LineHeight != value) { m_LineHeight = value; SetVerticesDirty(); } } }

        static readonly List<string> s_Lines = new List<string>();
        static readonly StringBuilder s_Builder = new StringBuilder();

        string Display => string.IsNullOrEmpty(text) ? string.Empty : m_Uppercase ? text.ToUpperInvariant() : text;

        float Scale => Mathf.Max(0.05f, pixelsPerUnit);

        int PixelSize => Mathf.Clamp(Mathf.RoundToInt(fontSize * Scale), 1, 512);

        /// <summary>Width of one line of text in canvas units, without trailing tracking.</summary>
        float LineWidth(string line, int px, float scale)
        {
            if (string.IsNullOrEmpty(line)) return 0f;
            float w = 0f, track = m_Tracking * fontSize;
            foreach (char ch in line)
            {
                if (font.GetCharacterInfo(ch, out CharacterInfo ci, px, fontStyle)) w += ci.advance / scale;
                else w += fontSize * 0.5f;
                w += track;
            }
            return w - track;
        }

        /// <summary>The width this text needs on one line.</summary>
        public float MeasureWidth()
        {
            if (font == null) return 0f;
            string s = Display;
            if (s.Length == 0) return 0f;
            int px = PixelSize;
            font.RequestCharactersInTexture(s, px, fontStyle);
            float widest = 0f;
            foreach (var line in s.Split('\n')) widest = Mathf.Max(widest, LineWidth(line, px, Scale));
            return widest;
        }

        public override float preferredWidth => MeasureWidth();

        public override float preferredHeight
        {
            get
            {
                int lines = 1;
                foreach (char ch in Display) if (ch == '\n') lines++;
                return fontSize * (0.75f + (lines - 1) * m_LineHeight) + fontSize * 0.5f;
            }
        }

        void BreakLines(string s, int px, float scale, float maxWidth)
        {
            s_Lines.Clear();
            foreach (var paragraph in s.Split('\n'))
            {
                if (horizontalOverflow != HorizontalWrapMode.Wrap || maxWidth <= 0f || LineWidth(paragraph, px, scale) <= maxWidth)
                {
                    s_Lines.Add(paragraph);
                    continue;
                }
                s_Builder.Clear();
                foreach (var word in paragraph.Split(' '))
                {
                    string attempt = s_Builder.Length == 0 ? word : s_Builder + " " + word;
                    if (s_Builder.Length > 0 && LineWidth(attempt, px, scale) > maxWidth)
                    {
                        s_Lines.Add(s_Builder.ToString());
                        s_Builder.Clear();
                        s_Builder.Append(word);
                    }
                    else
                    {
                        s_Builder.Clear();
                        s_Builder.Append(attempt);
                    }
                }
                if (s_Builder.Length > 0) s_Lines.Add(s_Builder.ToString());
            }
        }

        /// <summary>
        /// Puts this text's characters in the font texture before its mesh is built.
        /// Asking for them while the mesh is being built can make the font texture
        /// grow, which makes every other text rebuild at once through the vertex
        /// helper Unity shares between graphics. That left this text drawn where
        /// another one was, on top of it: a hint over the goal line, or over a
        /// dialog's question.
        /// </summary>
        public override void Rebuild(CanvasUpdate update)
        {
            if (update == CanvasUpdate.PreRender && font != null)
            {
                string s = Display;
                if (s.Length > 0) font.RequestCharactersInTexture(s + "H", PixelSize, fontStyle);
            }
            base.Rebuild(update);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (font == null) return;
            string s = Display;
            if (s.Length == 0) return;

            float scale = Scale;
            int px = PixelSize;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                // Normally a no-op, as Rebuild asked already. One request, so a rebuild
                // of the font texture can't drop the first set of characters.
                font.RequestCharactersInTexture(s + "H", px, fontStyle);
                // If that did rebuild other texts through the shared helper, start clean.
                vh.Clear();

                float capHeight = font.GetCharacterInfo('H', out CharacterInfo h, px, fontStyle) ? h.maxY / scale : fontSize * 0.7f;
                Rect r = rectTransform.rect;
                BreakLines(s, px, scale, r.width);

                float step = fontSize * m_LineHeight;
                float block = capHeight + (s_Lines.Count - 1) * step;
                float baseline;
                switch (alignment)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.UpperCenter:
                    case TextAnchor.UpperRight:
                        baseline = r.yMax - capHeight;
                        break;
                    case TextAnchor.LowerLeft:
                    case TextAnchor.LowerCenter:
                    case TextAnchor.LowerRight:
                        baseline = r.yMin + (s_Lines.Count - 1) * step;
                        break;
                    default:
                        baseline = r.center.y + block * 0.5f - capHeight;
                        break;
                }

                var frame = VoidFrame.Current;
                Matrix4x4 toFrame = frame != null ? frame.worldToLocalMatrix * rectTransform.localToWorldMatrix : Matrix4x4.identity;
                Color flat = color;
                float track = m_Tracking * fontSize;

                foreach (var line in s_Lines)
                {
                    float width = LineWidth(line, px, scale);
                    float x;
                    switch (alignment)
                    {
                        case TextAnchor.UpperLeft:
                        case TextAnchor.MiddleLeft:
                        case TextAnchor.LowerLeft:
                            x = r.xMin;
                            break;
                        case TextAnchor.UpperRight:
                        case TextAnchor.MiddleRight:
                        case TextAnchor.LowerRight:
                            x = r.xMax - width;
                            break;
                        default:
                            x = r.center.x - width * 0.5f;
                            break;
                    }

                    foreach (char ch in line)
                    {
                        if (!font.GetCharacterInfo(ch, out CharacterInfo ci, px, fontStyle))
                        {
                            x += fontSize * 0.5f + track;
                            continue;
                        }
                        float x0 = x + ci.minX / scale, x1 = x + ci.maxX / scale;
                        float y0 = baseline + ci.minY / scale, y1 = baseline + ci.maxY / scale;
                        if (x1 > x0 && y1 > y0)
                        {
                            Color c = flat;
                            if (m_SignalFill)
                            {
                                float fx = toFrame.MultiplyPoint3x4(new Vector3((x0 + x1) * 0.5f, 0f)).x + VoidFrame.Width * 0.5f;
                                c = Palette.SignalAtX(fx);
                                c.a = flat.a;
                            }
                            int i = vh.currentVertCount;
                            vh.AddVert(new Vector3(x0, y0), c, ci.uvBottomLeft);
                            vh.AddVert(new Vector3(x0, y1), c, ci.uvTopLeft);
                            vh.AddVert(new Vector3(x1, y1), c, ci.uvTopRight);
                            vh.AddVert(new Vector3(x1, y0), c, ci.uvBottomRight);
                            vh.AddTriangle(i, i + 1, i + 2);
                            vh.AddTriangle(i + 2, i + 3, i);
                        }
                        x += ci.advance / scale + track;
                    }
                    baseline -= step;
                }
            }
            finally
            {
                m_DisableFontTextureRebuiltCallback = false;
            }
        }
    }
}
