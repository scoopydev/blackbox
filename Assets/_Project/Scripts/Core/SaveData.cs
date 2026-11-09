using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Blackbox.Core
{
    /// <summary>Player preferences. Kept with the save so one file holds everything.</summary>
    public sealed class Preferences
    {
        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.5f;
        public float EffectsVolume = 0.8f;
        public bool ReduceMotion;
        public bool Fullscreen = true;
    }

    /// <summary>Where the player left one circuit: input values, block states and probes.</summary>
    public sealed class CircuitState
    {
        public int[] Inputs = Array.Empty<int>();
        public int[] States = Array.Empty<int>();
        /// <summary>Each probe as (wire index, position along the wire in thousandths).</summary>
        public readonly List<(int wire, int at)> Probes = new List<(int, int)>();
    }

    /// <summary>One pen stroke: a colour and points in frame coordinates (x, y, x, y, ...).</summary>
    public sealed class InkStroke
    {
        public int Color;
        public readonly List<int> Points = new List<int>();

        public int PointCount => Points.Count / 2;

        /// <summary>"1:12,40 14,42 ..."</summary>
        public string Encode()
        {
            var sb = new StringBuilder();
            sb.Append(Color.ToString(CultureInfo.InvariantCulture)).Append(':');
            for (int i = 0; i + 1 < Points.Count; i += 2)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Points[i].ToString(CultureInfo.InvariantCulture)).Append(',').Append(Points[i + 1].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static InkStroke Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int colon = s.IndexOf(':');
            if (colon <= 0) return null;
            if (!int.TryParse(s.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int color)) return null;
            var stroke = new InkStroke { Color = color };
            foreach (var pair in s.Substring(colon + 1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int comma = pair.IndexOf(',');
                if (comma <= 0) continue;
                if (int.TryParse(pair.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                    int.TryParse(pair.Substring(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    stroke.Points.Add(x);
                    stroke.Points.Add(y);
                }
            }
            return stroke.Points.Count >= 2 ? stroke : null;
        }
    }

    /// <summary>
    /// Everything that persists between sessions. Plain C#, written as JSON by
    /// <see cref="ToJson"/> so the format is readable and survives renames.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>Version 2 is the redesign with probes, the pen and signal media. Older circuits don't carry over.</summary>
        public const int CurrentVersion = 2;

        public readonly HashSet<string> Solved = new HashSet<string>();
        /// <summary>Fewest changes used to solve each circuit.</summary>
        public readonly Dictionary<string, int> BestMoves = new Dictionary<string, int>();
        /// <summary>Where the player left each circuit, so coming back resumes it.</summary>
        public readonly Dictionary<string, CircuitState> Circuits = new Dictionary<string, CircuitState>();
        /// <summary>The player's pen notes on each circuit.</summary>
        public readonly Dictionary<string, List<InkStroke>> Ink = new Dictionary<string, List<InkStroke>>();
        /// <summary>Chapters whose title card has been shown.</summary>
        public readonly HashSet<string> SeenIntros = new HashSet<string>();
        public string LastPuzzle;
        public Preferences Prefs = new Preferences();

        public void RecordSolve(string puzzleId, int moves)
        {
            Solved.Add(puzzleId);
            if (!BestMoves.TryGetValue(puzzleId, out int best) || moves < best) BestMoves[puzzleId] = moves;
        }

        public List<InkStroke> InkFor(string puzzleId)
        {
            if (!Ink.TryGetValue(puzzleId, out var list)) Ink[puzzleId] = list = new List<InkStroke>();
            return list;
        }

        /// <summary>Clears progress, circuits and notes but keeps preferences.</summary>
        public void ResetProgress()
        {
            Solved.Clear();
            BestMoves.Clear();
            Circuits.Clear();
            Ink.Clear();
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
            foreach (var id in Sorted(BestMoves.Keys)) best.Set(id, BestMoves[id]);
            root.Set("bestMoves", best);

            var circuits = JsonValue.NewObject();
            foreach (var id in Sorted(Circuits.Keys))
            {
                var c = Circuits[id];
                var obj = JsonValue.NewObject();
                obj.Set("inputs", IntArray(c.Inputs));
                obj.Set("states", IntArray(c.States));
                var probes = JsonValue.NewArray();
                foreach (var (wire, at) in c.Probes)
                {
                    var pair = JsonValue.NewArray();
                    pair.Add(JsonValue.From(wire));
                    pair.Add(JsonValue.From(at));
                    probes.Add(pair);
                }
                obj.Set("probes", probes);
                circuits.Set(id, obj);
            }
            root.Set("circuits", circuits);

            var ink = JsonValue.NewObject();
            foreach (var id in Sorted(Ink.Keys))
            {
                if (Ink[id].Count == 0) continue;
                var arr = JsonValue.NewArray();
                foreach (var stroke in Ink[id]) arr.Add(JsonValue.From(stroke.Encode()));
                ink.Set(id, arr);
            }
            root.Set("ink", ink);

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
            root.Set("preferences", prefs);

            return JsonWriter.Write(root);
        }

        /// <summary>
        /// Reads a save. Anything missing or damaged falls back to defaults rather than
        /// failing. A save from before the redesign keeps only its preferences.
        /// </summary>
        public static SaveData FromJson(string json)
        {
            var data = new SaveData();
            if (string.IsNullOrWhiteSpace(json)) return data;
            var root = JsonValue.Parse(json);
            int version = root["version"].AsInt(1);

            if (version >= 2)
            {
                foreach (var v in root["solved"].Items) if (v.IsString) data.Solved.Add(v.AsString());
                foreach (var m in root["bestMoves"].Members) if (m.Value.IsNumber) data.BestMoves[m.Key] = m.Value.AsInt();
                foreach (var m in root["circuits"].Members)
                {
                    if (!m.Value.IsObject) continue;
                    var c = new CircuitState
                    {
                        Inputs = ReadInts(m.Value["inputs"]),
                        States = ReadInts(m.Value["states"]),
                    };
                    foreach (var pair in m.Value["probes"].Items)
                        if (pair.IsArray && pair.Count == 2) c.Probes.Add((pair[0].AsInt(), pair[1].AsInt()));
                    data.Circuits[m.Key] = c;
                }
                foreach (var m in root["ink"].Members)
                {
                    var list = new List<InkStroke>();
                    foreach (var s in m.Value.Items)
                    {
                        var stroke = InkStroke.Decode(s.AsString());
                        if (stroke != null) list.Add(stroke);
                    }
                    if (list.Count > 0) data.Ink[m.Key] = list;
                }
                foreach (var v in root["seenIntros"].Items) if (v.IsString) data.SeenIntros.Add(v.AsString());
                data.LastPuzzle = root["lastPuzzle"].AsString();
            }

            var p = root["preferences"];
            var d = new Preferences();
            data.Prefs = new Preferences
            {
                MasterVolume = Clamp01(p["masterVolume"].AsFloat(d.MasterVolume)),
                MusicVolume = Clamp01(p["musicVolume"].AsFloat(d.MusicVolume)),
                EffectsVolume = Clamp01(p["effectsVolume"].AsFloat(d.EffectsVolume)),
                ReduceMotion = p["reduceMotion"].AsBool(d.ReduceMotion),
                Fullscreen = p["fullscreen"].AsBool(d.Fullscreen),
            };
            return data;
        }

        static JsonValue IntArray(int[] values)
        {
            var arr = JsonValue.NewArray();
            foreach (int v in values) arr.Add(JsonValue.From(v));
            return arr;
        }

        static int[] ReadInts(JsonValue arr)
        {
            if (!arr.IsArray) return Array.Empty<int>();
            var result = new int[arr.Count];
            for (int i = 0; i < result.Length; i++) result[i] = arr[i].AsInt();
            return result;
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
