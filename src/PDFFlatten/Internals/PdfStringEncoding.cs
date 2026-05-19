using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PDFFlatten.Internals;

internal static class PdfStringEncoding
{
    internal static byte[] GetBytes(PdfValue value, string context)
    {
        switch (value)
        {
            case PdfLiteralString literalString:
                return GetBytes(literalString.Value);
            case PdfHexString hexString:
                return ParseHexString(hexString.Value, context);
            default:
                throw new InvalidOperationException($"PDF value for {context} must be a string.");
        }
    }

    internal static byte[] GetBytes(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var bytes = new byte[value.Length];
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character > byte.MaxValue)
            {
                throw new InvalidOperationException("PDF string contains characters outside the supported byte range.");
            }

            bytes[index] = (byte)character;
        }

        return bytes;
    }

    internal static string GetLiteralString(byte[] bytes)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        var characters = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            characters[index] = (char)bytes[index];
        }

        return new string(characters);
    }

    internal static string GetHexString(byte[] bytes)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static void WriteLiteralStringBytes(Stream output, string value)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        output.WriteByte((byte)'(');
        foreach (var current in GetBytes(value))
        {
            switch (current)
            {
                case (byte)'\\':
                case (byte)'(':
                case (byte)')':
                    output.WriteByte((byte)'\\');
                    output.WriteByte(current);
                    break;
                case 13:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)'r');
                    break;
                case 10:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)'n');
                    break;
                default:
                    output.WriteByte(current);
                    break;
            }
        }

        output.WriteByte((byte)')');
    }

    private static byte[] ParseHexString(string value, string context)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var normalized = value.Length % 2 == 0 ? value : value + "0";
        var bytes = new byte[normalized.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            var pair = normalized.Substring(index * 2, 2);
            if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                throw new InvalidOperationException($"PDF hex string for {context} is malformed.");
            }
        }

        return bytes;
    }
}
