using System;
using System.Collections.Generic;

namespace Blackbox.Core
{
    /// <summary>
    /// What the player has named a box's behaviour: either one of the drawn glyphs
    /// or a few letters they typed.
    /// </summary>
    public struct RuleName : IEquatable<RuleName>
    {
        public string Glyph;
        public string Text;

        public bool IsEmpty => string.IsNullOrEmpty(Glyph) && string.IsNullOrEmpty(Text);

        public static RuleName FromGlyph(string glyph) => new RuleName { Glyph = glyph };
        public static RuleName FromText(string text) => new RuleName { Text = text };

        public string Encode() => !string.IsNullOrEmpty(Glyph) ? "g:" + Glyph : !string.IsNullOrEmpty(Text) ? "t:" + Text : string.Empty;

        public static RuleName Decode(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2) return default;
            if (s.StartsWith("g:", StringComparison.Ordinal)) return FromGlyph(s.Substring(2));
            if (s.StartsWith("t:", StringComparison.Ordinal)) return FromText(s.Substring(2));
            return default;
        }

        public bool Equals(RuleName other) => Glyph == other.Glyph && Text == other.Text;
        public override bool Equals(object obj) => obj is RuleName other && Equals(other);
        public override int GetHashCode() => (Glyph ?? "").GetHashCode() * 31 + (Text ?? "").GetHashCode();
    }

    /// <summary>Player preferences. Kept with the save so one file holds everything.</summary>
    public sealed class Preferences
    {
        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.5f;
        public float EffectsVolume = 0.8f;
        public bool ReduceMotion;
        public bool Fullscreen = true;
        /// <summary>Speeds up the signal animation when a circuit runs.</summary>
        public bool FastRuns;
    }

    /// <summary>
    /// Everything that persists between sessions. Plain C#, written as JSON by
    /// <see cref="ToJson"/> so the format is readable and survives renames.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public readonly HashSet<string> Solved = new HashSet<string>();
        /// <summary>Fewest test runs used to solve each circuit.</summary>
        public readonly Dictionary<string, int> BestTests = new Dictionary<string, int>();
        /// <summary>Box settings the player left each circuit on, so coming back resumes it.</summary>
        public readonly Dictionary<string, int[]> Settings = new Dictionary<string, int[]>();
        /// <summary>The player's names for box behaviours, keyed by "kind:setting".</summary>
        public readonly Dictionary<string, RuleName> Names = new Dictionary<string, RuleName>();
        /// <summary>Chapters whose title card has been shown.</summary>
        public readonly HashSet<string> SeenIntros = new HashSet<string>();
        public string LastPuzzle;
        public Preferences Prefs = new Preferences();

        public static string NameKey(string kind, int setting) => kind + ":" + setting;

        public RuleName GetName(string kind, int setting)
        {
            return Names.TryGetValue(NameKey(kind, setting), out var name) ? name : default;
        }

        public void SetName(string kind, int setting, RuleName name)
        {
            if (name.IsEmpty) Names.Remove(NameKey(kind, setting));
            else Names[NameKey(kind, setting)] = name;
        }

        public void RecordSolve(string puzzleId, int tests)
        {
            Solved.Add(puzzleId);
            if (!BestTests.TryGetValue(puzzleId, out int best) || tests < best) BestTests[puzzleId] = tests;
        }

        /// <summary>Clears progress and names but keeps preferences.</summary>
        public void ResetProgress()
        {
            Solved.Clear();
            BestTests.Clear();
            Settings.Clear();
            Names.Clear();
            SeenIntros.Clear();
            LastPuzzle = null;
        }

        public string ToJson()
        {
            var root = JsonValue.NewObject();
            root.Set("version", CurrentVersion);

            var solved = JsonValue.NewArray();
            foreach (var id in Sorted(Solved)) solved.Add(JsonValue.From(id));
            root.Set("solved", solved);

            var best = JsonValue.NewObject();
            foreach (var id in Sorted(BestTests.Keys)) best.Set(id, BestTests[id]);
            root.Set("bestTests", best);

            var settings = JsonValue.NewObject();
            foreach (var id in Sorted(Settings.Keys))
            {
                var arr = JsonValue.NewArray();
                foreach (int s in Settings[id]) arr.Add(JsonValue.From(s));
                settings.Set(id, arr);
            }
            root.Set("settings", settings);

            var names = JsonValue.NewObject();
            foreach (var key in Sorted(Names.Keys)) names.Set(key, Names[key].Encode());
            root.Set("names", names);

            var intros = JsonValue.NewArray();
            foreach (var id in Sorted(SeenIntros)) intros.Add(JsonValue.From(id));
            root.Set("seenIntros", intros);

            root.Set("lastPuzzle", JsonValue.From(LastPuzzle));

            var prefs = JsonValue.NewObject();
            prefs.Set("masterVolume", Prefs.MasterVolume);
            prefs.Set("musicVolume", Prefs.MusicVolume);
            prefs.Set("effectsVolume", Prefs.EffectsVolume);
            prefs.Set("reduceMotion", Prefs.ReduceMotion);
            prefs.Set("fullscreen", Prefs.Fullscreen);
            prefs.Set("fastRuns", Prefs.FastRuns);
            root.Set("preferences", prefs);

            return JsonWriter.Write(root);
        }

        /// <summary>Reads a save. Anything missing or damaged falls back to defaults rather than failing.</summary>
        public static SaveData FromJson(string json)
        {
            var data = new SaveData();
            if (string.IsNullOrWhiteSpace(json)) return data;
            var root = JsonValue.Parse(json);

            foreach (var v in root["solved"].Items) if (v.IsString) data.Solved.Add(v.AsString());
            foreach (var m in root["bestTests"].Members) if (m.Value.IsNumber) data.BestTests[m.Key] = m.Value.AsInt();
            foreach (var m in root["settings"].Members)
            {
                if (!m.Value.IsArray) continue;
                var arr = new int[m.Value.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = m.Value[i].AsInt();
                data.Settings[m.Key] = arr;
            }
            foreach (var m in root["names"].Members)
            {
                var name = RuleName.Decode(m.Value.AsString());
                if (!name.IsEmpty) data.Names[m.Key] = name;
            }
            foreach (var v in root["seenIntros"].Items) if (v.IsString) data.SeenIntros.Add(v.AsString());
            data.LastPuzzle = root["lastPuzzle"].AsString();

            var p = root["preferences"];
            var d = new Preferences();
            data.Prefs = new Preferences
            {
                MasterVolume = Clamp01(p["masterVolume"].AsFloat(d.MasterVolume)),
                MusicVolume = Clamp01(p["musicVolume"].AsFloat(d.MusicVolume)),
                EffectsVolume = Clamp01(p["effectsVolume"].AsFloat(d.EffectsVolume)),
                ReduceMotion = p["reduceMotion"].AsBool(d.ReduceMotion),
                Fullscreen = p["fullscreen"].AsBool(d.Fullscreen),
                FastRuns = p["fastRuns"].AsBool(d.FastRuns),
            };
            return data;
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        static List<string> Sorted(IEnumerable<string> items)
        {
            var list = new List<string>(items);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
