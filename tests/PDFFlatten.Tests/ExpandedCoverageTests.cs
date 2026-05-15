using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PDFFlatten;
using PDFFlatten.Internals;

namespace PDFFlatten.Tests;

public sealed partial class ExpandedCoverageTests
{
    [Test]
    public void Flatten_rejects_rotated_pages()
    {
        AssertRejects(
            SyntheticCoverageFixtureFactory.CreateRotatedPagePdf(),
            "/Rotate");
    }

    [Test]
    public void Flatten_rejects_non_identity_appearance_matrices()
    {
        AssertRejects(
            SyntheticCoverageFixtureFactory.CreateAppearanceMatrixPdf(),
            "/Matrix");
    }

    [Test]
    public void Flatten_rejects_checkbox_state_appearance_dictionaries()
    {
        AssertRejects(
            SyntheticCoverageFixtureFactory.CreateCheckboxStateAppearancePdf(),
            "state dictionaries");
    }

    [Test]
    public void Flatten_rejects_radio_state_appearance_dictionaries()
    {
        AssertRejects(
            SyntheticCoverageFixtureFactory.CreateRadioStateAppearancePdf(),
            "state dictionaries");
    }

    [Test]
    public void Flatten_flattens_self_contained_hierarchical_widget_fields()
    {
        var originalBytes = SyntheticCoverageFixtureFactory.CreateHierarchicalFieldPdf();
        var originalDocument = PdfParser.Parse(originalBytes);
        var originalCatalog = originalDocument.GetRequiredDictionary(originalDocument.Trailer.RequireReference("Root"));

        Assert.That(GetQualifiedFieldNames(originalDocument, originalCatalog), Is.EquivalentTo(new[] { "Group.Child" }));

        using var input = new MemoryStream(originalBytes, writable: false);
        using var flattened = PdfFlattener.Flatten(input);

        var flattenedDocument = PdfParser.Parse(ReadAllBytes(flattened));
        var flattenedCatalog = flattenedDocument.GetRequiredDictionary(flattenedDocument.Trailer.RequireReference("Root"));
        var flattenedPage = GetFirstPage(flattenedDocument, flattenedCatalog);
        var pageResources = ResolveDirectOrIndirectDictionary(flattenedDocument, flattenedPage, "Resources");
        var xObjects = ResolveDirectOrIndirectDictionary(flattenedDocument, pageResources, "XObject");

        PdfValue? acroForm = null;
        Assert.That(flattenedCatalog.TryGetValue("AcroForm", out acroForm), Is.False);
        Assert.That(flattenedPage.TryGetValue("Annots", out var annots), Is.False);
        Assert.That(xObjects.Items.Keys.Single(), Is.EqualTo("FldFlat001"));
        Assert.That(GetFlattenedDrawPlacements(flattenedDocument, flattenedPage).Select(item => item.ResourceName), Is.EqualTo(new[] { "FldFlat001" }));
    }

    [Test]
    public void Flatten_preserves_multi_filter_widget_appearances()
    {
        using var input = new MemoryStream(SyntheticCoverageFixtureFactory.CreateMultiFilterAppearancePdf(), writable: false);
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var xObjects = ResolveDirectOrIndirectDictionary(document, ResolveDirectOrIndirectDictionary(document, page, "Resources"), "XObject");
        var appearanceReference = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(appearanceReference, Is.Not.Null);

        var appearance = document.GetRequiredStream(appearanceReference!);
        var filters = appearance.Dictionary.RequireArray("Filter").Items
            .OfType<PdfName>()
            .Select(name => name.Value)
            .ToArray();

        Assert.That(filters, Is.EqualTo(new[] { "ASCIIHexDecode", "FlateDecode" }));
        Assert.That(GetFlattenedDrawPlacements(document, page).Select(item => item.ResourceName), Is.EqualTo(new[] { "FldFlat001" }));
    }

