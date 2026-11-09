using System;
using System.Collections.Generic;
using System.Text;
using Blackbox.Components;
using Blackbox.Core;

namespace Blackbox.Puzzles
{
    public sealed class PuzzleFormatException : Exception
    {
        public PuzzleFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Reads chapter files. See Docs/PuzzleFormat.md for the format, and the files in
    /// Puzzles/Resources/BlackboxPuzzles for examples.
    /// </summary>
    public static class PuzzleLoader
    {
        /// <summary>Reads the chapter index: the ordered list of chapter file names.</summary>
        public static List<string> ReadIndex(string json)
        {
            var root = JsonValue.Parse(json);
            var list = new List<string>();
            foreach (var item in root["chapters"].Items)
            {
                var name = item.AsString();
                if (string.IsNullOrWhiteSpace(name)) throw new PuzzleFormatException("chapters.json: every entry must be a file name.");
                list.Add(name.Trim());
            }
            if (list.Count == 0) throw new PuzzleFormatException("chapters.json lists no chapters.");
            return list;
        }

        public static ChapterDef ReadChapter(string json, string sourceName, int chapterNumber)
        {
            JsonValue root;
            try { root = JsonValue.Parse(json); }
            catch (FormatException e) { throw new PuzzleFormatException($"{sourceName}: {e.Message}"); }

            var chapter = new ChapterDef
            {
                Id = Required(root, "id", sourceName),
                Numeral = root["numeral"].AsString(ToRoman(chapterNumber + 1)),
                Title = Required(root, "title", sourceName),
                Tagline = root["tagline"].AsString(string.Empty),
                Number = chapterNumber,
            };

            var unlock = root["unlock"];
            if (unlock.IsObject)
            {
                chapter.UnlockAfter = unlock["after"].AsString();
                chapter.UnlockSolved = unlock["solved"].AsInt();
            }

            var circuits = root["circuits"];
            if (!circuits.IsArray || circuits.Count == 0)
                throw new PuzzleFormatException($"{sourceName}: \"circuits\" must be a non-empty array.");

            var ids = new HashSet<string>();
            int number = 0;
            foreach (var c in circuits.Items)
            {
                var puzzle = ReadPuzzle(c, $"{sourceName} › circuit {number + 1}");
                if (!ids.Add(puzzle.Id)) throw new PuzzleFormatException($"{sourceName}: two circuits use the id '{puzzle.Id}'.");
                puzzle.Chapter = chapter;
                puzzle.Number = number++;
                chapter.Puzzles.Add(puzzle);
            }
            return chapter;
        }

