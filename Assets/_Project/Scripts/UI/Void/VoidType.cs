using UnityEngine;

namespace Blackbox.UI
{
    /// <summary>
    /// Sora at the four weights the game uses. The fonts live in
    /// Fonts/Resources/BlackboxFonts; if they're missing the built-in font is used
    /// so the game still runs.
    /// </summary>
    public static class VoidFonts
    {
        static Font s_ExtraLight, s_Light, s_Regular, s_SemiBold, s_Fallback;

        public static Font ExtraLight => s_ExtraLight ? s_ExtraLight : s_ExtraLight = Load("Sora-ExtraLight");
        public static Font Light => s_Light ? s_Light : s_Light = Load("Sora-Light");
        public static Font Regular => s_Regular ? s_Regular : s_Regular = Load("Sora-Regular");
        public static Font SemiBold => s_SemiBold ? s_SemiBold : s_SemiBold = Load("Sora-SemiBold");

        public static Font ForWeight(int weight)
        {
            if (weight <= 200) return ExtraLight;
            if (weight <= 300) return Light;
            if (weight <= 400) return Regular;
            return SemiBold;
        }

        static Font Load(string name)
        {
            var font = Resources.Load<Font>("BlackboxFonts/" + name);
            if (font != null) return font;
            Debug.LogWarning($"BLACKBOX: font {name} not found in Resources/BlackboxFonts, using the built-in font.");
            return Fallback;
        }

        static Font Fallback
        {
            get
            {
                if (s_Fallback) return s_Fallback;
                // Unity 2022.2+ renamed the built-in font.
                try { s_Fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (!s_Fallback) try { s_Fallback = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                return s_Fallback;
            }
        }
    }

    /// <summary>The type styles from the Void board.</summary>
    public struct TextStyle
    {
        public int Weight;
        public int Size;
        public float Tracking;
        public bool Upper;
        /// <summary>Sentences keep the case they're written in, even with <see cref="VoidText.AllCaps"/> on.</summary>
        public bool Sentence;
        public Color Color;

        public TextStyle(int weight, int size, float tracking, bool upper, Color color)
        {
            Weight = weight;
            Size = size;
            Tracking = tracking;
            Upper = upper;
            Sentence = false;
            Color = color;
        }

        public TextStyle WithSize(int size) { var s = this; s.Size = size; return s; }
        public TextStyle WithColor(Color c) { var s = this; s.Color = c; return s; }
        public TextStyle WithWeight(int w) { var s = this; s.Weight = w; return s; }
        public TextStyle WithTracking(float t) { var s = this; s.Tracking = t; return s; }
        /// <summary>Shown as written: for sentences and the player's own words.</summary>
        public TextStyle AsSentence() { var s = this; s.Upper = false; s.Sentence = true; return s; }

        /// <summary>BLACKBOX on the title screen.</summary>
        public static TextStyle Display => new TextStyle(200, 80, 0.5f, true, Palette.Text);
        /// <summary>The wordmark in the corner of every screen.</summary>
        public static TextStyle Wordmark => new TextStyle(200, 30, 0.45f, true, Palette.Text);
        /// <summary>Headings and names.</summary>
        public static TextStyle Heading => new TextStyle(300, 36, 0.04f, false, Palette.Text);
        /// <summary>Small, widely tracked, uppercase, muted.</summary>
        public static TextStyle Label => new TextStyle(400, 18, 0.28f, true, Palette.Dim);
        /// <summary>Values: the only text that gets size.</summary>
        public static TextStyle Value => new TextStyle(300, 48, 0.02f, false, Palette.Text);
        /// <summary>Menu entries and button text.</summary>
        public static TextStyle Menu => new TextStyle(400, 21, 0.3f, true, Palette.Dim);
        /// <summary>Sentences, used sparingly.</summary>
        public static TextStyle Body => new TextStyle(300, 22, 0.02f, false, Palette.Dim).AsSentence();
        /// <summary>Digits in tables.</summary>
        public static TextStyle Digit => new TextStyle(400, 20, 0.05f, false, Palette.Dim);
    }
}
