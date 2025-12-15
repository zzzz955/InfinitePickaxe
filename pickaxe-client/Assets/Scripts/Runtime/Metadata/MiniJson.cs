using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InfinitePickaxe.Client.Metadata.MiniJson
{
    /// <summary>
    /// Minimal JSON parser/serializer (Unity MiniJSON style).
    /// </summary>
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return new Parser(json).ParseValue();
        }

        #region Parser
        private sealed class Parser : IDisposable
        {
            private readonly StringReader reader;

            public Parser(string json)
            {
                reader = new StringReader(json);
            }

            public void Dispose()
            {
                reader.Dispose();
            }

            public object ParseValue()
            {
                EatWhitespace();
                if (Peek == -1) return null;

                switch (PeekChar)
                {
                    case '"':
                        return ParseString();
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case 't':
                        EatLiteral("true");
                        return true;
                    case 'f':
                        EatLiteral("false");
                        return false;
                    case 'n':
                        EatLiteral("null");
                        return null;
                    default:
                        return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                // {
                reader.Read();
                while (true)
                {
                    EatWhitespace();
                    if (PeekChar == '}')
                    {
                        reader.Read();
                        break;
                    }

                    var key = ParseString();
                    EatWhitespace();
                    // :
                    reader.Read();
                    var value = ParseValue();
                    table[key] = value;
                    EatWhitespace();
                    if (PeekChar == ',')
                    {
                        reader.Read();
                        continue;
                    }
                    if (PeekChar == '}')
                    {
                        reader.Read();
                        break;
                    }
                }
                return table;
            }

            private List<object> ParseArray()
            {
                var array = new List<object>();
                // [
                reader.Read();
                while (true)
                {
                    EatWhitespace();
                    if (PeekChar == ']')
                    {
                        reader.Read();
                        break;
                    }
                    var value = ParseValue();
                    array.Add(value);
                    EatWhitespace();
                    if (PeekChar == ',')
                    {
                        reader.Read();
                        continue;
                    }
                    if (PeekChar == ']')
                    {
                        reader.Read();
                        break;
                    }
                }
                return array;
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                // "
                reader.Read();
                while (true)
                {
                    if (Peek == -1) break;
                    var ch = (char)reader.Read();
                    if (ch == '"') break;
                    if (ch == '\\')
                    {
                        if (Peek == -1) break;
                        ch = (char)reader.Read();
                        switch (ch)
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
                                var hex = new char[4];
                                reader.Read(hex, 0, 4);
                                sb.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                var number = NextWord;
                if (number.IndexOf('.', StringComparison.Ordinal) != -1)
                {
                    if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return d;
                }
                else
                {
                    if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return l;
                }
                return 0;
            }

            private void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar))
                {
                    reader.Read();
                    if (Peek == -1) break;
                }
            }

            private void EatLiteral(string literal)
            {
                foreach (var c in literal)
                {
                    reader.Read();
                }
            }

            private char PeekChar => (char)Peek;
            private int Peek => reader.Peek();
            private string NextWord
            {
                get
                {
                    var sb = new StringBuilder();
                    while (Peek != -1 && !" \t\n\r,]}".Contains(PeekChar))
                    {
                        sb.Append((char)reader.Read());
                    }
                    return sb.ToString();
                }
            }
        }
        #endregion
    }
}
