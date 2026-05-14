using System.Text;
using System.IO.Compression;
using NUnit.Framework;
using PDFFlatten;
using PDFFlatten.Internals;

namespace PDFFlatten.Tests;

public sealed class PdfFlattenerTests
{
    [Test]
    public void Flatten_returns_memory_stream_positioned_at_start()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithoutForms());
        using var flattened = PdfFlattener.Flatten(input);

        Assert.Multiple(() =>
        {
            Assert.That(flattened.Position, Is.EqualTo(0));
            Assert.That(flattened.Length, Is.GreaterThan(0));
        });
    }

    [Test]
    public void Flatten_leaves_non_form_pdf_unchanged()
    {
        var original = SimplePdfFactory.CreateDocumentWithoutForms();
        using var input = new MemoryStream(original);
        using var flattened = PdfFlattener.Flatten(input);

        Assert.That(ReadAllBytes(flattened), Is.EqualTo(original));
    }

    [Test]
    public void Flatten_removes_acroform_and_widgets_from_generic_fixture()
    {
        using var input = File.OpenRead(TestAssets.SamplePdfPath);
        using var flattened = PdfFlattener.Flatten(input);
        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);

        PdfValue? ignored = null;
        Assert.That(catalog.TryGetValue("AcroForm", ref ignored), Is.False);
        Assert.That(page.TryGetValue("Annots", ref ignored), Is.False);

        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedResourceNames = xObjects.Items.Keys
            .Where(key => key.StartsWith("FldFlat", StringComparison.Ordinal))
            .ToArray();
        Assert.That(flattenedResourceNames, Has.Length.EqualTo(TestAssets.ExpectedFieldValues.Count));

        var contents = page.RequireArray("Contents");
        Assert.That(contents.Items.Count, Is.EqualTo(2));

        var appendedRef = contents.Items.Last() as PdfIndirectReference;
        Assert.That(appendedRef, Is.Not.Null);
        var appendedStream = document.GetRequiredStream(appendedRef!);
        var commands = Encoding.ASCII.GetString(appendedStream.Data);
        var drawOperationCount = commands
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.Contains("/FldFlat", StringComparison.Ordinal) &&
                           line.Contains(" Do", StringComparison.Ordinal));
        Assert.That(drawOperationCount, Is.EqualTo(TestAssets.ExpectedFieldValues.Count));
    }

    [Test]
    public void Flatten_preserves_non_widget_annotations()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithWidgetAndLink());
        using var flattened = PdfFlattener.Flatten(input);
        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);

        PdfValue? ignored = null;
        Assert.That(catalog.TryGetValue("AcroForm", ref ignored), Is.False);

        var annotations = page.RequireArray("Annots");
        Assert.That(annotations.Items.Count, Is.EqualTo(1));
        var linkRef = annotations.Items[0] as PdfIndirectReference;
        Assert.That(linkRef, Is.Not.Null);
        var link = document.GetRequiredDictionary(linkRef!);
        Assert.That(link.GetNameValue("Subtype"), Is.EqualTo("Link"));
    }

    [Test]
    public void Flatten_repairs_broken_text_appearance_font_resources()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithBrokenTextAppearanceResources());
        using var flattened = PdfFlattener.Flatten(input);
        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedAppearanceRef = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceRef, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceRef!);
        var appearanceResources = ResolveNestedDictionary(document, flattenedAppearance.Dictionary, "Resources");
        var fontResources = ResolveNestedDictionary(document, appearanceResources, "Font");

        Assert.That(fontResources.Items.ContainsKey("Helv"), Is.True);

        var repairedFontRef = fontResources.Items["Helv"] as PdfIndirectReference;
        Assert.That(repairedFontRef, Is.Not.Null);
        Assert.That(document.GetRequiredDictionary(repairedFontRef!).GetNameValue("BaseFont"), Is.EqualTo("Helvetica"));

        var appearanceContent = DecompressFlate(flattenedAppearance.Data);
        Assert.That(appearanceContent, Does.Contain("(Filled) Tj"));
    }

    [Test]
    public void Flatten_rejects_missing_input_stream()
    {
        using var output = new MemoryStream();

        Assert.That(() => PdfFlattener.Flatten(null!, output), Throws.TypeOf<ArgumentNullException>());
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static PdfDictionary GetFirstPage(PdfDocument document, PdfDictionary catalog)
    {
        var pages = document.GetRequiredDictionary(catalog.RequireReference("Pages"));
        var firstPageRef = pages.RequireArray("Kids").Items.Single() as PdfIndirectReference;
        Assert.That(firstPageRef, Is.Not.Null);
        return document.GetRequiredDictionary(firstPageRef!);
    }

    private static PdfDictionary ResolvePageResources(PdfDocument document, PdfDictionary page)
    {
        PdfValue? resourcesValue = null;
        Assert.That(page.TryGetValue("Resources", ref resourcesValue), Is.True);
        return resourcesValue switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException("Unexpected /Resources value.")
        };
    }

    private static PdfDictionary ResolveNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        PdfValue? value = null;
        Assert.That(parent.TryGetValue(key, ref value), Is.True);
        return value switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException($"Unexpected /{key} value.")
        };
    }

    private static class SimplePdfFactory
    {
        public static byte[] CreateDocumentWithoutForms()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources <</Font <</F1 5 0 R>>>> /Contents 4 0 R>>",
                [4] = Stream("BT /F1 12 Tf 20 100 Td (Hello) Tj ET"),
                [5] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"
            });
        }

        public static byte[] CreateDocumentWithWidgetAndLink()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R 7 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /AP <</N 8 0 R>> /V (Filled)>>",
                [7] = "<</Type /Annot /Subtype /Link /Rect [140 20 180 44] /Border [0 0 0] /A <</S /URI /URI (https://example.com)>>>>",
                [8] = Stream("q 0 0 100 24 re W n BT /F1 12 Tf 2 8 Td (Filled) Tj ET Q", "<</Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font <</F1 10 0 R>>>>>>"),
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"
            });
        }

        public static byte[] CreateDocumentWithBrokenTextAppearanceResources()
        {
            return BuildPdf(new Dictionary<int, byte[]>
            {
                [1] = AsciiBody("<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>"),
                [2] = AsciiBody("<</Type /Pages /Count 1 /Kids [3 0 R]>>"),
                [3] = AsciiBody("<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>"),
                [5] = AsciiBody("<</Font <</He 10 0 R>> /XObject <<>>>>"),
                [6] = AsciiBody("<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /DA (/He 12 Tf 0 g) /DR <</Font 11 0 R>> /AP <</N 8 0 R>> /V (Filled)>>"),
                [8] = CompressedRawStream("q 0 0 100 24 re W n BT /Helv 12 Tf 0 g 2 8 Td (Filled) Tj ET Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font 253 0 R>>"),
                [9] = AsciiBody(Stream("q Q")),
                [10] = AsciiBody("<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>"),
                [11] = AsciiBody("<</He 10 0 R>>")
            });
        }

        private static byte[] BuildPdf(IDictionary<int, string> bodies)
        {
            return BuildPdf(bodies.ToDictionary(pair => pair.Key, pair => AsciiBody(pair.Value)));
        }

        private static byte[] BuildPdf(IDictionary<int, byte[]> bodies)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.NewLine = "\n";
            writer.Write("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");
            writer.Flush();

            var offsets = new Dictionary<int, long>();
            foreach (var objectNumber in bodies.Keys.Order())
            {
                offsets[objectNumber] = stream.Position;
                writer.Write($"{objectNumber} 0 obj\n");
                writer.Flush();
                stream.Write(bodies[objectNumber], 0, bodies[objectNumber].Length);
                writer.Write("\nendobj\n");
                writer.Flush();
            }

            var size = bodies.Keys.Max() + 1;
            var xrefPosition = stream.Position;
            writer.Write($"xref\n0 {size}\n");
            writer.Write("0000000000 65535 f \n");
            for (var objectNumber = 1; objectNumber < size; objectNumber++)
            {
                if (offsets.TryGetValue(objectNumber, out var offset))
                {
                    writer.Write($"{offset:D10} 00000 n \n");
                }
                else
                {
                    writer.Write("0000000000 00000 f \n");
                }
            }

            writer.Write("trailer\n");
            writer.Write($"<</Size {size} /Root 1 0 R>>\n");
            writer.Write("startxref\n");
            writer.Write($"{xrefPosition}\n%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static string Stream(string content, string? dictionaryPrefix = null)
        {
            var dictionaryEntries = dictionaryPrefix is null ? string.Empty : dictionaryPrefix.Replace("<<", string.Empty).Replace(">>", string.Empty);
            return CreateStreamBody(content, dictionaryEntries);
        }

        private static string CreateStreamBody(string content, string dictionaryEntries = "")
        {
            var data = Encoding.ASCII.GetBytes(content);
            return $"<<{dictionaryEntries} /Length {data.Length} >>\nstream\n{content}\nendstream";
        }

        private static byte[] AsciiBody(string content)
        {
            return Encoding.ASCII.GetBytes(content);
        }

        private static byte[] CompressedRawStream(string content, string dictionaryEntries)
        {
            var contentBytes = Encoding.ASCII.GetBytes(content);
            var compressed = Compress(contentBytes);

            using var stream = new MemoryStream();
            stream.Write(Encoding.ASCII.GetBytes($"<<{dictionaryEntries} /Filter /FlateDecode /Length {compressed.Length}>>\nstream\n"));
            stream.Write(compressed, 0, compressed.Length);
            stream.Write(Encoding.ASCII.GetBytes("\nendstream"));
            return stream.ToArray();
        }

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var compressor = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }
    }

    private static string DecompressFlate(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return Encoding.ASCII.GetString(output.ToArray());
    }
}
