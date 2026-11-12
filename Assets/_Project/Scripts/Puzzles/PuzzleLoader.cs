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
        /// <summary>Reads the chapter index: the ordered list of chapter file names, across every part.</summary>
        public static List<string> ReadIndex(string json)
        {
            var list = new List<string>();
            foreach (var (_, files) in ReadParts(json)) list.AddRange(files);
            return list;
        }

        /// <summary>
        /// Reads the chapter index as parts, each with its chapter file names in
        /// play order. An index that lists "chapters" without parts is one part.
        /// </summary>
        public static List<(PartDef part, List<string> files)> ReadParts(string json)
        {
            JsonValue root;
            try { root = JsonValue.Parse(json); }
            catch (FormatException e) { throw new PuzzleFormatException($"chapters.json: {e.Message}"); }
            var parts = new List<(PartDef, List<string>)>();
            if (root["parts"].IsArray)
            {
                foreach (var item in root["parts"].Items)
                {
                    var part = new PartDef
                    {
                        Id = Required(item, "id", "chapters.json part"),
                        Title = Required(item, "title", "chapters.json part"),
                        Tagline = item["tagline"].AsString(string.Empty),
                        Number = parts.Count,
                        AfterPrevious = item["afterPrevious"].AsBool(false),
                    };
                    var files = ReadFileList(item["chapters"], $"chapters.json part '{part.Id}'");
                    parts.Add((part, files));
                }
                if (parts.Count == 0) throw new PuzzleFormatException("chapters.json lists no parts.");
                return parts;
            }
            var only = new PartDef { Id = "main", Title = "Circuits", Tagline = string.Empty };
            parts.Add((only, ReadFileList(root["chapters"], "chapters.json")));
            return parts;
        }

        static List<string> ReadFileList(JsonValue arr, string where)
        {
            var list = new List<string>();
            foreach (var item in arr.Items)
            {
                var name = item.AsString();
                if (string.IsNullOrWhiteSpace(name)) throw new PuzzleFormatException($"{where}: every chapter entry must be a file name.");
                list.Add(name.Trim());
            }
            if (list.Count == 0) throw new PuzzleFormatException($"{where} lists no chapters.");
            return list;
        }

        public static ChapterDef ReadChapter(string json, string sourceName, int chapterNumber)
        {
            JsonValue root;
            try { root = JsonValue.Parse(json); }
            catch (FormatException e) { throw new PuzzleFormatException($"{sourceName}: {e.Message}"); }

            bool wiring = ReadMode(root, sourceName, false);
            var medium = Medium.Binary;
            if (!wiring || root.Has("medium"))
            {
                string mediumKey = Required(root, "medium", sourceName);
                if (!Media.TryParseMedium(mediumKey, out medium))
                    throw new PuzzleFormatException($"{sourceName}: unknown medium '{mediumKey}' (use binary, decimal, shape, wave or sampled).");
            }

            var chapter = new ChapterDef
            {
                Id = Required(root, "id", sourceName),
                Numeral = root["numeral"].AsString(ToRoman(chapterNumber + 1)),
                Title = Required(root, "title", sourceName),
                Medium = medium,
                Tagline = root["tagline"].AsString(string.Empty),
                Signals = root["signals"].AsString(string.Empty),
                Number = chapterNumber,
                Wiring = wiring,
                Mixed = wiring && !root.Has("medium"),
                Dark = root["dark"].AsBool(false),
            };
            if (chapter.Dark && !wiring) throw new PuzzleFormatException($"{sourceName}: only a wired chapter can be \"dark\" (played by ear).");
            chapter.Guide = ReadGuide(root, sourceName, chapter);

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
                var puzzle = ReadPuzzle(c, $"{sourceName} › circuit {number + 1}", medium, wiring);
                if (!ids.Add(puzzle.Id)) throw new PuzzleFormatException($"{sourceName}: two circuits use the id '{puzzle.Id}'.");
                puzzle.Chapter = chapter;
                puzzle.Number = number++;
                puzzle.Name = NameOf(chapter, puzzle.Number);
                chapter.Puzzles.Add(puzzle);
            }
            return chapter;
        }

        /// <summary>A circuit's name: its chapter's title and its number, "TALLY 1".</summary>
        public static string NameOf(ChapterDef chapter, int number) => $"{chapter.Title.ToUpperInvariant()} {number + 1}";

        static readonly string[] GuideKeys = { "none", "bits", "digits", "changing-digits", "waves", "readings", "recap", "listening", "dark" };

        /// <summary>
        /// The chapter's signal guide: "guide" names one, or else it follows the
        /// chapter's signals (bits, digits, waves, or readings for every kind).
        /// </summary>
        static SignalGuide ReadGuide(JsonValue root, string sourceName, ChapterDef chapter)
        {
            if (root.Has("guide"))
            {
                string key = root["guide"].AsString(string.Empty);
                int i = System.Array.IndexOf(GuideKeys, key);
                if (i < 0) throw new PuzzleFormatException($"{sourceName}: unknown guide '{key}' (use {string.Join(", ", GuideKeys)}).");
                return (SignalGuide)i;
            }
            if (chapter.Mixed) return SignalGuide.Readings;
            switch (chapter.Medium)
            {
                case Medium.Binary: return SignalGuide.Bits;
                case Medium.Decimal: return SignalGuide.Digits;
                case Medium.Wave: return SignalGuide.Waves;
                default: return SignalGuide.None;
            }
        }

        /// <summary>
        /// Reads one circuit. <paramref name="medium"/> applies unless the circuit
        /// names its own; <paramref name="wiring"/>
        /// come from the chapter unless the circuit says otherwise.
        /// </summary>
        public static PuzzleDef ReadPuzzle(JsonValue c, string where, Medium medium, bool wiring = false)
        {
            var p = new PuzzleDef
            {
                Id = Required(c, "id", where),
                Name = c["name"].AsString(c["id"].AsString("Untitled")),
                Hint = c["hint"].AsString(),
                Goal = c["goal"].AsString(),
                Probes = c["probes"].AsInt(0),
                Medium = medium,
            };
            where = $"{where} ({p.Id})";
            p.Wiring = ReadMode(c, where, wiring);
            if (p.Wiring) return ReadWiredPuzzle(c, p, where);
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
            CheckDrivers(p.Wires, p, where);
            CheckLoops(p.Wires, p, where);

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
                    ReadOptions(v, node, p.Medium, where);
                }
                else throw new PuzzleFormatException($"{where}: input '{m.Key}' must be [column, row] or an object.");
                if (node.Adjustable && node.Options == null && Media.IsWave(p.Medium))
                    throw new PuzzleFormatException($"{where}: input '{m.Key}' is adjustable, so it needs \"options\": the waves it steps through.");
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

        static void ReadOptions(JsonValue v, InputDef node, Medium medium, string where)
        {
            if (!v.Has("options") || v["options"].IsNull) return;
            var list = v["options"];
            string what = $"input '{node.Id}'";
            if (!node.Adjustable) throw new PuzzleFormatException($"{where}: {what} has options but isn't adjustable.");
            if (!list.IsArray || list.Count < 2) throw new PuzzleFormatException($"{where}: {what} options must be a list of at least two values.");
            var options = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                options[i] = ReadValue(list[i], medium, $"{what} option {i + 1}", where);
                if (System.Array.IndexOf(options, options[i], 0, i) >= 0)
                    throw new PuzzleFormatException($"{where}: {what} lists {Media.Describe(medium, options[i])} twice.");
            }
            if (System.Array.IndexOf(options, node.Value) < 0)
                throw new PuzzleFormatException($"{where}: {what} starts at {Media.Describe(medium, node.Value)}, which isn't one of its options.");
            if (node.Target.HasValue && System.Array.IndexOf(options, node.Target.Value) < 0)
                throw new PuzzleFormatException($"{where}: {what} has a target of {Media.Describe(medium, node.Target.Value)}, which isn't one of its options.");
            node.Options = options;
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
                case Medium.Wave: return "Use the form and its settings, like \"sine f3 a5\" or \"saw f1 a7 p2\".";
                default: return "Use the form and its settings, like \"sine f3 a5 s6\" or \"saw f1 a7 s12\".";
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

        static void CheckDrivers(List<WireDef> wires, PuzzleDef p, string where)
        {
            var driven = new HashSet<(NodeDef, int)>();
            foreach (var w in wires)
                if (!driven.Add((w.To, w.ToPort)))
                    throw new PuzzleFormatException($"{where}: {w.To.Id} port {w.ToPort} has two wires running into it.");
            foreach (var b in p.Blocks)
            {
                // An optional block may be left out of the answer, with no wires in at all.
                bool any = false;
                for (int port = 0; port < b.Kind.InputCount; port++) any |= driven.Contains((b, port));
                if (b.Optional && !any) continue;
                for (int port = 0; port < b.Kind.InputCount; port++)
                    if (!driven.Contains((b, port)))
                        throw new PuzzleFormatException($"{where}: block '{b.Id}' port {port} has nothing wired into it.");
            }
            foreach (var o in p.Outputs)
                if (!driven.Contains((o, 0)))
                    throw new PuzzleFormatException($"{where}: output '{o.Id}' has nothing wired into it.");
        }

        static void CheckLoops(List<WireDef> wires, PuzzleDef p, string where)
        {
            var next = new Dictionary<NodeDef, List<NodeDef>>();
            foreach (var w in wires)
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

        static bool ReadMode(JsonValue obj, string where, bool fallback)
        {
            if (!obj.Has("mode")) return fallback;
            string mode = obj["mode"].AsString();
            if (mode == "wiring") return true;
            if (mode == "settings") return false;
            throw new PuzzleFormatException($"{where}: \"mode\" must be \"wiring\" (the player draws the wires) or \"settings\" (the player changes inputs and blocks).");
        }

        /// <summary>
        /// A circuit the player wires. Inputs carry signals of any type, blocks keep
        /// their state, outputs have no targets of their own: what each must get is
        /// worked out from <c>"solution"</c>, the designer's wiring, which the board
        /// never shows. See Docs/PuzzleFormat.md.
        /// </summary>
        static PuzzleDef ReadWiredPuzzle(JsonValue c, PuzzleDef p, string where)
        {
            if (p.Probes != 0) throw new PuzzleFormatException($"{where}: a wired circuit has no probes; the player wires a block to an output to test it.");

            var inputs = c["inputs"];
            if (!inputs.IsObject) throw new PuzzleFormatException($"{where}: \"inputs\" must be an object of id: input.");
            foreach (var m in inputs.Members)
            {
                var node = new InputDef { Id = m.Key, Role = NodeRole.Input, Index = p.Inputs.Count };
                var v = m.Value;
                string text = v.IsString ? v.AsString() : v.IsObject ? v["signal"].AsString() : null;
                if (v.IsObject) ReadPosition(v["at"], node, where);
                if (text == null) throw new PuzzleFormatException($"{where}: input '{m.Key}' needs a \"signal\", like \"binary 0110\" or \"wave sine f2 a7\".");
                if (!Signals.TryParse(text, out node.Signal, out string error))
                    throw new PuzzleFormatException($"{where}: input '{m.Key}': {error}.");
                if (KindCatalog.MediumShelved(node.Signal.Medium))
                    throw new PuzzleFormatException($"{where}: input '{m.Key}': {Media.Key(node.Signal.Medium)} signals are set aside for now, so no circuit may use them.");
                if (v.IsObject && (v.Has("adjustable") || v.Has("target") || v.Has("value")))
                    throw new PuzzleFormatException($"{where}: input '{m.Key}': inputs of a wired circuit are fixed signals, so they take no \"value\", \"adjustable\" or \"target\".");
                // Inputs are optional, and dotted, unless the circuit needs one: then it's solid and must be used.
                if (v.IsObject && v.Has("optional"))
                    throw new PuzzleFormatException($"{where}: input '{m.Key}': inputs are optional unless marked \"required\": true.");
                node.Optional = !(v.IsObject && v["required"].AsBool(false));
                p.Inputs.Add(node);
            }

            var blocks = c["blocks"];
            if (!blocks.IsObject || blocks.Count == 0) throw new PuzzleFormatException($"{where}: a wired circuit needs at least one block.");
            foreach (var m in blocks.Members)
            {
                var b = m.Value;
                string kindId = b.IsString ? b.AsString() : b["kind"].AsString();
                if (!KindCatalog.TryGet(kindId, out var kind))
                    throw new PuzzleFormatException($"{where}: block '{m.Key}' has unknown kind '{kindId}'.");
                if (b.IsObject && b.Has("state"))
                    throw new PuzzleFormatException($"{where}: block '{m.Key}' has a \"state\", but blocks have no settings: each kind does one thing.");
                if (kind.Shelved)
                    throw new PuzzleFormatException($"{where}: block '{m.Key}' is a {kind.Id}, which is set aside for now, so no circuit may use it.");
                var block = new BlockDef
                {
                    Id = m.Key,
                    Role = NodeRole.Block,
                    Kind = kind,
                    Locked = true,
                    Index = p.Blocks.Count,
                };
                if (b.IsObject)
                {
                    ReadPosition(b["at"], block, where);
                    if (b.Has("target")) throw new PuzzleFormatException($"{where}: block '{block.Id}': in a wired circuit only outputs have targets.");
                    block.Optional = b["optional"].AsBool(false);
                    // A listen-only block's signal is heard, never drawn.
                    block.Listen = b["listen"].AsBool(false);
                }
                p.Blocks.Add(block);
            }

            var outputs = c["outputs"];
            if (!outputs.IsObject) throw new PuzzleFormatException($"{where}: \"outputs\" must be an object of id: output.");
            foreach (var m in outputs.Members)
            {
                var node = new OutputDef { Id = m.Key, Role = NodeRole.Output, Index = p.Outputs.Count };
                var v = m.Value;
                if (v.IsArray) ReadPosition(v, node, where);
                else if (v.IsObject)
                {
                    ReadPosition(v["at"], node, where);
                    if (v.Has("target")) throw new PuzzleFormatException($"{where}: output '{m.Key}': a wired circuit's targets come from its \"solution\".");
                    if (v.Has("device") && !Devices.TryParse(v["device"].AsString(), out node.Device))
                        throw new PuzzleFormatException($"{where}: output '{m.Key}' has unknown device '{v["device"].AsString()}' (use display, light, speaker, digits, fan or servo).");
                }
                else if (v.IsString)
                {
                    if (!Devices.TryParse(v.AsString(), out node.Device))
                        throw new PuzzleFormatException($"{where}: output '{m.Key}' has unknown device '{v.AsString()}' (use display, light, speaker, digits, fan or servo).");
                }
                else if (!v.IsNull) throw new PuzzleFormatException($"{where}: output '{m.Key}' must be [column, row], a device name or an object.");
                if (v.IsObject && v.Has("optional")) throw new PuzzleFormatException($"{where}: output '{m.Key}': only inputs and blocks can be optional; every output has to match.");
                if (Devices.Shelved.Contains(node.Device))
                    throw new PuzzleFormatException($"{where}: output '{m.Key}' is a {Devices.Noun(node.Device)}, which is set aside for now: every output is a display.");
                p.Outputs.Add(node);
            }

            if (p.Inputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one input.");
            if (p.Outputs.Count == 0) throw new PuzzleFormatException($"{where}: needs at least one output.");
            var seen = new HashSet<string>();
            foreach (var n in p.AllNodes)
                if (!seen.Add(n.Id)) throw new PuzzleFormatException($"{where}: the id '{n.Id}' is used twice.");

            if (c.Has("wires") && c["wires"].Count > 0)
                throw new PuzzleFormatException($"{where}: a wired circuit's \"wires\" would be fixed. Put the wires the player starts with in \"start\", and the intended wiring in \"solution\".");
            if (!c["solution"].IsArray || c["solution"].Count == 0)
                throw new PuzzleFormatException($"{where}: a wired circuit needs a \"solution\": the wiring its targets come from.");
            foreach (var w in c["solution"].Items)
            {
                var wire = ReadWire(w, p, where);
                wire.Index = p.Solution.Count;
                p.Solution.Add(wire);
            }
            CheckDrivers(p.Solution, p, $"{where} solution");
            CheckLoops(p.Solution, p, $"{where} solution");

            if (c.Has("start"))
            {
                if (!c["start"].IsArray) throw new PuzzleFormatException($"{where}: \"start\" is a list of wires, like [\"A -> g.0\"].");
                var taken = new HashSet<(NodeDef, int)>();
                foreach (var w in c["start"].Items)
                {
                    var wire = ReadWire(w, p, where + " start");
                    if (!taken.Add((wire.To, wire.ToPort)))
                        throw new PuzzleFormatException($"{where} start: {wire.To.Id} port {wire.ToPort} has two wires running into it.");
                    wire.Index = p.Start.Count;
                    p.Start.Add(wire);
                }
                CheckLoops(p.Start, p, $"{where} start");
            }

            var values = WiringSession.Evaluate(p, p.Solution);
            foreach (var o in p.Outputs)
                o.Want = values[p.Inputs.Count + p.Blocks.Count + o.Index]
                    ?? throw new PuzzleFormatException($"{where}: the solution sends nothing to output '{o.Id}'.");
            // The answer uses every required input and block. It may use an optional
            // block or leave it out, but a block it wires up has to lead somewhere.
            var used = WiringSession.UsedNodes(p, p.Solution);
            for (int i = 0; i < p.Inputs.Count; i++)
                if (!p.Inputs[i].Optional && !used[i])
                    throw new PuzzleFormatException($"{where}: input '{p.Inputs[i].Id}' is required, but the solution doesn't use it.");
            for (int b = 0; b < p.Blocks.Count; b++)
            {
                var block = p.Blocks[b];
                bool on = used[p.Inputs.Count + b];
                bool wired = p.Solution.Exists(w => w.To == block);
                if (!on && !block.Optional)
                    throw new PuzzleFormatException($"{where}: the solution doesn't use block '{block.Id}': every block must lead to an output, unless it's marked \"optional\": true.");
                if (!on && wired)
                    throw new PuzzleFormatException($"{where}: the solution wires optional block '{block.Id}' up, but it leads nowhere. Leave it out, or use it.");
            }

            BoardLayout.ApplyWiring(p);
            LabelBlocks(p);
            p.Hint = NameBlocks(p.Hint, p, where + " hint");
            p.Goal = NameBlocks(p.Goal, p, where + " goal");
            return p;
        }

        /// <summary>
        /// Labels each block by the kind of signal it sends (<see cref="Media.Letter"/>:
        /// B, D or W) and its number among those, counting down each column of the
        /// board in turn: B1, B2, D1, W1 ...
        /// </summary>
        public static void LabelBlocks(PuzzleDef p)
        {
            var order = new List<BlockDef>(p.Blocks);
            order.Sort((a, b) => a.At.Col != b.At.Col ? a.At.Col.CompareTo(b.At.Col) : a.At.Row != b.At.Row ? a.At.Row.CompareTo(b.At.Row) : a.Index.CompareTo(b.Index));
            var counts = new Dictionary<Medium, int>();
            foreach (var b in order)
            {
                counts.TryGetValue(b.Kind.Medium, out int n);
                counts[b.Kind.Medium] = ++n;
                b.Label = Media.Letter(b.Kind.Medium) + n;
            }
        }

        /// <summary>
        /// Writes each "{id}" in a hint or goal as the block's label ("{g}" becomes
        /// "B1"), so text never has to know how the blocks are numbered.
        /// </summary>
        static string NameBlocks(string text, PuzzleDef p, string where)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}]*)\}", m =>
            {
                var block = p.Blocks.Find(b => b.Id == m.Groups[1].Value);
                if (block == null) throw new PuzzleFormatException($"{where}: \"{m.Value}\" doesn't name a block.");
                return block.Label;
            });
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
