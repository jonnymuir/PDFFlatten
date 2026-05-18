using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PDFFlatten.Internals;

/// <summary>
/// Parses the narrow classic-PDF subset supported by the in-house flattening engine.
/// </summary>
internal static class PdfParser
{
    internal static PdfDocument Parse(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.Length == 0)
        {
            throw new InvalidOperationException("The PDF stream is empty.");
        }

        var startXref = FindStartXref(data);
        var parsedXref = ParseXref(data, startXref);
        RejectUnsupportedTrailer(parsedXref.Trailer);
        var inUseEntries = parsedXref.Entries.Values.Where(entry => entry.InUse).ToArray();
        if (inUseEntries.Length == 0)
        {
            throw new InvalidOperationException("Cross-reference table did not contain any in-use objects.");
        }

        var firstObjectOffset = inUseEntries.Min(entry => entry.Offset);
        if (firstObjectOffset < 0 || firstObjectOffset > data.Length)
        {
            throw new InvalidOperationException("Cross-reference entries point outside the PDF data.");
        }

        var objects = new List<PdfIndirectObject>();

        foreach (var entry in inUseEntries.OrderBy(item => item.ObjectNumber))
        {
            var pdfObject = ParseObject(data, entry.Offset);
            RejectUnsupportedObject(pdfObject);
            objects.Add(pdfObject);
        }

