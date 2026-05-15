using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PDFFlatten.Internals;

/// <summary>
/// Serializes rewritten PDF objects back into a classic cross-reference-table PDF file.
/// </summary>
internal static class PdfSerializer
{
    internal static byte[] Serialize(PdfDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var output = new MemoryStream();
        output.Write(document.Preamble, 0, document.Preamble.Length);
        if (document.Preamble.Length == 0 || !EndsWithLineBreak(document.Preamble))
        {
            WriteAscii(output, Environment.NewLine);
        }

        var serializableObjects = CollectReachableObjects(document);
        var offsets = new Dictionary<int, long>();
        foreach (var pdfObject in serializableObjects.OrderBy(item => item.Number))
        {
            offsets[pdfObject.Number] = output.Position;
            WriteAscii(output, pdfObject.Number.ToString(CultureInfo.InvariantCulture));
            WriteAscii(output, " ");
            WriteAscii(output, pdfObject.Generation.ToString(CultureInfo.InvariantCulture));
            WriteAscii(output, " obj" + Environment.NewLine);
            WriteValue(output, pdfObject.Value);
            WriteAscii(output, Environment.NewLine + "endobj" + Environment.NewLine);
        }

        var xrefPosition = output.Position;
        var size = serializableObjects.Count == 0 ? 1 : serializableObjects.Max(item => item.Number) + 1;
        WriteAscii(output, "xref" + Environment.NewLine);
        WriteAscii(output, $"0 {size}" + Environment.NewLine);
        WriteAscii(output, "0000000000 65535 f " + Environment.NewLine);

        for (var objectNumber = 1; objectNumber < size; objectNumber++)
        {
            if (offsets.TryGetValue(objectNumber, out var offset))
            {
                WriteAscii(output, offset.ToString("D10", CultureInfo.InvariantCulture));
                WriteAscii(output, " 00000 n " + Environment.NewLine);
            }
            else
            {
                WriteAscii(output, "0000000000 00000 f " + Environment.NewLine);
            }
        }

        var trailer = document.Trailer;
        trailer["Size"] = new PdfNumber(size);
        WriteAscii(output, "trailer" + Environment.NewLine);
        WriteValue(output, trailer);
        WriteAscii(output, Environment.NewLine + "startxref" + Environment.NewLine);
        WriteAscii(output, xrefPosition.ToString(CultureInfo.InvariantCulture));
        WriteAscii(output, Environment.NewLine + "%%EOF");
        return output.ToArray();
    }

    private static List<PdfIndirectObject> CollectReachableObjects(PdfDocument document)
    {
        var reachable = new HashSet<int>();
        foreach (var entry in document.Trailer.Items)
        {
            if (string.Equals(entry.Key, "Size", StringComparison.Ordinal))
            {
                continue;
            }

            MarkReachable(document, entry.Value, reachable);
        }

        return document.Objects.Where(item => reachable.Contains(item.Number)).ToList();
    }

    private static void MarkReachable(PdfDocument document, PdfValue? value, HashSet<int> reachable)
    {
        if (value is null)
        {
            return;
        }

        if (value is PdfIndirectReference reference)
        {
            if (reachable.Add(reference.ObjectNumber))
            {
                PdfIndirectObject target;
                try
                {
                    target = document.GetRequiredObject(reference);
                }
                catch (InvalidOperationException ex)
                {
                    throw new NotSupportedException(
                        $"PDF contains an unresolved indirect reference ({reference.ObjectNumber} {reference.Generation} R).",
                        ex);
                }

                MarkReachable(document, target.Value, reachable);
            }

            return;
        }

        if (value is PdfArray array)
        {
            foreach (var item in array.Items)
            {
                MarkReachable(document, item, reachable);
            }

            return;
        }

        if (value is PdfStream stream)
        {
            MarkReachable(document, stream.Dictionary, reachable);
            return;
        }

        if (value is PdfDictionary dictionary)
        {
            foreach (var entry in dictionary.Items)
            {
                MarkReachable(document, entry.Value, reachable);
            }
        }
    }

    private static void WriteValue(Stream output, PdfValue value)
    {
        switch (value)
        {
            case PdfNull:
                WriteAscii(output, "null");
                break;
            case PdfBoolean boolean:
                WriteAscii(output, boolean.Value ? "true" : "false");
                break;
            case PdfNumber number:
                WriteAscii(output, number.RawValue);
                break;
            case PdfName name:
                WriteAscii(output, "/" + name.Value);
                break;
            case PdfLiteralString literalString:
                WriteAscii(output, "(" + EscapeLiteralString(literalString.Value) + ")");
                break;
            case PdfHexString hexString:
                WriteAscii(output, "<" + hexString.Value + ">");
                break;
            case PdfIndirectReference reference:
                WriteAscii(output, $"{reference.ObjectNumber} {reference.Generation} R");
                break;
            case PdfArray array:
                WriteAscii(output, "[");
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        WriteAscii(output, " ");
                    }

                    WriteValue(output, array.Items[index]);
                }

                WriteAscii(output, "]");
                break;
            case PdfStream stream:
                WriteStream(output, stream);
                break;
            case PdfDictionary dictionary:
                WriteAscii(output, "<<");
                foreach (var entry in dictionary.Items)
                {
                    WriteAscii(output, "/" + entry.Key + " ");
                    WriteValue(output, entry.Value);
                }

                WriteAscii(output, ">>");
                break;
            default:
                throw new NotSupportedException($"Cannot serialize PDF value type {value.GetType().Name}.");
        }
    }

    private static void WriteStream(Stream output, PdfStream stream)
    {
        WriteAscii(output, "<<");
        foreach (var entry in stream.Dictionary.Items)
        {
            if (string.Equals(entry.Key, "Length", StringComparison.Ordinal))
            {
                continue;
            }

            WriteAscii(output, "/" + entry.Key + " ");
            WriteValue(output, entry.Value);
        }

        WriteAscii(output, "/Length " + stream.Data.Length.ToString(CultureInfo.InvariantCulture) + ">>" + Environment.NewLine);
        WriteAscii(output, "stream" + Environment.NewLine);
        output.Write(stream.Data, 0, stream.Data.Length);
        WriteAscii(output, Environment.NewLine + "endstream");
    }

    private static string EscapeLiteralString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static bool EndsWithLineBreak(byte[] bytes)
    {
        var last = bytes[bytes.Length - 1];
        return last == 10 || last == 13;
    }

    private static void WriteAscii(Stream output, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }
}
