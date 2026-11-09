using System;
using System.Collections.Generic;

namespace Blackbox.Components
{
    /// <summary>
    /// What travels along the wires. Each chapter is built on one medium, and every
    /// value in a circuit is a small whole number whose meaning the medium gives.
    /// </summary>
    public enum Medium
    {
        /// <summary>0 or 1.</summary>
        Binary,
        /// <summary>A digit, 0 to 9. Arithmetic wraps round: 9 + 1 is 0.</summary>
        Decimal,
        /// <summary>A form (circle, triangle, square, hexagon) that is hollow or solid.</summary>
        Shape,
        /// <summary>A wave form (sine, square, saw) at one of three pitches.</summary>
        Wave,
    }

    /// <summary>Shape values: <c>form * 2 + fill</c>.</summary>
    public static class Shapes
    {
        public const int Forms = 4;
        public const int Circle = 0, Triangle = 1, Square = 2, Hexagon = 3;
        public static readonly string[] FormNames = { "circle", "triangle", "square", "hexagon" };
        /// <summary>Corners of each form. A circle has none.</summary>
        public static readonly int[] Corners = { 0, 3, 4, 6 };

        public static int Form(int v) => v / 2;
        public static bool Solid(int v) => (v & 1) == 1;
        public static int Make(int form, bool solid) => ((form % Forms + Forms) % Forms) * 2 + (solid ? 1 : 0);
    }

    /// <summary>Wave values: <c>form * 3 + pitch</c>.</summary>
    public static class Waves
    {
        public const int Forms = 3;
        public const int Pitches = 3;
        public const int Sine = 0, SquareWave = 1, Saw = 2;
        public static readonly string[] FormNames = { "sine", "square", "saw" };
        public static readonly string[] PitchNames = { "low", "mid", "high" };

        public static int Form(int v) => v / Pitches;
        public static int Pitch(int v) => v % Pitches;
        public static int Make(int form, int pitch) => ((form % Forms + Forms) % Forms) * Pitches + ((pitch % Pitches + Pitches) % Pitches);
    }

    public static class Media
    {
        /// <summary>How many values the medium has. Values run from 0 to Size - 1.</summary>
        public static int Size(Medium m)
        {
            switch (m)
            {
                case Medium.Binary: return 2;
                case Medium.Decimal: return 10;
                case Medium.Shape: return Shapes.Forms * 2;
                default: return Waves.Forms * Waves.Pitches;
            }
        }

        public static bool IsValid(Medium m, int v) => v >= 0 && v < Size(m);

        /// <summary>The medium's name as puzzle files write it.</summary>
        public static string Key(Medium m) => m.ToString().ToLowerInvariant();

        public static bool TryParseMedium(string s, out Medium m)
        {
            foreach (Medium candidate in Enum.GetValues(typeof(Medium)))
            {
                if (string.Equals(Key(candidate), s?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    m = candidate;
                    return true;
                }
            }
            m = Medium.Binary;
            return false;
        }

        /// <summary>What one value is called: "a bit", "a digit", "a shape", "a wave".</summary>
        public static string Noun(Medium m)
        {
            switch (m)
            {
                case Medium.Binary: return "bit";
                case Medium.Decimal: return "digit";
                case Medium.Shape: return "shape";
                default: return "wave";
            }
        }

        /// <summary>The verb an output uses for its value in goal sentences.</summary>
        public static string Verb(Medium m) => m == Medium.Wave ? "play" : "show";

        /// <summary>A value in words, for goals, tests and the editor. "1", "7", "solid triangle", "square wave, high".</summary>
        public static string Describe(Medium m, int v)
        {
            if (!IsValid(m, v)) return "?";
            switch (m)
            {
                case Medium.Binary:
                case Medium.Decimal:
                    return v.ToString();
                case Medium.Shape:
                    return (Shapes.Solid(v) ? "solid " : "hollow ") + Shapes.FormNames[Shapes.Form(v)];
                default:
                    return Waves.FormNames[Waves.Form(v)] + " " + Waves.PitchNames[Waves.Pitch(v)];
            }
        }

        /// <summary>
        /// Reads a value as puzzle files write it: a number for binary and decimal,
        /// words for shapes ("solid triangle") and waves ("square high").
        /// </summary>
        public static bool TryParse(Medium m, string s, out int v)
        {
            v = -1;
            if (s == null) return false;
            s = s.Trim().ToLowerInvariant();
            if (int.TryParse(s, out int n))
            {
                v = n;
                return IsValid(m, n);
            }
            if (m == Medium.Shape)
            {
                bool solid = s.StartsWith("solid ", StringComparison.Ordinal);
                bool hollow = s.StartsWith("hollow ", StringComparison.Ordinal);
                if (!solid && !hollow) return false;
                string form = s.Substring(solid ? 6 : 7).Trim();
                int f = Array.IndexOf(Shapes.FormNames, form);
                if (f < 0) return false;
                v = Shapes.Make(f, solid);
                return true;
            }
            if (m == Medium.Wave)
            {
                var parts = s.Replace(",", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) return false;
                int f = Array.IndexOf(Waves.FormNames, parts[0]);
                int p = Array.IndexOf(Waves.PitchNames, parts[1]);
                if (f < 0 || p < 0) return false;
                v = Waves.Make(f, p);
                return true;
            }
            return false;
        }

        /// <summary>Every value of the medium in order, for tests and tools.</summary>
        public static IEnumerable<int> All(Medium m)
        {
            int n = Size(m);
            for (int i = 0; i < n; i++) yield return i;
        }
    }
}
