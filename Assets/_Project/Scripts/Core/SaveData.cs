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
        /// <summary>Play a signal's sound when an input is set and as a run shows each output.</summary>
        public bool SignalSounds = true;
        /// <summary>Debug: write what every block does, in its current state, under it.</summary>
        public bool DebugRules;
        /// <summary>Debug: open every part, chapter and circuit, whatever has been solved.</summary>
        public bool DebugOpenAll;
        /// <summary>How fast the signals on screen loop: 1x to 4x.</summary>
        public int LoopSpeed = 1;
        /// <summary>How fast a run plays out: 1x to 4x.</summary>
        public int RunSpeed = 1;
    }

    /// <summary>Where the player left one circuit: input values, block states and probes, or the wires they drew.</summary>
    public sealed class CircuitState
    {
        public int[] Inputs = Array.Empty<int>();
        public int[] States = Array.Empty<int>();
        /// <summary>Each probe as (wire index, position along the wire in thousandths).</summary>
        public readonly List<(int wire, int at)> Probes = new List<(int, int)>();
        /// <summary>A wired circuit's wires, like "A -> g.0".</summary>
        public readonly List<string> Wires = new List<string>();
        /// <summary>Blocks the player moved, by block id: where each sits, in board coordinates.</summary>
        public readonly Dictionary<string, (int x, int y)> Places = new Dictionary<string, (int x, int y)>();
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

    /// <summary>A typed note: its text, its colour, and where its top-left corner sits, in frame coordinates (y down).</summary>
    public sealed class TextNote
    {
        public int X, Y;
        public int Color;
        public string Text = string.Empty;
        /// <summary>The id of the block this note labels, or null for a note placed freely on the circuit.</summary>
        public string Block;
    }

    /// <summary>
    /// Everything that persists between sessions. Plain C#, written as JSON by
    /// <see cref="ToJson"/> so the format is readable and survives renames.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>
        /// Version 3 is the wiring game, where the player draws every circuit's
        /// wires. Progress from older versions doesn't carry over, only preferences.
        /// </summary>
        public const int CurrentVersion = 3;

        public readonly HashSet<string> Solved = new HashSet<string>();
        /// <summary>Fewest changes used to solve each circuit.</summary>
        public readonly Dictionary<string, int> BestMoves = new Dictionary<string, int>();
        /// <summary>Where the player left each circuit, so coming back resumes it.</summary>
        public readonly Dictionary<string, CircuitState> Circuits = new Dictionary<string, CircuitState>();
        /// <summary>The player's pen notes on each circuit.</summary>
        public readonly Dictionary<string, List<InkStroke>> Ink = new Dictionary<string, List<InkStroke>>();
        /// <summary>The player's typed notes on each circuit.</summary>
        public readonly Dictionary<string, List<TextNote>> Notes = new Dictionary<string, List<TextNote>>();
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

        public List<TextNote> NotesFor(string puzzleId)
        {
            if (!Notes.TryGetValue(puzzleId, out var list)) Notes[puzzleId] = list = new List<TextNote>();
            return list;
        }

        /// <summary>Clears progress, circuits and notes but keeps preferences.</summary>
        public void ResetProgress()
        {
            Solved.Clear();
            BestMoves.Clear();
            Circuits.Clear();
            Ink.Clear();
            Notes.Clear();
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
                if (c.Wires.Count > 0)
                {
                    var wires = JsonValue.NewArray();
                    foreach (var w in c.Wires) wires.Add(JsonValue.From(w));
                    obj.Set("wires", wires);
                }
                if (c.Places.Count > 0)
                {
                    var places = JsonValue.NewObject();
                    foreach (var block in Sorted(c.Places.Keys))
                    {
                        var pair = JsonValue.NewArray();
                        pair.Add(JsonValue.From(c.Places[block].x));
                        pair.Add(JsonValue.From(c.Places[block].y));
                        places.Set(block, pair);
                    }
                    obj.Set("places", places);
                }
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

            var notes = JsonValue.NewObject();
            foreach (var id in Sorted(Notes.Keys))
            {
                var arr = JsonValue.NewArray();
                foreach (var note in Notes[id])
                {
                    if (string.IsNullOrWhiteSpace(note.Text)) continue;
                    var obj = JsonValue.NewObject();
                    obj.Set("x", note.X);
                    obj.Set("y", note.Y);
                    obj.Set("c", note.Color);
                    if (note.Block != null) obj.Set("b", note.Block);
                    obj.Set("t", JsonValue.From(note.Text));
                    arr.Add(obj);
                }
                if (arr.Count > 0) notes.Set(id, arr);
            }
            root.Set("notes", notes);

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
            prefs.Set("signalSounds", Prefs.SignalSounds);
            prefs.Set("debugRules", Prefs.DebugRules);
            prefs.Set("debugOpenAll", Prefs.DebugOpenAll);
            prefs.Set("loopSpeed", Prefs.LoopSpeed);
            prefs.Set("runSpeed", Prefs.RunSpeed);
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

            if (version >= CurrentVersion)
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
                    foreach (var w in m.Value["wires"].Items)
                        if (w.IsString) c.Wires.Add(w.AsString());
                    foreach (var place in m.Value["places"].Members)
                        if (place.Value.IsArray && place.Value.Count == 2)
                            c.Places[place.Key] = (place.Value[0].AsInt(), place.Value[1].AsInt());
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
                foreach (var m in root["notes"].Members)
                {
                    var list = new List<TextNote>();
                    foreach (var n in m.Value.Items)
                    {
                        if (!n.IsObject || !n["t"].IsString || string.IsNullOrWhiteSpace(n["t"].AsString())) continue;
                        list.Add(new TextNote { X = n["x"].AsInt(), Y = n["y"].AsInt(), Color = n["c"].AsInt(), Text = n["t"].AsString(), Block = n["b"].IsString ? n["b"].AsString() : null });
                    }
                    if (list.Count > 0) data.Notes[m.Key] = list;
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
                SignalSounds = p["signalSounds"].AsBool(d.SignalSounds),
                DebugRules = p["debugRules"].AsBool(d.DebugRules),
                DebugOpenAll = p["debugOpenAll"].AsBool(d.DebugOpenAll),
                LoopSpeed = Math.Max(1, Math.Min(4, p["loopSpeed"].AsInt(d.LoopSpeed))),
                RunSpeed = Math.Max(1, Math.Min(4, p["runSpeed"].AsInt(d.RunSpeed))),
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