    [Test]
    public void Flatten_replays_original_widget_appearances_with_expected_transforms()
    {
        var originalBytes = SyntheticCoverageFixtureFactory.CreateTransformComparisonPdf();
        var originalDocument = PdfParser.Parse(originalBytes);
        var originalCatalog = originalDocument.GetRequiredDictionary(originalDocument.Trailer.RequireReference("Root"));
        var originalPage = GetFirstPage(originalDocument, originalCatalog);
        var originalWidgets = GetWidgetAppearances(originalDocument, originalPage)
            .ToDictionary(widget => widget.AppearanceReference.ObjectNumber);

        using var input = new MemoryStream(originalBytes, writable: false);
        using var flattened = PdfFlattener.Flatten(input);

        var flattenedDocument = PdfParser.Parse(ReadAllBytes(flattened));
        var flattenedCatalog = flattenedDocument.GetRequiredDictionary(flattenedDocument.Trailer.RequireReference("Root"));
        var flattenedPage = GetFirstPage(flattenedDocument, flattenedCatalog);
        var flattenedResources = ResolveDirectOrIndirectDictionary(flattenedDocument, flattenedPage, "Resources");
        var xObjects = ResolveDirectOrIndirectDictionary(flattenedDocument, flattenedResources, "XObject");
        var placements = GetFlattenedDrawPlacements(flattenedDocument, flattenedPage)
            .ToDictionary(item => item.ResourceName, StringComparer.Ordinal);

        Assert.That(placements.Keys, Is.EquivalentTo(new[] { "FldFlat001", "FldFlat002" }));

        foreach (var resource in xObjects.Items.Where(item => item.Key.StartsWith("FldFlat", StringComparison.Ordinal)))
        {
            var appearanceReference = resource.Value as PdfIndirectReference;
            Assert.That(appearanceReference, Is.Not.Null, $"/{resource.Key} should point to the original appearance stream.");

            var originalWidget = originalWidgets[((PdfIndirectReference)resource.Value).ObjectNumber];
            var expected = ComputeExpectedPlacement(originalWidget);
            var actual = placements[resource.Key];

            Assert.Multiple(() =>
            {
                Assert.That(actual.ScaleX, Is.EqualTo(expected.ScaleX).Within(0.001), $"{resource.Key} scaleX mismatch.");
                Assert.That(actual.ScaleY, Is.EqualTo(expected.ScaleY).Within(0.001), $"{resource.Key} scaleY mismatch.");
                Assert.That(actual.TranslateX, Is.EqualTo(expected.TranslateX).Within(0.001), $"{resource.Key} translateX mismatch.");
                Assert.That(actual.TranslateY, Is.EqualTo(expected.TranslateY).Within(0.001), $"{resource.Key} translateY mismatch.");
            });
        }
    }

    private static void AssertRejects(byte[] pdfBytes, string expectedMessageFragment)
    {
        using var input = new MemoryStream(pdfBytes, writable: false);

        var exception = Assert.Throws<NotSupportedException>(() =>
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

    private static PdfDictionary ResolveDirectOrIndirectDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        Assert.That(parent.TryGetValue(key, out var value), Is.True, $"Expected /{key} to exist.");
        return value switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException($"Unexpected /{key} value.")
        };
    }

    private static IReadOnlyList<string> GetQualifiedFieldNames(PdfDocument document, PdfDictionary catalog)
    {
        PdfValue? acroFormValue = null;
        Assert.That(catalog.TryGetValue("AcroForm", out acroFormValue), Is.True);

        var acroForm = acroFormValue switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => document.GetRequiredDictionary(reference),
            _ => throw new InvalidOperationException("Unexpected /AcroForm value.")
        };

        var names = new List<string>();
        foreach (var fieldValue in acroForm.RequireArray("Fields").Items.OfType<PdfIndirectReference>())
        {
            CollectFieldNames(document, document.GetRequiredDictionary(fieldValue), null, names);
        }