        public static PuzzleDef ReadPuzzle(JsonValue c, string where)
        {
            var p = new PuzzleDef
            {
                Id = Required(c, "id", where),
                Name = c["name"].AsString("Untitled"),
                Hint = c["hint"].AsString(),
            };
            where = $"{where} ({p.Id})";

            ReadNodes(c["inputs"], NodeRole.Input, p, where);
            ReadBoxes(c["boxes"], p, where);
            ReadNodes(c["outputs"], NodeRole.Output, p, where);

            if (p.Inputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one input.");
            if (p.Outputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one output.");

            var seen = new HashSet<string>();
            foreach (var n in p.AllNodes)
                if (!seen.Add(n.Id)) throw new PuzzleFormatException($"{where}: the id '{n.Id}' is used twice.");

            foreach (var w in c["wires"].Items) p.Wires.Add(ReadWire(w, p, where));
            CheckDrivers(p, where);

            bool hasTable = c.Has("table");
            bool hasTrace = c.Has("trace") || c.Has("traces");
            if (hasTable == hasTrace)
                throw new PuzzleFormatException($"{where}: give either a \"table\" or a \"trace\", not both or neither.");

            if (hasTable)
            {
                p.Mode = PuzzleMode.Table;
                foreach (var row in c["table"].Items) p.Cases.Add(ReadRow(row, p, where));
            }
            else
            {
                p.Mode = PuzzleMode.Trace;
                if (c.Has("trace")) p.Cases.Add(ReadTrace(c["trace"], p, where));
                foreach (var t in c["traces"].Items) p.Cases.Add(ReadTrace(t, p, where));
            }
            if (p.Cases.Count == 0) throw new PuzzleFormatException($"{where}: has no test cases.");
            BoardLayout.Apply(p);
            return p;
        }

        static void ReadNodes(JsonValue obj, NodeRole role, PuzzleDef p, string where)
        {
            if (!obj.IsObject) throw new PuzzleFormatException($"{where}: \"{RoleKey(role)}\" must be an object of id: position.");
            foreach (var m in obj.Members)
            {
                var node = new NodeDef { Id = m.Key, Role = role };
                ReadPosition(m.Value, node, where);
                if (role == NodeRole.Input) { node.Index = p.Inputs.Count; p.Inputs.Add(node); }
                else { node.Index = p.Outputs.Count; p.Outputs.Add(node); }
            }
        }

        static void ReadBoxes(JsonValue obj, PuzzleDef p, string where)
        {
            if (obj.IsNull) return;
            if (!obj.IsObject) throw new PuzzleFormatException($"{where}: \"boxes\" must be an object of id: box.");
            foreach (var m in obj.Members)
            {
                var b = m.Value;
                string kindId = b["kind"].AsString();
                if (!KindCatalog.TryGet(kindId, out var kind))
                    throw new PuzzleFormatException($"{where}: box '{m.Key}' has unknown kind '{kindId}'.");
                var box = new BoxDef
                {
                    Id = m.Key,
                    Role = NodeRole.Box,
                    Kind = kind,
                    Setting = b["setting"].AsInt(0),
                    Locked = b["locked"].AsBool(false),
                    Index = p.Boxes.Count,
                };
                if (box.Setting < 0 || box.Setting >= kind.SettingCount)
                    throw new PuzzleFormatException($"{where}: box '{box.Id}' ({kind.Id}) has {kind.SettingCount} settings, so setting {box.Setting} is out of range.");
                ReadPosition(b["at"], box, where);
                p.Boxes.Add(box);
            }
        }

        static void ReadPosition(JsonValue at, NodeDef node, string where)
        {
            if (at.IsNull) return;
            if (!at.IsArray || at.Count != 2 || !at[0].IsNumber || !at[1].IsNumber)
                throw new PuzzleFormatException($"{where}: '{node.Id}' position must be [column, row].");
            node.At = new GridPoint(at[0].AsFloat(), at[1].AsFloat());
            node.HasPosition = true;
        }

        static WireDef ReadWire(JsonValue w, PuzzleDef p, string where)
        {
            string from, to;
            float? via = null;
            if (w.IsString)
            {
                var parts = w.AsString().Split(new[] { "->" }, StringSplitOptions.None);
                if (parts.Length != 2) throw new PuzzleFormatException($"{where}: wire \"{w.AsString()}\" must look like \"A -> u1.0\".");
                from = parts[0].Trim();
                to = parts[1].Trim();
            }
            else if (w.IsObject)
            {
                from = w["from"].AsString("").Trim();
                to = w["to"].AsString("").Trim();
                if (w["via"].IsNumber) via = w["via"].AsFloat();
            }
            else throw new PuzzleFormatException($"{where}: each wire is a string like \"A -> u1.0\" or an object with from/to.");

            string label = $"wire '{from} -> {to}'";
            var source = p.Find(from);
            if (source == null) throw new PuzzleFormatException($"{where}: {label}: nothing is called '{from}'.");
            if (source.Role == NodeRole.Output) throw new PuzzleFormatException($"{where}: {label}: an output can't drive a wire.");

            string targetId = to;
            int port = -1;
            int dot = to.LastIndexOf('.');
            if (dot > 0 && int.TryParse(to.Substring(dot + 1), out int parsedPort))
            {
                targetId = to.Substring(0, dot);
                port = parsedPort;
            }
            var target = p.Find(targetId);
            if (target == null) throw new PuzzleFormatException($"{where}: {label}: nothing is called '{targetId}'.");
            if (target.Role == NodeRole.Input) throw new PuzzleFormatException($"{where}: {label}: wires can't run into an input.");

            int portCount = target is BoxDef tb ? tb.Kind.InputCount : 1;
            if (port < 0)
            {
                if (portCount != 1)
                    throw new PuzzleFormatException($"{where}: {label}: '{targetId}' has {portCount} inputs, so name the port, like {targetId}.0 (top) or {targetId}.{portCount - 1} (bottom).");
                port = 0;
            }
            if (port >= portCount)
                throw new PuzzleFormatException($"{where}: {label}: '{targetId}' has no port {port}.");

            return new WireDef { From = source, To = target, ToPort = port, ViaRow = via };
        }

        static void CheckDrivers(PuzzleDef p, string where)
        {
            var driven = new Dictionary<(NodeDef, int), WireDef>();
            foreach (var w in p.Wires)
            {
                if (driven.ContainsKey((w.To, w.ToPort)))
                    throw new PuzzleFormatException($"{where}: {w.To.Id} port {w.ToPort} has two wires running into it.");
                driven[(w.To, w.ToPort)] = w;
            }
            foreach (var b in p.Boxes)
                for (int port = 0; port < b.Kind.InputCount; port++)
                    if (!driven.ContainsKey((b, port)))
                        throw new PuzzleFormatException($"{where}: box '{b.Id}' port {port} has nothing wired into it.");
            foreach (var o in p.Outputs)
                if (!driven.ContainsKey((o, 0)))
                    throw new PuzzleFormatException($"{where}: output '{o.Id}' has nothing wired into it.");
        }

        static CaseDef ReadRow(JsonValue row, PuzzleDef p, string where)
        {
            string text = row.AsString();
            if (text == null) throw new PuzzleFormatException($"{where}: table rows are strings like \"01 > 1\".");
            var halves = text.Split('>');
            if (halves.Length != 2) throw new PuzzleFormatException($"{where}: table row \"{text}\" must look like \"01 > 1\".");
            string ins = Squash(halves[0]);
            string outs = Squash(halves[1]);
            if (ins.Length != p.Inputs.Count)
                throw new PuzzleFormatException($"{where}: table row \"{text}\" has {ins.Length} input values, but the circuit has {p.Inputs.Count} inputs.");
            if (outs.Length != p.Outputs.Count)
                throw new PuzzleFormatException($"{where}: table row \"{text}\" has {outs.Length} output values, but the circuit has {p.Outputs.Count} outputs.");

            var c = new CaseDef { Ticks = 1, Inputs = new[] { new bool[p.Inputs.Count] }, Want = new[] { new sbyte[p.Outputs.Count] } };
            for (int i = 0; i < ins.Length; i++) c.Inputs[0][i] = Bit(ins[i], false, text, where) == 1;
            for (int o = 0; o < outs.Length; o++) c.Want[0][o] = Bit(outs[o], true, text, where);
            return c;
        }

        static CaseDef ReadTrace(JsonValue t, PuzzleDef p, string where)
        {
            if (!t.IsObject) throw new PuzzleFormatException($"{where}: a trace is an object like {{ \"A\": \"0110\", \"X\": \"0011\" }}.");
            int ticks = -1;
            var lanes = new Dictionary<string, string>();
            foreach (var m in t.Members)
            {
                var node = p.Find(m.Key);
                if (node == null || node.Role == NodeRole.Box)
                    throw new PuzzleFormatException($"{where}: trace names '{m.Key}', which is not an input or output.");
                string lane = Squash(m.Value.AsString(""));
                if (ticks < 0) ticks = lane.Length;
                if (lane.Length != ticks)
                    throw new PuzzleFormatException($"{where}: every lane of a trace needs the same length ('{m.Key}' has {lane.Length}, expected {ticks}).");
                lanes[m.Key] = lane;
            }
            if (ticks <= 0) throw new PuzzleFormatException($"{where}: a trace needs at least one tick.");

            var c = new CaseDef { Ticks = ticks, Inputs = new bool[ticks][], Want = new sbyte[ticks][] };
            for (int tick = 0; tick < ticks; tick++)
            {
                c.Inputs[tick] = new bool[p.Inputs.Count];
                c.Want[tick] = new sbyte[p.Outputs.Count];
            }
            foreach (var input in p.Inputs)
            {
                if (!lanes.TryGetValue(input.Id, out var lane))
                    throw new PuzzleFormatException($"{where}: the trace is missing input '{input.Id}'.");
                for (int tick = 0; tick < ticks; tick++) c.Inputs[tick][input.Index] = Bit(lane[tick], false, lane, where) == 1;
            }
            foreach (var output in p.Outputs)
            {
                if (!lanes.TryGetValue(output.Id, out var lane))
                    throw new PuzzleFormatException($"{where}: the trace is missing output '{output.Id}'.");
                for (int tick = 0; tick < ticks; tick++) c.Want[tick][output.Index] = Bit(lane[tick], true, lane, where);
            }
            return c;
        }

        static sbyte Bit(char ch, bool allowDontCare, string context, string where)
        {
            if (ch == '0') return 0;
            if (ch == '1') return 1;
            if (allowDontCare && (ch == '-' || ch == '.')) return -1;
            throw new PuzzleFormatException($"{where}: '{ch}' in \"{context}\" is not a valid value{(allowDontCare ? " (use 0, 1 or - for don't care)" : " (use 0 or 1)")}.");
        }

        static string Squash(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s)
                if (!char.IsWhiteSpace(ch) && ch != '|' && ch != '_') sb.Append(ch);
            return sb.ToString();
        }

        static string Required(JsonValue obj, string key, string where)
        {
            var s = obj[key].AsString();
            if (string.IsNullOrWhiteSpace(s)) throw new PuzzleFormatException($"{where}: missing \"{key}\".");
            return s;
        }

        static string RoleKey(NodeRole role) => role == NodeRole.Input ? "inputs" : role == NodeRole.Output ? "outputs" : "boxes";

        public static string ToRoman(int n)
        {
            var numerals = new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
            var sb = new StringBuilder();
            foreach (var (value, letters) in numerals)
                while (n >= value) { sb.Append(letters); n -= value; }
            return sb.ToString();
        }
    }
}
