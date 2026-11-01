// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NatsunekoLaboratory.AssetExplorer.Json
{
    /// <summary>
    ///     A strict JSON parser that produces <see cref="Dictionary{TKey,TValue}" /> (objects), <see cref="List{T}" /> (arrays),
    ///     <see cref="string" />, <see cref="long" /> (integral numbers), <see cref="double" />, <see cref="bool" /> and null.
    /// </summary>
    internal sealed class JsonReader
    {
        private readonly string _text;
        private int _position;

        private JsonReader(string text)
        {
            _text = text ?? "";
        }

        public static object Parse(string json)
        {
            var reader = new JsonReader(json);
            reader.SkipWhitespace();

            var value = reader.ReadValue();

            reader.SkipWhitespace();
            if (reader._position != reader._text.Length)
                throw new JsonException("Unexpected trailing characters", reader._position);

            return value;
        }

        private object ReadValue()
        {
            if (_position >= _text.Length)
                throw new JsonException("Unexpected end of input", _position);

            switch (_text[_position])
            {
                case '{':
                    return ReadObject();

                case '[':
                    return ReadArray();

                case '"':
                    return ReadString();

                case 't':
                    ExpectLiteral("true");
                    return true;

                case 'f':
                    ExpectLiteral("false");
                    return false;

                case 'n':
                    ExpectLiteral("null");
                    return null;

                default:
                    return ReadNumber();
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            var obj = new Dictionary<string, object>();
            _position++; // {
            SkipWhitespace();

            if (TryConsume('}'))
                return obj;

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                    throw new JsonException("Expected a property name", _position);

                var key = ReadString();
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                obj[key] = ReadValue();
                SkipWhitespace();

                if (TryConsume(','))
                    continue;

                Expect('}');
                return obj;
            }
        }

        private List<object> ReadArray()
        {
            var array = new List<object>();
            _position++; // [
            SkipWhitespace();

            if (TryConsume(']'))
                return array;

            while (true)
            {
                SkipWhitespace();
                array.Add(ReadValue());
                SkipWhitespace();

                if (TryConsume(','))
                    continue;

                Expect(']');
                return array;
            }
        }

        private string ReadString()
        {
            var start = _position;
            _position++; // "

            var sb = new StringBuilder();
            while (true)
            {
                if (_position >= _text.Length)
                    throw new JsonException("Unterminated string", start);

                var c = _text[_position++];
                if (c == '"')
                    return sb.ToString();

                if (c < 0x20)
                    throw new JsonException("Control character in string", _position - 1);

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (_position >= _text.Length)
                    throw new JsonException("Unterminated string", start);

                var escape = _text[_position++];
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(escape);
                        break;

                    case 'b':
                        sb.Append('\b');
                        break;

                    case 'f':
                        sb.Append('\f');
                        break;

                    case 'n':
                        sb.Append('\n');
                        break;

                    case 'r':
                        sb.Append('\r');
                        break;

                    case 't':
                        sb.Append('\t');
                        break;

                    case 'u':
                        if (_position + 4 > _text.Length || !int.TryParse(_text.Substring(_position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            throw new JsonException("Invalid unicode escape", _position);

                        // surrogate pairs arrive as two escapes and are appended unit by unit
                        sb.Append((char)code);
                        _position += 4;
                        break;

                    default:
                        throw new JsonException("Invalid escape", _position - 1);
                }
            }
        }

        private object ReadNumber()
        {
            var start = _position;
            if (Peek() == '-')
                _position++;

            var integral = true;
            if (!ConsumeDigits())
                throw new JsonException("Unexpected character", start);

            if (Peek() == '.')
            {
                integral = false;
                _position++;
                if (!ConsumeDigits())
                    throw new JsonException("Expected digits after the decimal point", _position);
            }

            if (Peek() == 'e' || Peek() == 'E')
            {
                integral = false;
                _position++;
                if (Peek() == '+' || Peek() == '-')
                    _position++;
                if (!ConsumeDigits())
                    throw new JsonException("Expected digits in the exponent", _position);
            }

            var token = _text.Substring(start, _position - start);
            if (integral && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
                return l;

            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private bool ConsumeDigits()
        {
            var start = _position;
            while (_position < _text.Length && _text[_position] >= '0' && _text[_position] <= '9')
                _position++;

            return _position > start;
        }

        private void ExpectLiteral(string literal)
        {
            if (string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
                throw new JsonException($"Expected {literal}", _position);

            _position += literal.Length;
        }

        private void Expect(char c)
        {
            if (!TryConsume(c))
                throw new JsonException($"Expected '{c}'", _position);
        }

        private bool TryConsume(char c)
        {
            if (Peek() != c)
                return false;

            _position++;
            return true;
        }

        private char Peek()
        {
            return _position < _text.Length ? _text[_position] : '\0';
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    return;

                _position++;
            }
        }
    }
}
