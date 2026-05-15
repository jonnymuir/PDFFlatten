using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PDFFlatten;
using PDFFlatten.Internals;

namespace PDFFlatten.Tests;

public sealed partial class UnsupportedPdfGuardTests
{
    [Test]
    public void Flatten_rejects_xref_stream_inputs()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateXrefStreamPdf(),
            "Cross-reference streams are not supported");
    }

    [Test]
    public void Flatten_rejects_indirect_stream_lengths()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateIndirectStreamLengthPdf(),
            "Only streams with direct integer /Length values are supported.");
    }

    [Test]
    public void Flatten_rejects_indirect_page_annotation_arrays()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateIndirectAnnotsPdf(),
            "page /Annots");
    }

    [Test]
    public void Flatten_rejects_missing_widget_appearance_references()
    {
        AssertRejects<InvalidOperationException>(
            UnsupportedPdfFixtureFactory.CreateMissingAppearanceReferencePdf(),
            "Object 99 0 R was not found.");
    }

    [Test]
    public void Flatten_rejects_non_stream_widget_normal_appearances()
    {
        AssertRejects<InvalidOperationException>(
            UnsupportedPdfFixtureFactory.CreateNonStreamNormalAppearancePdf(),
            "Object 8 0 R is not a stream.");
    }

    [Test]
    public void Flatten_keeps_each_replayed_appearance_reachable_from_page_resources()
    {
        using var input = File.OpenRead(TestAssets.SamplePdfPath);
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var pageResources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, pageResources, "XObject");
        var appendedContentReference = page.RequireArray("Contents").Items.Last() as PdfIndirectReference;

        Assert.That(appendedContentReference, Is.Not.Null);

        var appendedContent = Encoding.ASCII.GetString(document.GetRequiredStream(appendedContentReference!).Data);
        var drawnResources = DrawOperationRegex()
            .Matches(appendedContent)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(drawnResources, Has.Length.EqualTo(TestAssets.ExpectedFieldValues.Count));

        foreach (var resourceName in drawnResources)
        {
            Assert.That(
                xObjects.Items.TryGetValue(resourceName, out var resourceValue),
                Is.True,
                $"Expected /{resourceName} to exist in page /Resources /XObject.");
            Assert.That(resourceValue, Is.TypeOf<PdfIndirectReference>(), $"/{resourceName} should resolve through an indirect XObject reference.");
            Assert.That(
                () => document.GetRequiredStream((PdfIndirectReference)resourceValue!),
                Throws.Nothing,
                $"Expected /{resourceName} to resolve to a stream-backed XObject.");
        }
    }

    private static void AssertRejects<TException>(byte[] pdfBytes, string expectedMessageFragment)
        where TException : Exception
    {
        using var input = new MemoryStream(pdfBytes, writable: false);

        var exception = Assert.Throws<TException>(() =>
        {
            using var _ = PdfFlattener.Flatten(input);
        });

        Assert.That(exception!.Message, Does.Contain(expectedMessageFragment));
    }

    private static PdfDictionary GetFirstPage(PdfDocument document, PdfDictionary catalog)
    {
        var pages = document.GetRequiredDictionary(catalog.RequireReference("Pages"));
        var firstPageReference = pages.RequireArray("Kids").Items.Single() as PdfIndirectReference;
        Assert.That(firstPageReference, Is.Not.Null);
        return document.GetRequiredDictionary(firstPageReference!);
    }

    private static PdfDictionary ResolvePageResources(PdfDocument document, PdfDictionary page)
    {
        Assert.That(page.TryGetValue("Resources", out var resourcesValue), Is.True);
        return resourcesValue switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException("Unexpected /Resources value.")
        };
    }

    private static PdfDictionary ResolveNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        Assert.That(parent.TryGetValue(key, out var nestedValue), Is.True, $"Expected /{key} to exist.");
        return nestedValue switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException($"Unexpected /{key} value.")
        };
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

    [GeneratedRegex(@"/(?<name>FldFlat\d+)\s+Do", RegexOptions.CultureInvariant)]
    private static partial Regex DrawOperationRegex();

    private static class UnsupportedPdfFixtureFactory
    {
        public static byte[] CreateXrefStreamPdf()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.NewLine = "\n";
            writer.Write("%PDF-1.5\n%\u00E2\u00E3\u00CF\u00D3\n");
            writer.Write("1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n");
            writer.Write("2 0 obj\n<</Type /Pages /Count 0 /Kids []>>\nendobj\n");
            var xrefStreamOffset = stream.Position;
            writer.Write("5 0 obj\n<</Type /XRef /Size 6 /Root 1 0 R /W [1 1 1] /Length 0>>\nstream\n\nendstream\nendobj\n");
            writer.Write("startxref\n");
            writer.Write($"{xrefStreamOffset}\n");
            writer.Write("%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        public static byte[] CreateIndirectStreamLengthPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R>>",
                [4] = "<< /Length 5 0 R >>\nstream\nBT ET\nendstream",
                [5] = "5"
            });
        }

        public static byte[] CreateIndirectAnnotsPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots 5 0 R /Contents 4 0 R>>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [5] = "[6 0 R]",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP <</N 8 0 R>>>>",
                [8] = FormXObjectStream("q 0 0 100 24 re W n Q")
            });
        }

        public static byte[] CreateMissingAppearanceReferencePdf()
        {
            return CreateWidgetPdf("[6 0 R]", "99 0 R", FormXObjectStream("q 0 0 100 24 re W n Q"), includeAppearanceObject: false);
        }

        public static byte[] CreateNonStreamNormalAppearancePdf()
        {
            return CreateWidgetPdf("[6 0 R]", "8 0 R", "<</Type /XObject /Subtype /Form /BBox [0 0 100 24]>>");
        }

        private static byte[] CreateWidgetPdf(string annotsValue, string normalAppearanceReference, string appearanceBody, bool includeAppearanceObject = true)
        {
            var objects = new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots {annotsValue} /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [5] = "[6 0 R]",
                [6] = $"<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP << /N {normalAppearanceReference} >> >>"
            };

            if (includeAppearanceObject)
            {
                objects[8] = appearanceBody;
            }

            return BuildPdf(objects);
        }

        private static byte[] BuildPdf(IDictionary<int, string> bodies)
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
                writer.Write($"{objectNumber} 0 obj\n{bodies[objectNumber]}\nendobj\n");
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

        private static string FormXObjectStream(string content)
        {
            var contentLength = Encoding.ASCII.GetByteCount(content);
            return $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 24] /Length {contentLength} >>\nstream\n{content}\nendstream";
        }
    }
}
