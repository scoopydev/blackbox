using System;

namespace Blackbox.Components
{
    /// <summary>What an output is: something that shows, plays or does what reaches it.</summary>
    public enum Device
    {
        /// <summary>Draws the signal itself: a value, a pattern or a wave.</summary>
        Display,
        /// <summary>Glows: brighter the bigger the signal, in a shape's colour.</summary>
        Light,
        /// <summary>Plays the signal as sound.</summary>
        Speaker,
        /// <summary>Shows a number: a bit or digit as itself, a shape's corners, a wave's height.</summary>
        Digits,
        /// <summary>Spins: faster the bigger the signal, backwards below the middle of a wave.</summary>
        Fan,
        /// <summary>Swings an arm from left to right as the signal rises.</summary>
        Servo,
    }

    /// <summary>
    /// How each output device reads each type of signal. A target on a device is
    /// met when the device does the same thing through the whole loop, so
    /// different signals can meet it: a digit display showing 3 is met by the
    /// digit 3 and by a triangle. Displays and speakers need the very same signal.
    ///
    /// <list type="bullet">
    /// <item>Light: a bit is off or on; a digit is a brightness from 0 (off) to 9 (full); a wave lights with its height above the middle and is dark below it; a shape lights fully in its form's colour, half as bright when hollow.</item>
    /// <item>Digits: a bit or digit shows itself, a shape its corners (a circle 0), a wave its height rounded (-7 to 7).</item>
    /// <item>Fan: speed from 0 to full: a bit off or full, a digit in ninths, a shape by its corners (a hexagon is full), a wave by its height, spinning backwards below the middle.</item>
    /// <item>Servo: the arm's angle from far left to far right: a wave from its bottom to its top, everything else from nothing (left) to full (right), on the same scales as the fan.</item>
    /// </list>
    /// </summary>
    public static class Devices
    {
        public static readonly Device[] All = { Device.Display, Device.Light, Device.Speaker, Device.Digits, Device.Fan, Device.Servo };

        /// <summary>
        /// Devices set aside for now (11 Nov 2026): every output in the game is a
        /// plain display, and the loader refuses a circuit with any other. The code
        /// for each device stays. To bring them back, empty this set and give the
        /// circuits their devices again (commit aea4196 has every circuit's device).
        /// </summary>
        public static readonly System.Collections.Generic.HashSet<Device> Shelved =
            new System.Collections.Generic.HashSet<Device> { Device.Light, Device.Speaker, Device.Digits, Device.Fan, Device.Servo };
        static readonly string[] s_Keys = { "display", "light", "speaker", "digits", "fan", "servo" };
        static readonly string[] s_Nouns = { "display", "light", "speaker", "digit display", "fan", "servo" };

        /// <summary>How far two readings may be apart and still count as the same.</summary>
        public const float Tolerance = 0.01f;

        public static string Key(Device d) => s_Keys[(int)d];
        public static string Noun(Device d) => s_Nouns[(int)d];

        public static bool TryParse(string s, out Device d)
        {
            for (int i = 0; i < s_Keys.Length; i++)
            {
                if (string.Equals(s_Keys[i], s?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    d = (Device)i;
                    return true;
                }
            }
            if (string.Equals(s?.Trim(), "motor", StringComparison.OrdinalIgnoreCase)) { d = Device.Fan; return true; }
            d = Device.Display;
            return false;
        }

        /// <summary>
        /// How much of a signal there is at one level, for the devices that work
        /// by amount: 0 to 1 for bits, digits (in ninths) and shapes (by corners, a
        /// hexagon is 1); -1 to 1 for waves.
        /// </summary>
        public static float Amount(Medium m, float level)
        {
            switch (m)
            {
                case Medium.Binary: return level;
                case Medium.Decimal: return level / 9f;
                case Medium.Shape: return Shapes.Corners[Shapes.Form((int)level)] / 6f;
                default: return level / Trace.Peak;
            }
        }

        /// <summary>The device's reading at one level of a signal. Displays and speakers have none: they show or play the signal itself.</summary>
        public static float Read(Device d, Medium m, float level)
        {
            switch (d)
            {
                case Device.Light:
                    if (m == Medium.Shape) return 1f; // lit in the shape's colour, whatever its form or fill
                    return Clamp01(Amount(m, level));
                case Device.Digits:
                    if (m == Medium.Shape) return Shapes.Corners[Shapes.Form((int)level)];
                    if (Media.IsWave(m)) return Sampled.RoundLevel(level);
                    return level;
                case Device.Fan:
                    return Amount(m, level);
                case Device.Servo:
                    return Media.IsWave(m) ? level / Trace.Peak : Amount(m, level) * 2f - 1f;
                default:
                    return level;
            }
        }

        /// <summary>The device's reading through the whole loop.</summary>
        public static float[] Reading(Device d, Trace t)
        {
            var r = new float[Trace.Steps];
            for (int k = 0; k < Trace.Steps; k++) r[k] = Read(d, t.Medium, t[k]);
            return r;
        }

        /// <summary>
        /// The colour a light shows at one level: a shape's colour (1 pink, 2 amber,
        /// 3 green), or -1 for the plain signal colour, which is also what a cyan
        /// shape and every other signal show.
        /// </summary>
        public static int LightColour(Medium m, float level) => m == Medium.Shape && Shapes.Colour((int)level) > 0 ? Shapes.Colour((int)level) : -1;

        /// <summary>True when the device does the same thing with both signals at every step.</summary>
        public static bool Same(Device d, Trace a, Trace b)
        {
            if (a == null || b == null) return false;
            if (d == Device.Display || d == Device.Speaker) return a.Matches(b);
            for (int k = 0; k < Trace.Steps; k++)
            {
                if (Math.Abs(Read(d, a.Medium, a[k]) - Read(d, b.Medium, b[k])) > Tolerance) return false;
                if (d == Device.Light && Read(d, a.Medium, a[k]) > 0f && LightColour(a.Medium, a[k]) != LightColour(b.Medium, b[k])) return false;
            }
            return true;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
