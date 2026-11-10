using System;
using System.Collections.Generic;
using System.Text;

namespace Blackbox.Components
{
    /// <summary>
    /// A signal given to a circuit the player wires: a type and either one value
    /// or a pattern of values held in turn through the loop.
    /// </summary>
    public sealed class SignalSpec
    {
        public Medium Medium;
        /// <summary>The values in the order they play. One value is a steady signal.</summary>
        public int[] Values;
        public Trace Trace;

        /// <summary>The single value of a steady signal, or null for a pattern.</summary>
        public int? Value => Values.Length == 1 ? Values[0] : (int?)null;

        public override string ToString() => Signals.Describe(this);
    }

    /// <summary>
    /// Reads and writes the signals of wired circuits, such as "binary 0110",
    /// "decimal 3", "shape solid triangle, hollow circle", "wave sine f2 a7" or
    /// "sampled saw f1 a7 s6".
    /// </summary>
    public static class Signals
    {
        /// <summary>Reads a signal. On failure <paramref name="error"/> says what is wrong.</summary>
        public static bool TryParse(string text, out SignalSpec spec, out string error)
        {
            spec = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)) { error = "a signal is a type and a value, like \"binary 1\" or \"wave sine f2 a7\""; return false; }
            text = text.Trim();
            int space = text.IndexOf(' ');
            if (space < 0) { error = $"\"{text}\" needs a type and a value, like \"binary 1\" or \"wave sine f2 a7\""; return false; }
            string key = text.Substring(0, space);
            string rest = text.Substring(space + 1).Trim();
            if (!Media.TryParseMedium(key, out var medium)) { error = $"\"{key}\" isn't a signal type (use binary, decimal, shape, wave or sampled)"; return false; }

            var parts = new List<string>();
            if (Media.IsWave(medium)) parts.Add(rest);
            else if (rest.Contains(",")) foreach (var p in rest.Split(',')) parts.Add(p.Trim());
            else if (medium != Medium.Shape && AllDigits(rest)) foreach (char c in rest) parts.Add(c.ToString());
            else if (medium != Medium.Shape) foreach (var p in rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) parts.Add(p);
            else parts.Add(rest);

            if (!Trace.FitsLoop(parts.Count))
            {
                error = $"a pattern of {parts.Count} values doesn't fit the loop evenly (use 1, 2, 3, 4, 6, 8, 9, 12 or 16 values)";
                return false;
            }
            var values = new int[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                if (!Media.TryParse(medium, parts[i], out values[i]))
                {
                    error = $"\"{parts[i]}\" isn't a {Media.Key(medium)} value";
                    return false;
                }
            }
            spec = new SignalSpec { Medium = medium, Values = values, Trace = Trace.Pattern(medium, values) };
            return true;
        }

        public static SignalSpec Parse(string text)
        {
            if (TryParse(text, out var spec, out string error)) return spec;
            throw new FormatException(error);
        }

        /// <summary>"binary 0110", "wave sine f2 a7": the form <see cref="TryParse"/> reads.</summary>
        public static string Describe(SignalSpec spec)
        {
            var sb = new StringBuilder(Media.Key(spec.Medium)).Append(' ');
            bool compact = spec.Medium == Medium.Binary || spec.Medium == Medium.Decimal;
            for (int i = 0; i < spec.Values.Length; i++)
            {
                if (i > 0 && !compact) sb.Append(", ");
                sb.Append(Media.Describe(spec.Medium, spec.Values[i]));
            }
            return sb.ToString();
        }

        static bool AllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