        return names;
    }

    private static void CollectFieldNames(PdfDocument document, PdfDictionary field, string? parentName, ICollection<string> names)
    {
        var partialName = field.TryGetValue("T", out var nameValue) && nameValue is PdfLiteralString literal
            ? literal.Value
            : null;
        var qualifiedName = string.IsNullOrEmpty(parentName)
            ? partialName
            : string.IsNullOrEmpty(partialName)
                ? parentName
                : parentName + "." + partialName;

        if (field.TryGetValue("Subtype", out var subtypeValue)
            && subtypeValue is PdfName subtypeName
            && string.Equals(subtypeName.Value, "Widget", StringComparison.Ordinal)
            && !string.IsNullOrEmpty(qualifiedName))
        {
            names.Add(qualifiedName);
        }

        if (!field.TryGetValue("Kids", out var kidsValue) || kidsValue is not PdfArray kids)
        {
            return;
        }

        foreach (var kidReference in kids.Items.OfType<PdfIndirectReference>())
        {
            CollectFieldNames(document, document.GetRequiredDictionary(kidReference), qualifiedName, names);
        }
    }

    private static IReadOnlyList<WidgetAppearance> GetWidgetAppearances(PdfDocument document, PdfDictionary page)
    {
        var annots = page.RequireArray("Annots");
        var widgets = new List<WidgetAppearance>();

        foreach (var annotReference in annots.Items.OfType<PdfIndirectReference>())
        {
            var annotation = document.GetRequiredDictionary(annotReference);
            if (!annotation.TryGetValue("Subtype", out var subtypeValue)
                || subtypeValue is not PdfName subtypeName
                || !string.Equals(subtypeName.Value, "Widget", StringComparison.Ordinal))
            {
                continue;
            }

            var appearanceReference = annotation.RequireDictionary("AP").RequireReference("N");
            var appearance = document.GetRequiredStream(appearanceReference);
            widgets.Add(new WidgetAppearance(
                appearanceReference,
                GetNumbers(annotation.RequireArray("Rect")),
                GetNumbers(appearance.Dictionary.RequireArray("BBox"))));
        }

        return widgets;
    }

    private static DrawPlacement ComputeExpectedPlacement(WidgetAppearance widget)
    {
        var boxWidth = widget.BBox[2] - widget.BBox[0];
        var boxHeight = widget.BBox[3] - widget.BBox[1];
        var rectWidth = widget.Rect[2] - widget.Rect[0];
        var rectHeight = widget.Rect[3] - widget.Rect[1];
        var scaleX = rectWidth / boxWidth;
        var scaleY = rectHeight / boxHeight;

        return new DrawPlacement(
            string.Empty,
            scaleX,
            scaleY,
            widget.Rect[0] - (widget.BBox[0] * scaleX),
            widget.Rect[1] - (widget.BBox[1] * scaleY));
    }

    private static IReadOnlyList<DrawPlacement> GetFlattenedDrawPlacements(PdfDocument document, PdfDictionary page)
    {
        var appendedReference = page.RequireArray("Contents").Items.Last() as PdfIndirectReference;
        Assert.That(appendedReference, Is.Not.Null);

        var content = Encoding.ASCII.GetString(document.GetRequiredStream(appendedReference!).Data);
        return DrawOperationRegex()
            .Matches(content)
            .Select(match => new DrawPlacement(
                match.Groups["name"].Value,
                double.Parse(match.Groups["sx"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(match.Groups["sy"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(match.Groups["tx"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(match.Groups["ty"].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static double[] GetNumbers(PdfArray array)
    {
        return array.Items.Select(PdfValueConversions.RequireNumber).ToArray();
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

    [GeneratedRegex(@"q\s+(?<sx>[-0-9.]+)\s+0\s+0\s+(?<sy>[-0-9.]+)\s+(?<tx>[-0-9.]+)\s+(?<ty>[-0-9.]+)\s+cm\s+/(?<name>FldFlat\d+)\s+Do\s+Q", RegexOptions.CultureInvariant)]
    private static partial Regex DrawOperationRegex();

    private sealed record WidgetAppearance(PdfIndirectReference AppearanceReference, double[] Rect, double[] BBox);

    private sealed record DrawPlacement(string ResourceName, double ScaleX, double ScaleY, double TranslateX, double TranslateY);

    private static class SyntheticCoverageFixtureFactory
    {
        public static byte[] CreateRotatedPagePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /Rotate 90 /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Rotated) /V (Value) /AP <</N 8 0 R>>>>",
                [8] = Stream("q 0 0 100 24 re W n Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24]")
            });
        }

        public static byte[] CreateAppearanceMatrixPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Matrix) /V (Value) /AP <</N 8 0 R>>>>",
                [8] = Stream("q 0 0 100 24 re W n Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Matrix [0 1 -1 0 0 0]")
            });
        }

        public static byte[] CreateCheckboxStateAppearancePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 44 44] /FT /Btn /T (Checkbox) /AS /Yes /AP <</N <</Off 8 0 R /Yes 9 0 R>>>>>>",
                [8] = Stream("q 0 0 24 24 re S Q", "/Type /XObject /Subtype /Form /BBox [0 0 24 24]"),
                [9] = Stream("q 0 0 m 24 24 l S Q", "/Type /XObject /Subtype /Form /BBox [0 0 24 24]")
            });
        }

        public static byte[] CreateRadioStateAppearancePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 44 44] /FT /Btn /Ff 32768 /T (Radio) /AS /ChoiceA /AP <</N <</Off 8 0 R /ChoiceA 9 0 R>>>>>>",
                [8] = Stream("q 12 12 10 0 360 arc S Q", "/Type /XObject /Subtype /Form /BBox [0 0 24 24]"),
                [9] = Stream("q 12 12 6 0 360 arc f Q", "/Type /XObject /Subtype /Form /BBox [0 0 24 24]")
            });
        }

        public static byte[] CreateHierarchicalFieldPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [7 0 R] /Contents 4 0 R>>",
                [4] = Stream("BT /F1 12 Tf 20 150 Td (Base page) Tj ET"),
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</T (Group) /Kids [7 0 R]>>",
                [7] = "<</Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [20 20 120 44] /FT /Tx /T (Child) /V (Hierarchy Value) /AP <</N 8 0 R>>>>",
                [8] = Stream("q 0 0 100 24 re W n BT /F1 12 Tf 2 8 Td (Hierarchy Value) Tj ET Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font <</F1 10 0 R>>>>"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"
            });
        }

        public static byte[] CreateMultiFilterAppearancePdf()
        {
            return BuildPdf(new Dictionary<int, byte[]>
            {
                [1] = AsciiBody("<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>"),
                [2] = AsciiBody("<</Type /Pages /Count 1 /Kids [3 0 R]>>"),
                [3] = AsciiBody("<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 4 0 R>>"),
                [4] = AsciiBody(Stream("q Q")),
                [5] = AsciiBody("<</XObject <<>>>>"),
                [6] = AsciiBody("<</Type /Annot /Subtype /Widget /Rect [20 20 80 80] /FT /Btn /T (MultiFilter) /AP <</N 8 0 R>>>>"),
                [8] = MultiFilterStream("q 10 10 40 40 re f Q", "/Type /XObject /Subtype /Form /BBox [0 0 60 60]")
            });
        }

        public static byte[] CreateTransformComparisonPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R 7 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources 5 0 R /Annots [6 0 R 7 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [10 20 110 40] /FT /Btn /T (One) /AP <</N 8 0 R>>>>",
                [7] = "<</Type /Annot /Subtype /Widget /Rect [50 80 250 120] /FT /Btn /T (Two) /AP <</N 9 0 R>>>>",
                [8] = Stream("q 0 0 100 20 re S Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 20]"),
                [9] = Stream("q -10 -5 100 20 re S Q", "/Type /XObject /Subtype /Form /BBox [-10 -5 90 15]")
            });
        }

        private static byte[] BuildPdf(IDictionary<int, string> bodies)
        {
            return BuildPdf(bodies.ToDictionary(pair => pair.Key, pair => AsciiBody(pair.Value)));
        }

        private static byte[] BuildPdf(IDictionary<int, byte[]> bodies)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                NewLine = "\n"
            };

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

        private static string Stream(string content, string? dictionaryEntries = null)
        {
            var prefix = string.IsNullOrWhiteSpace(dictionaryEntries) ? string.Empty : $" {dictionaryEntries}";
            return $"<<{prefix} /Length {Encoding.ASCII.GetByteCount(content)}>>\nstream\n{content}\nendstream";
        }

        private static byte[] MultiFilterStream(string content, string dictionaryEntries)
        {
            var compressed = Compress(Encoding.ASCII.GetBytes(content));
            var encoded = Encoding.ASCII.GetBytes(ToAsciiHex(compressed));

            using var stream = new MemoryStream();
            stream.Write(Encoding.ASCII.GetBytes($"<< {dictionaryEntries} /Filter [/ASCIIHexDecode /FlateDecode] /Length {encoded.Length} >>\nstream\n"));
            stream.Write(encoded, 0, encoded.Length);
            stream.Write(Encoding.ASCII.GetBytes("\nendstream"));
            return stream.ToArray();
        }

        private static byte[] AsciiBody(string content) => Encoding.ASCII.GetBytes(content);

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var compressor = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        private static string ToAsciiHex(byte[] data)
        {
            var builder = new StringBuilder((data.Length * 2) + 1);
            foreach (var b in data)
            {
                builder.AppendFormat("{0:X2}", b);
            }

            builder.Append('>');
            return builder.ToString();
        }
    }
}