        var preamble = new byte[firstObjectOffset];
        Array.Copy(data, 0, preamble, 0, firstObjectOffset);
        return new PdfDocument((byte[])data.Clone(), preamble, parsedXref.Trailer, objects);
    }

    internal static void SkipWhiteSpaceAndComments(byte[] data, ref int position)
    {
        while (position < data.Length)
        {
            var current = data[position];
            if (current == '%')
            {
                while (position < data.Length && data[position] != 10 && data[position] != 13)
                {
                    position += 1;
                }
            }
            else if (IsWhiteSpace(current))
            {
                position += 1;
            }
            else
            {
                break;
            }
        }
    }

    internal static bool IsDelimiter(byte value)
    {
        return IsWhiteSpace(value)
               || value == '('
               || value == ')'
               || value == '<'
               || value == '>'
               || value == '['
               || value == ']'
               || value == '{'
               || value == '}'
               || value == '/'
               || value == '%';
    }

    private static int FindStartXref(byte[] data)
    {
        var marker = Encoding.ASCII.GetBytes("startxref");
        for (var index = data.Length - marker.Length; index >= 0; index--)
        {
            var matched = true;
            for (var markerIndex = 0; markerIndex < marker.Length; markerIndex++)
            {
                if (data[index + markerIndex] != marker[markerIndex])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                var position = index + marker.Length;
                SkipWhiteSpaceAndComments(data, ref position);
                var offsetToken = ReadSimpleToken(data, ref position);
                return ParseIntegerToken(offsetToken, "startxref offset");
            }
        }

        throw new InvalidOperationException("Could not find the PDF cross-reference table.");
    }

    private static ParsedXref ParseXref(byte[] data, int startXref)
    {
        if (startXref < 0 || startXref >= data.Length)
        {
            throw new InvalidOperationException("Cross-reference offset is outside the PDF data.");
        }

        var position = startXref;
        var keyword = ReadSimpleToken(data, ref position);
        if (!string.Equals(keyword, "xref", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Cross-reference streams are not supported in this version.");
        }

        var entries = new Dictionary<int, XrefEntry>();

        while (true)
        {
            SkipWhiteSpaceAndComments(data, ref position);
            var token = PeekSimpleToken(data, position);
            if (string.Equals(token, "trailer", StringComparison.Ordinal))
            {
                ReadSimpleToken(data, ref position);
                SkipWhiteSpaceAndComments(data, ref position);
                var reader = new PdfReader(data, position);
                if (reader.ReadValue() is not PdfDictionary trailerDictionary)
                {
                    throw new InvalidOperationException("Trailer dictionary is missing.");
                }

                return new ParsedXref(entries, trailerDictionary);
            }

            var firstObject = ParseIntegerToken(ReadSimpleToken(data, ref position), "xref subsection start object");
            if (firstObject < 0)
            {
                throw new InvalidOperationException("Cross-reference subsection object numbers must be non-negative.");
            }

            SkipWhiteSpaceAndComments(data, ref position);
            var count = ParseIntegerToken(ReadSimpleToken(data, ref position), "xref subsection count");
            if (count < 0)
            {
                throw new InvalidOperationException("Cross-reference subsection count must be non-negative.");
            }

            ConsumeLineEnding(data, ref position);

            for (var offsetIndex = 0; offsetIndex < count; offsetIndex++)
            {
                var line = ReadLine(data, ref position);
                if (line.Length < 18)
                {
                    throw new InvalidOperationException("Malformed cross-reference entry.");
                }

                var offset = ParseIntegerToken(line.Substring(0, 10), "xref entry offset");
                var generation = ParseIntegerToken(line.Substring(11, 5), "xref entry generation");
                var inUse = line[17] == 'n';
                entries[firstObject + offsetIndex] = new XrefEntry(firstObject + offsetIndex, offset, generation, inUse);
            }
        }
    }

    private static PdfIndirectObject ParseObject(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
        {
            throw new InvalidOperationException("Indirect object offset points outside the PDF data.");
        }

        var reader = new PdfReader(data, offset);
        var objectNumber = reader.ReadInteger();
        var generation = reader.ReadInteger();
        var keyword = reader.ReadKeyword();
        if (!string.Equals(keyword, "obj", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Indirect object header is malformed.");
        }

        var value = reader.ReadValue();
        reader.SkipWhiteSpaceAndComments();

        if (value is PdfDictionary dictionary && reader.PeekKeyword() == "stream")
        {
            reader.ReadKeyword();
            reader.ConsumeStreamLineEnding();

            if (!dictionary.TryGetValue("Length", out var lengthValue))
            {
                throw new NotSupportedException("Streams without /Length are not supported.");
            }

            if (lengthValue is not PdfNumber lengthNumber || !lengthNumber.IsInteger)
            {
                throw new NotSupportedException("Only streams with direct integer /Length values are supported.");
            }

            var streamLength = PdfSecurityLimits.RequireInt32(lengthNumber, "stream /Length");
            if (streamLength < 0)
            {
                throw new InvalidOperationException("Stream /Length must be non-negative.");
            }

            if (streamLength > PdfSecurityLimits.MaxStreamBytes)
            {
                throw new NotSupportedException($"Streams longer than {PdfSecurityLimits.MaxStreamBytes.ToString(CultureInfo.InvariantCulture)} bytes are not supported.");
            }

            var streamData = reader.ReadBytes(streamLength);
            reader.SkipPotentialStreamTerminator();
            if (reader.ReadKeyword() != "endstream")
            {
                throw new InvalidOperationException("Stream was not terminated correctly.");
            }

            value = new PdfStream(dictionary, streamData);
        }

        reader.SkipWhiteSpaceAndComments();
        if (reader.ReadKeyword() != "endobj")
        {
            throw new InvalidOperationException("Indirect object did not terminate with endobj.");
        }

        return new PdfIndirectObject(objectNumber, generation, value);
    }

    private static void RejectUnsupportedTrailer(PdfDictionary trailer)
    {
        if (trailer.TryGetValue("Prev", out _))
        {
            throw new NotSupportedException("Incremental-update PDFs are not supported; the trailer /Prev chain must be absent.");
        }

        if (trailer.TryGetValue("Encrypt", out _))
        {
            throw new NotSupportedException("Encrypted PDFs are not supported; trailer /Encrypt must be absent.");
        }

        if (trailer.TryGetValue("XRefStm", out _))
        {
            throw new NotSupportedException("Hybrid-reference PDFs with /XRefStm are not supported.");
        }
    }

    private static void RejectUnsupportedObject(PdfIndirectObject pdfObject)
    {
        if (pdfObject.Value is not PdfStream stream)
        {
            return;
        }

        if (stream.Dictionary.TryGetValue("Type", out var typeValue)
            && typeValue is PdfName typeName
            && string.Equals(typeName.Value, "ObjStm", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Object streams (/ObjStm) are not supported.");
        }
    }

    internal static int ParseIntegerToken(string token, string context)
    {
        return PdfSecurityLimits.ParseInt32Token(token, context);
    }

    private static string ReadSimpleToken(byte[] data, ref int position)
    {
        SkipWhiteSpaceAndComments(data, ref position);
        var start = position;
        while (position < data.Length && !IsDelimiter(data[position]))
        {
            position += 1;
        }

        return Encoding.ASCII.GetString(data, start, position - start);
    }

    private static string PeekSimpleToken(byte[] data, int position)
    {
        var copy = position;
        return ReadSimpleToken(data, ref copy);
    }

    private static string ReadLine(byte[] data, ref int position)
    {
        var start = position;
        while (position < data.Length && data[position] != 10 && data[position] != 13)
        {
            position += 1;
        }

        var line = Encoding.ASCII.GetString(data, start, position - start);
        ConsumeLineEnding(data, ref position);
        return line;
    }

    private static void ConsumeLineEnding(byte[] data, ref int position)
    {
        if (position < data.Length && data[position] == 13)
        {
            position += 1;
            if (position < data.Length && data[position] == 10)
            {
                position += 1;
            }
        }
        else if (position < data.Length && data[position] == 10)
        {
            position += 1;
        }
    }

    private static bool IsWhiteSpace(byte value)
    {
        return value == 0 || value == 9 || value == 10 || value == 12 || value == 13 || value == 32;
    }
}
