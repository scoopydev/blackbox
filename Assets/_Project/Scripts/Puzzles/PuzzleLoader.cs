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

            string mediumKey = Required(root, "medium", sourceName);
            if (!Media.TryParseMedium(mediumKey, out var medium))
                throw new PuzzleFormatException($"{sourceName}: unknown medium '{mediumKey}' (use binary, decimal, shape or wave).");

            var chapter = new ChapterDef
            {
                Id = Required(root, "id", sourceName),
                Numeral = root["numeral"].AsString(ToRoman(chapterNumber + 1)),
                Title = Required(root, "title", sourceName),
                Medium = medium,
                Tagline = root["tagline"].AsString(string.Empty),
                Signals = root["signals"].AsString(string.Empty),
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
                var puzzle = ReadPuzzle(c, $"{sourceName} › circuit {number + 1}", medium);
                if (!ids.Add(puzzle.Id)) throw new PuzzleFormatException($"{sourceName}: two circuits use the id '{puzzle.Id}'.");
                puzzle.Chapter = chapter;
                puzzle.Number = number++;
                chapter.Puzzles.Add(puzzle);
            }
            return chapter;
        }

        /// <summary>Reads one circuit. <paramref name="medium"/> applies unless the circuit names its own.</summary>
        public static PuzzleDef ReadPuzzle(JsonValue c, string where, Medium medium)
        {
            var p = new PuzzleDef
            {
                Id = Required(c, "id", where),
                Name = c["name"].AsString("Untitled"),
                Hint = c["hint"].AsString(),
                Goal = c["goal"].AsString(),
                Probes = c["probes"].AsInt(0),
                Medium = medium,
            };
            where = $"{where} ({p.Id})";
            if (c.Has("medium"))
            {
                if (!Media.TryParseMedium(c["medium"].AsString(), out var own))
                    throw new PuzzleFormatException($"{where}: unknown medium '{c["medium"].AsString()}'.");
                p.Medium = own;
            }
            if (p.Probes < 0) throw new PuzzleFormatException($"{where}: \"probes\" can't be negative.");

            ReadInputs(c["inputs"], p, where);
            ReadBlocks(c["blocks"], p, where);
            ReadOutputs(c["outputs"], p, where);

            if (p.Inputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one input.");
            if (p.Outputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one output.");

            var seen = new HashSet<string>();
            foreach (var n in p.AllNodes)
                if (!seen.Add(n.Id)) throw new PuzzleFormatException($"{where}: the id '{n.Id}' is used twice.");

            foreach (var w in c["wires"].Items)
            {
                var wire = ReadWire(w, p, where);
                wire.Index = p.Wires.Count;
                p.Wires.Add(wire);
            }
            CheckDrivers(p, where);
            CheckLoops(p, where);

            foreach (var n in p.AllNodes)
                if (n.Target.HasValue) p.Targets.Add(new TargetDef { Node = n, Want = n.Target.Value, Index = p.Targets.Count });

            BoardLayout.Apply(p);
            return p;
        }

        static void ReadInputs(JsonValue obj, PuzzleDef p, string where)
        {
            if (!obj.IsObject) throw new PuzzleFormatException($"{where}: \"inputs\" must be an object of id: input.");
            foreach (var m in obj.Members)
            {
                var node = new InputDef { Id = m.Key, Role = NodeRole.Input, Index = p.Inputs.Count };
                var v = m.Value;
                if (v.IsArray)
                {
                    ReadPosition(v, node, where);
                }
                else if (v.IsObject)
                {
                    ReadPosition(v["at"], node, where);
                    node.Value = v.Has("value") ? ReadValue(v["value"], p.Medium, $"input '{m.Key}' value", where) : 0;
                    node.Adjustable = v["adjustable"].AsBool(false);
                    node.Target = ReadTarget(v, p.Medium, $"input '{m.Key}'", where);
                }
                else throw new PuzzleFormatException($"{where}: input '{m.Key}' must be [column, row] or an object.");
                p.Inputs.Add(node);
            }
        }

        static void ReadBlocks(JsonValue obj, PuzzleDef p, string where)
        {
            if (obj.IsNull) return;
            if (!obj.IsObject) throw new PuzzleFormatException($"{where}: \"blocks\" must be an object of id: block.");
            foreach (var m in obj.Members)
            {
                var b = m.Value;
                string kindId = b["kind"].AsString();
                if (!KindCatalog.TryGet(kindId, out var kind))
                    throw new PuzzleFormatException($"{where}: block '{m.Key}' has unknown kind '{kindId}'.");
                if (kind.Medium != p.Medium)
                    throw new PuzzleFormatException($"{where}: block '{m.Key}' is a {kind.Id}, which works on {Media.Key(kind.Medium)} signals, but this circuit is {Media.Key(p.Medium)}.");
                var block = new BlockDef
                {
                    Id = m.Key,
                    Role = NodeRole.Block,
                    Kind = kind,
                    State = b["state"].AsInt(0),
                    Locked = b["locked"].AsBool(false),
                    Index = p.Blocks.Count,
                };
                if (block.State < 0 || block.State >= kind.StateCount)
                    throw new PuzzleFormatException($"{where}: block '{block.Id}' ({kind.Id}) has {kind.StateCount} states, so state {block.State} is out of range.");
                ReadPosition(b["at"], block, where);
                block.Target = ReadTarget(b, p.Medium, $"block '{m.Key}'", where);
                p.Blocks.Add(block);
            }
        }

        static void ReadOutputs(JsonValue obj, PuzzleDef p, string where)
        {
            if (!obj.IsObject) throw new PuzzleFormatException($"{where}: \"outputs\" must be an object of id: output.");
            foreach (var m in obj.Members)
            {
                var node = new OutputDef { Id = m.Key, Role = NodeRole.Output, Index = p.Outputs.Count };
                var v = m.Value;
                if (v.IsArray) ReadPosition(v, node, where);
                else if (v.IsObject)
                {
                    ReadPosition(v["at"], node, where);
                    node.Target = ReadTarget(v, p.Medium, $"output '{m.Key}'", where);
                }
                else throw new PuzzleFormatException($"{where}: output '{m.Key}' must be [column, row] or an object.");
                p.Outputs.Add(node);
            }
        }

        static int? ReadTarget(JsonValue node, Medium medium, string what, string where)
        {
            if (!node.Has("target") || node["target"].IsNull) return null;
            return ReadValue(node["target"], medium, what + " target", where);
        }

        static int ReadValue(JsonValue v, Medium medium, string what, string where)
        {
            string text = v.IsNumber ? v.AsInt().ToString() : v.AsString();
            if (text == null || !Media.TryParse(medium, text, out int value))
                throw new PuzzleFormatException($"{where}: {what} '{v}' isn't a {Media.Key(medium)} value. {Example(medium)}");
            return value;
        }

        static string Example(Medium m)
        {
            switch (m)
            {
                case Medium.Binary: return "Use 0 or 1.";
                case Medium.Decimal: return "Use 0 to 9.";
                case Medium.Shape: return "Use words like \"solid triangle\" or \"hollow hexagon\".";
                default: return "Use words like \"sine low\" or \"saw high\".";
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
            string text = w.AsString();
            if (text == null) throw new PuzzleFormatException($"{where}: each wire is a string like \"A -> b1.0\".");
            var parts = text.Split(new[] { "->" }, StringSplitOptions.None);
            if (parts.Length != 2) throw new PuzzleFormatException($"{where}: wire \"{text}\" must look like \"A -> b1.0\".");
            string from = parts[0].Trim();
            string to = parts[1].Trim();

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

            int portCount = target is BlockDef tb ? tb.Kind.InputCount : 1;
            if (port < 0)
            {
                if (portCount != 1)
                    throw new PuzzleFormatException($"{where}: {label}: '{targetId}' has {portCount} inputs, so name the port, like {targetId}.0 (top) or {targetId}.{portCount - 1} (bottom).");
                port = 0;
            }
            if (port >= portCount)
                throw new PuzzleFormatException($"{where}: {label}: '{targetId}' has no port {port}.");

            return new WireDef { From = source, To = target, ToPort = port };
        }

        static void CheckDrivers(PuzzleDef p, string where)
        {
            var driven = new HashSet<(NodeDef, int)>();
            foreach (var w in p.Wires)
                if (!driven.Add((w.To, w.ToPort)))
                    throw new PuzzleFormatException($"{where}: {w.To.Id} port {w.ToPort} has two wires running into it.");
            foreach (var b in p.Blocks)
                for (int port = 0; port < b.Kind.InputCount; port++)
                    if (!driven.Contains((b, port)))
                        throw new PuzzleFormatException($"{where}: block '{b.Id}' port {port} has nothing wired into it.");
            foreach (var o in p.Outputs)
                if (!driven.Contains((o, 0)))
                    throw new PuzzleFormatException($"{where}: output '{o.Id}' has nothing wired into it.");
        }

        static void CheckLoops(PuzzleDef p, string where)
        {
            var next = new Dictionary<NodeDef, List<NodeDef>>();
            foreach (var w in p.Wires)
            {
                if (!next.TryGetValue(w.From, out var list)) next[w.From] = list = new List<NodeDef>();
                list.Add(w.To);
            }
            var state = new Dictionary<NodeDef, int>();
            bool Visit(NodeDef n)
            {
                state.TryGetValue(n, out int s);
                if (s == 1) return false;
                if (s == 2) return true;
                state[n] = 1;
                if (next.TryGetValue(n, out var outs))
                    foreach (var o in outs)
                        if (!Visit(o)) return false;
                state[n] = 2;
                return true;
            }
            foreach (var n in p.AllNodes)
                if (!Visit(n)) throw new PuzzleFormatException($"{where}: the wires make a loop through '{n.Id}'. Signals must flow one way, from inputs to outputs.");
        }

        static string Required(JsonValue obj, string key, string where)
        {
            var s = obj[key].AsString();
            if (string.IsNullOrWhiteSpace(s)) throw new PuzzleFormatException($"{where}: missing \"{key}\".");
            return s;
        }

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
