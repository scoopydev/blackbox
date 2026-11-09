using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Blackbox.Core
{
    public enum JsonType { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// A small JSON document model. Objects keep their key order, which matters for
    /// puzzle files (the order of inputs and outputs is the order of table columns).
    /// This lives in plain C# so puzzle content and save files load the same way
    /// in the game, in edit mode tests and in offline tools.
    /// </summary>
    public sealed class JsonValue
    {
        public static readonly JsonValue Null = new JsonValue(JsonType.Null);

        public JsonType Type { get; }
        public int Line { get; internal set; }

        readonly bool _bool;
        readonly double _number;
        readonly string _string;
        readonly List<JsonValue> _items;
        readonly List<KeyValuePair<string, JsonValue>> _members;

        JsonValue(JsonType type)
        {
            Type = type;
            if (type == JsonType.Array) _items = new List<JsonValue>();
            if (type == JsonType.Object) _members = new List<KeyValuePair<string, JsonValue>>();
        }

        JsonValue(bool value) : this(JsonType.Bool) { _bool = value; }
        JsonValue(double value) : this(JsonType.Number) { _number = value; }
        JsonValue(string value) : this(JsonType.String) { _string = value ?? string.Empty; }

        public static JsonValue From(bool value) => new JsonValue(value);
        public static JsonValue From(double value) => new JsonValue(value);
        public static JsonValue From(int value) => new JsonValue(value);
        public static JsonValue From(string value) => value == null ? Null : new JsonValue(value);
        public static JsonValue NewArray() => new JsonValue(JsonType.Array);
        public static JsonValue NewObject() => new JsonValue(JsonType.Object);

        public bool IsNull => Type == JsonType.Null;
        public bool IsObject => Type == JsonType.Object;
        public bool IsArray => Type == JsonType.Array;
        public bool IsString => Type == JsonType.String;
        public bool IsNumber => Type == JsonType.Number;
        public bool IsBool => Type == JsonType.Bool;

        public int Count => Type == JsonType.Array ? _items.Count : Type == JsonType.Object ? _members.Count : 0;

        public JsonValue this[int index] => Type == JsonType.Array && index >= 0 && index < _items.Count ? _items[index] : Null;

        public JsonValue this[string key]
        {
            get
            {
                if (Type != JsonType.Object) return Null;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i].Key == key) return _members[i].Value;
                return Null;
            }
        }

        public bool Has(string key)
        {
            if (Type != JsonType.Object) return false;
            for (int i = 0; i < _members.Count; i++)
                if (_members[i].Key == key) return true;
            return false;
        }

        public IEnumerable<JsonValue> Items
        {
            get
            {
                if (Type != JsonType.Array) yield break;
                foreach (var item in _items) yield return item;
            }
        }

        public IEnumerable<KeyValuePair<string, JsonValue>> Members
        {
            get
            {
                if (Type != JsonType.Object) yield break;
                foreach (var member in _members) yield return member;
            }
        }

        public string AsString(string fallback = null) => Type == JsonType.String ? _string : fallback;
        public double AsNumber(double fallback = 0) => Type == JsonType.Number ? _number : fallback;
        public int AsInt(int fallback = 0) => Type == JsonType.Number ? (int)Math.Round(_number) : fallback;
        public float AsFloat(float fallback = 0) => Type == JsonType.Number ? (float)_number : fallback;
        public bool AsBool(bool fallback = false) => Type == JsonType.Bool ? _bool : fallback;

        public JsonValue Add(JsonValue item)
        {
            if (Type != JsonType.Array) throw new InvalidOperationException("Add needs a JSON array.");
            _items.Add(item ?? Null);
            return this;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (Type != JsonType.Object) throw new InvalidOperationException("Set needs a JSON object.");
            value = value ?? Null;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Key != key) continue;
                _members[i] = new KeyValuePair<string, JsonValue>(key, value);
                return this;
            }
            _members.Add(new KeyValuePair<string, JsonValue>(key, value));
            return this;
        }

        public JsonValue Set(string key, string value) => Set(key, From(value));
        public JsonValue Set(string key, int value) => Set(key, From(value));
        public JsonValue Set(string key, double value) => Set(key, From(value));
        public JsonValue Set(string key, bool value) => Set(key, From(value));

        public override string ToString() => JsonWriter.Write(this, false);

        // ------------------------------------------------------------------ parsing

        public static JsonValue Parse(string text)
        {
            var parser = new Parser(text ?? string.Empty);
            return parser.ParseDocument();
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;
            int _line = 1;

            public Parser(string s) { _s = s; }

            public JsonValue ParseDocument()
            {
                SkipSpace();
                var value = ParseValue();
                SkipSpace();
                if (_i < _s.Length) throw Error("Unexpected text after the end of the document");
                return value;
            }

            JsonValue ParseValue()
            {
                SkipSpace();
                if (_i >= _s.Length) throw Error("Unexpected end of document");
                int line = _line;
                char c = _s[_i];
                JsonValue v;
                switch (c)
                {
                    case '{': v = ParseObject(); break;
                    case '[': v = ParseArray(); break;
                    case '"': v = new JsonValue(ParseString()); break;
                    case 't': Expect("true"); v = new JsonValue(true); break;
                    case 'f': Expect("false"); v = new JsonValue(false); break;
                    case 'n': Expect("null"); v = new JsonValue(JsonType.Null); break;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) { v = new JsonValue(ParseNumber()); break; }
                        throw Error($"Unexpected character '{c}'");
                }
                v.Line = line;
                return v;
            }

            JsonValue ParseObject()
            {
                var obj = new JsonValue(JsonType.Object);
                _i++; // {
                SkipSpace();
                if (Peek() == '}') { _i++; return obj; }
                while (true)
                {
                    SkipSpace();
                    if (Peek() != '"') throw Error("Expected a quoted key");
                    string key = ParseString();
                    SkipSpace();
                    if (Peek() != ':') throw Error($"Expected ':' after key \"{key}\"");
                    _i++;
                    var value = ParseValue();
                    if (obj.Has(key)) throw Error($"Duplicate key \"{key}\"");
                    obj._members.Add(new KeyValuePair<string, JsonValue>(key, value));
                    SkipSpace();
                    char c = Peek();
                    if (c == ',') { _i++; SkipSpace(); if (Peek() == '}') { _i++; return obj; } continue; }
                    if (c == '}') { _i++; return obj; }
                    throw Error("Expected ',' or '}' in object");
                }
            }

            JsonValue ParseArray()
            {
                var arr = new JsonValue(JsonType.Array);
                _i++; // [
                SkipSpace();
                if (Peek() == ']') { _i++; return arr; }
                while (true)
                {
                    arr._items.Add(ParseValue());
                    SkipSpace();
                    char c = Peek();
                    if (c == ',') { _i++; SkipSpace(); if (Peek() == ']') { _i++; return arr; } continue; }
                    if (c == ']') { _i++; return arr; }
                    throw Error("Expected ',' or ']' in array");
                }
            }

            string ParseString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\n') throw Error("Line break inside a string");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (_i >= _s.Length) throw Error("Unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"Unknown escape '\\{e}'");
                    }
                }
            }

            double ParseNumber()
            {
                int start = _i;
                if (Peek() == '-') _i++;
                while (_i < _s.Length && "0123456789.eE+-".IndexOf(_s[_i]) >= 0) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error($"Bad number '{token}'");
                return d;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                _i += word.Length;
            }

            char Peek() => _i < _s.Length ? _s[_i] : '\0';

            void SkipSpace()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == '\n') { _line++; _i++; }
                    else if (c == ' ' || c == '\t' || c == '\r' || c == '﻿') _i++;
                    else if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        // Line comments are allowed so puzzle files can carry author notes.
                        while (_i < _s.Length && _s[_i] != '\n') _i++;
                    }
                    else break;
                }
            }

            FormatException Error(string message) => new FormatException($"JSON line {_line}: {message}");
        }
    }

    public static class JsonWriter
    {
        public static string Write(JsonValue value, bool pretty = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value ?? JsonValue.Null, pretty, 0);
            if (pretty) sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, JsonValue v, bool pretty, int depth)
        {
            switch (v.Type)
            {
                case JsonType.Null: sb.Append("null"); break;
                case JsonType.Bool: sb.Append(v.AsBool() ? "true" : "false"); break;
                case JsonType.Number: sb.Append(v.AsNumber().ToString("R", CultureInfo.InvariantCulture)); break;
                case JsonType.String: WriteString(sb, v.AsString()); break;
                case JsonType.Array:
                {
                    if (v.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    int i = 0;
                    foreach (var item in v.Items)
                    {
                        if (i++ > 0) sb.Append(',');
                        NewLine(sb, pretty, depth + 1);
                        WriteValue(sb, item, pretty, depth + 1);
                    }
                    NewLine(sb, pretty, depth);
                    sb.Append(']');
                    break;
                }
                case JsonType.Object:
                {
                    if (v.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    int i = 0;
                    foreach (var member in v.Members)
                    {
                        if (i++ > 0) sb.Append(',');
                        NewLine(sb, pretty, depth + 1);
                        WriteString(sb, member.Key);
                        sb.Append(pretty ? ": " : ":");
                        WriteValue(sb, member.Value, pretty, depth + 1);
                    }
                    NewLine(sb, pretty, depth);
                    sb.Append('}');
                    break;
                }
            }
        }

        static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
