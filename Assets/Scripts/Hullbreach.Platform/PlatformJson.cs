using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hullbreach.Platform
{
    // Just enough JSON for the auth endpoints: netstandard2.1 has no System.Text.Json
    // and JsonUtility needs UnityEngine. Objects become Dictionary<string, object>,
    // arrays List<object>, numbers double.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public static class PlatformJson
    {
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            int code = c;
                            builder.Append("\\u").Append(code.ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                            builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        // False on any syntax error or a non-object top level; the API answers objects only.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static bool TryParseObject(string json, out Dictionary<string, object> result)
        {
            result = null;
            if (string.IsNullOrEmpty(json)) return false;
            var reader = new Reader(json);
            if (!reader.TryValue(out object value) || !reader.AtEnd()) return false;
            result = value as Dictionary<string, object>;
            return result != null;
        }

        // Null when the key is missing or not a string.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static string GetString(Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out object value) ? value as string : null;

        // Null when the key is missing or not an object.
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out object value) ? value as Dictionary<string, object> : null;

        sealed class Reader
        {
            readonly string _text;
            int _pos;

            public Reader(string text) => _text = text;

            public bool AtEnd()
            {
                SkipWhitespace();
                return _pos == _text.Length;
            }

            public bool TryValue(out object value)
            {
                value = null;
                SkipWhitespace();
                if (_pos >= _text.Length) return false;
                char c = _text[_pos];
                if (c == '{') return TryObject(out value);
                if (c == '[') return TryArray(out value);
                if (c == '"')
                {
                    bool ok = TryString(out string s);
                    value = s;
                    return ok;
                }
                if (TryLiteral("true")) { value = true; return true; }
                if (TryLiteral("false")) { value = false; return true; }
                if (TryLiteral("null")) return true;
                return TryNumber(out value);
            }

            bool TryObject(out object value)
            {
                value = null;
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++;
                SkipWhitespace();
                if (Peek() == '}') { _pos++; value = obj; return true; }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"' || !TryString(out string key)) return false;
                    SkipWhitespace();
                    if (Peek() != ':') return false;
                    _pos++;
                    if (!TryValue(out object item)) return false;
                    obj[key] = item;
                    SkipWhitespace();
                    char next = Peek();
                    _pos++;
                    if (next == '}') { value = obj; return true; }
                    if (next != ',') return false;
                }
            }

            bool TryArray(out object value)
            {
                value = null;
                var list = new List<object>();
                _pos++;
                SkipWhitespace();
                if (Peek() == ']') { _pos++; value = list; return true; }
                while (true)
                {
                    if (!TryValue(out object item)) return false;
                    list.Add(item);
                    SkipWhitespace();
                    char next = Peek();
                    _pos++;
                    if (next == ']') { value = list; return true; }
                    if (next != ',') return false;
                }
            }

            bool TryString(out string value)
            {
                value = null;
                var builder = new StringBuilder();
                _pos++;
                while (_pos < _text.Length)
                {
                    char c = _text[_pos++];
                    if (c == '"') { value = builder.ToString(); return true; }
                    if (c != '\\') { builder.Append(c); continue; }
                    if (_pos >= _text.Length) return false;
                    char escape = _text[_pos++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length ||
                                !int.TryParse(_text.Substring(_pos, 4), NumberStyles.AllowHexSpecifier,
                                    CultureInfo.InvariantCulture, out int code))
                                return false;
                            builder.Append((char)code);
                            _pos += 4;
                            break;
                        default: return false;
                    }
                }
                return false;
            }

            bool TryNumber(out object value)
            {
                value = null;
                int start = _pos;
                while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0) _pos++;
                if (_pos == start) return false;
                if (!double.TryParse(_text.Substring(start, _pos - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double number))
                    return false;
                value = number;
                return true;
            }

            bool TryLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0) return false;
                _pos += literal.Length;
                return true;
            }

            char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

            void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }
        }
    }
}
