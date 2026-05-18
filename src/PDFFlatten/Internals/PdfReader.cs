using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PDFFlatten.Internals;

/// <summary>
/// Incrementally reads PDF values from a byte buffer.
/// </summary>
internal sealed class PdfReader
{
    private readonly byte[] _data;
    private int _position;

    internal PdfReader(byte[] data, int position)
    {
        _data = data;
        _position = position;
    }

    internal PdfValue ReadValue()
    {
        SkipWhiteSpaceAndComments();
        if (_position >= _data.Length)
        {
            throw new InvalidOperationException("Unexpected end of file while reading a PDF value.");
        }

        var current = _data[_position];
        switch (current)
        {
            case (byte)'<':
                if (_position + 1 < _data.Length && _data[_position + 1] == '<')
                {
                    return ReadDictionary();
                }

                return ReadHexString();
            case (byte)'(':
                return ReadLiteralString();
            case (byte)'[':
                return ReadArray();
            case (byte)'/':
                return ReadName();
            default:
                if (IsNumericToken(current) || current == '+' || current == '-')
                {
                    return ReadNumberOrReference();
                }

                var keyword = ReadKeyword();
                return keyword switch
                {
                    "true" => new PdfBoolean(true),
                    "false" => new PdfBoolean(false),
                    "null" => PdfNull.Value,
                    _ => throw new NotSupportedException($"Unsupported PDF keyword '{keyword}'.")
                };
        }
    }

    internal int ReadInteger()
    {
        var token = ReadSimpleToken();
        return PdfParser.ParseIntegerToken(token, "indirect object header");
    }

    internal string ReadKeyword()
    {
        return ReadSimpleToken();
    }

    internal string PeekKeyword()
    {
        var snapshot = _position;
        var token = ReadSimpleToken();
        _position = snapshot;
        return token;
    }

    internal byte[] ReadBytes(int length)
    {
        if (length < 0)
        {
            throw new InvalidOperationException("Stream /Length must be non-negative.");
        }

        if (length > _data.Length - _position)
        {
            throw new InvalidOperationException("Stream /Length extends beyond the available data.");
        }

        var buffer = new byte[length];
        Array.Copy(_data, _position, buffer, 0, length);
        _position += length;
        return buffer;
    }

    internal void SkipWhiteSpaceAndComments()
    {
        PdfParser.SkipWhiteSpaceAndComments(_data, ref _position);
    }

    internal void ConsumeStreamLineEnding()
    {
        if (_position < _data.Length && _data[_position] == 13)
        {
            _position += 1;
            if (_position < _data.Length && _data[_position] == 10)
            {
                _position += 1;
            }
        }
        else if (_position < _data.Length && _data[_position] == 10)
        {
            _position += 1;
        }
    }

    internal void SkipPotentialStreamTerminator()
    {
        if (_position < _data.Length && _data[_position] == 13)
        {
            _position += 1;
            if (_position < _data.Length && _data[_position] == 10)
            {
                _position += 1;
            }
        }
        else if (_position < _data.Length && _data[_position] == 10)
        {
            _position += 1;
        }
    }

    private PdfDictionary ReadDictionary()
    {
        _position += 2;
        var dictionary = new PdfDictionary();

        while (true)
        {
            SkipWhiteSpaceAndComments();
            if (_position + 1 < _data.Length && _data[_position] == '>' && _data[_position + 1] == '>')
            {
                _position += 2;
                break;
            }

            var key = ReadName();
            var value = ReadValue();
            dictionary[key.Value] = value;
        }

        return dictionary;
    }

    private PdfArray ReadArray()
    {
        _position += 1;
        var items = new List<PdfValue>();

        while (true)
        {
            SkipWhiteSpaceAndComments();
            if (_position >= _data.Length)
            {
                throw new InvalidOperationException("Array was not terminated.");
            }

            if (_data[_position] == ']')
            {
                _position += 1;
                break;
            }

            items.Add(ReadValue());
        }

        return new PdfArray(items);
    }

    private PdfName ReadName()
    {
        _position += 1;
        var start = _position;
        while (_position < _data.Length && !PdfParser.IsDelimiter(_data[_position]))
        {
            _position += 1;
        }

        var name = Encoding.ASCII.GetString(_data, start, _position - start);
        return new PdfName(name);
    }

    private PdfLiteralString ReadLiteralString()
    {
        _position += 1;
        var depth = 1;
        var builder = new StringBuilder();

        while (_position < _data.Length && depth > 0)
        {
            var current = _data[_position];
            _position += 1;
            if (current == '\\')
            {
                if (_position >= _data.Length)
                {
                    break;
                }

                var escaped = _data[_position];
                _position += 1;
                switch (escaped)
                {
                    case (byte)'n':
                        builder.Append((char)10);
                        break;
                    case (byte)'r':
                        builder.Append((char)13);
                        break;
                    case (byte)'t':
                        builder.Append((char)9);
                        break;
                    case (byte)'b':
                        builder.Append((char)8);
                        break;
                    case (byte)'f':
                        builder.Append((char)12);
                        break;
                    case (byte)'(':
                    case (byte)')':
                    case (byte)'\\':
                        builder.Append((char)escaped);
                        break;
                    case 10:
                    case 13:
                        if (escaped == 13 && _position < _data.Length && _data[_position] == 10)
                        {
                            _position += 1;
                        }

                        break;
                    default:
                        builder.Append((char)escaped);
                        break;
                }
            }
            else if (current == '(')
            {
                depth += 1;
                builder.Append('(');
            }
            else if (current == ')')
            {
                depth -= 1;
                if (depth > 0)
                {
                    builder.Append(')');
                }
            }
            else
            {
                builder.Append((char)current);
            }
        }

        if (depth != 0)
        {
            throw new InvalidOperationException("String literal was not terminated.");
        }

        return new PdfLiteralString(builder.ToString());
    }

    private PdfHexString ReadHexString()
    {
        _position += 1;
        var builder = new StringBuilder();
        while (_position < _data.Length && _data[_position] != '>')
        {
            var current = (char)_data[_position];
            if (!char.IsWhiteSpace(current))
            {
                builder.Append(current);
            }

            _position += 1;
        }

        if (_position >= _data.Length)
        {
            throw new InvalidOperationException("Hex string was not terminated.");
        }

        _position += 1;
        return new PdfHexString(builder.ToString());
    }

    private PdfValue ReadNumberOrReference()
    {
        var firstToken = ReadSimpleToken();
        var snapshot = _position;

        SkipWhiteSpaceAndComments();
        var secondToken = ReadSimpleToken();
        SkipWhiteSpaceAndComments();
        var maybeReferenceMarker = ReadSimpleToken();
        if (IsIntegerToken(firstToken) && IsIntegerToken(secondToken) && maybeReferenceMarker == "R")
        {
            return new PdfIndirectReference(
                PdfParser.ParseIntegerToken(firstToken, "indirect reference object number"),
                PdfParser.ParseIntegerToken(secondToken, "indirect reference generation"));
        }

        _position = snapshot;
        return new PdfNumber(firstToken);
    }

    private string ReadSimpleToken()
    {
        SkipWhiteSpaceAndComments();
        var start = _position;
        while (_position < _data.Length && !PdfParser.IsDelimiter(_data[_position]))
        {
            _position += 1;
        }

        return Encoding.ASCII.GetString(_data, start, _position - start);
    }

    private static bool IsIntegerToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index == 0 && (character == '+' || character == '-'))
            {
                continue;
            }

            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumericToken(byte value)
    {
        return value >= '0' && value <= '9';
    }
}
