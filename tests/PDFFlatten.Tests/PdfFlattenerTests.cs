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
        Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
        Assert.That(page.TryGetValue("Annots", out ignored), Is.False);

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
        Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);

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
    public void Flatten_synthesizes_simple_need_appearances_text_widget_appearances()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesTextWidgetWithoutAppearance());
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
        var appearanceCommands = Encoding.ASCII.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            PdfValue? ignored = null;
            Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
            Assert.That(page.TryGetValue("Annots", out ignored), Is.False);
            Assert.That(fontResources.Items.ContainsKey("He"), Is.True);
            Assert.That(appearanceCommands, Does.Contain("/He 12 Tf"));
            Assert.That(appearanceCommands, Does.Contain("(Filled) Tj"));
            Assert.That(appearanceCommands, Does.Contain("0.1 0.2 0.3 rg"));
        });
    }

    [Test]
    public void Flatten_synthesizes_need_appearances_text_widget_appearances_when_default_appearance_uses_octal_escapes()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesTextWidgetWithOctalEscapedDefaultAppearance());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedAppearanceRef = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceRef, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceRef!);
        var appearanceCommands = Encoding.ASCII.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            PdfValue? ignored = null;
            Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
            Assert.That(page.TryGetValue("Annots", out ignored), Is.False);
            Assert.That(appearanceCommands, Does.Contain("/He 12 Tf"));
            Assert.That(appearanceCommands, Does.Contain("(Filled) Tj"));
        });
    }

    [Test]
    public void Flatten_synthesizes_right_aligned_need_appearances_text_widget_appearances()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesRightAlignedTextWidgetWithoutAppearance());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedAppearanceRef = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceRef, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceRef!);
        var appearanceCommands = Encoding.ASCII.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            Assert.That(appearanceCommands, Does.Contain("/He 12 Tf"));
            Assert.That(appearanceCommands, Does.Contain("1 0 0 1 61.304 8.4 Tm"));
            Assert.That(appearanceCommands, Does.Contain("(123.45) Tj"));
        });
    }

    [Test]
    public void Flatten_synthesizes_multiline_need_appearances_text_widget_appearances()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesMultilineTextWidgetWithoutAppearance());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedAppearanceRef = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceRef, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceRef!);
        var appearanceCommands = Encoding.ASCII.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            Assert.That(appearanceCommands, Does.Contain("/He 12 Tf"));
            Assert.That(appearanceCommands, Does.Contain("1 0 0 1 2 48.4 Tm (Line 1) Tj"));
            Assert.That(appearanceCommands, Does.Contain("1 0 0 1 2 34 Tm (Line 2) Tj"));
            Assert.That(appearanceCommands, Does.Contain("1 0 0 1 2 19.6 Tm (Line 3) Tj"));
        });
    }

    [Test]
    public void Flatten_wraps_multiline_need_appearances_text_widget_appearances()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesWrappedMultilineTextWidgetWithoutAppearance());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedAppearanceRef = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceRef, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceRef!);
        var appearanceCommands = Encoding.ASCII.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            Assert.That(appearanceCommands, Does.Contain("(Hyrule Castle) Tj"));
            Assert.That(appearanceCommands, Does.Contain("(Courtyard) Tj"));
        });
    }

    [Test]
    public void Flatten_supports_need_appearances_documents_with_mixed_multiline_and_right_aligned_text_widgets()
    {
        using var input = new MemoryStream(SimplePdfFactory.CreateDocumentWithNeedAppearancesMixedTextWidgetsWithoutAppearance());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var resources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, resources, "XObject");
        var flattenedResourceNames = xObjects.Items.Keys
            .Where(key => key.StartsWith("FldFlat", StringComparison.Ordinal))
            .ToArray();

        Assert.Multiple(() =>
        {
            PdfValue? ignored = null;
            Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
            Assert.That(page.TryGetValue("Annots", out ignored), Is.False);
            Assert.That(flattenedResourceNames, Has.Length.EqualTo(6));
        });
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
        Assert.That(page.TryGetValue("Resources", out resourcesValue), Is.True);
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
        Assert.That(parent.TryGetValue(key, out value), Is.True);
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

        public static byte[] CreateDocumentWithNeedAppearancesTextWidgetWithoutAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /DA (0.1 0.2 0.3 rg /He 12 Tf) /DR <</Font 11 0 R>> /V (Filled)>>",
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [11] = "<</He 10 0 R>>"
            });
        }

        public static byte[] CreateDocumentWithNeedAppearancesTextWidgetWithOctalEscapedDefaultAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /DA (0.1 0.2 0.3 rg /He 12 T\\146) /DR <</Font 11 0 R>> /V (Filled)>>",
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [11] = "<</He 10 0 R>>"
            });
        }

        public static byte[] CreateDocumentWithNeedAppearancesRightAlignedTextWidgetWithoutAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 11 0 R>> /V (123.45)>>",
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [11] = "<</He 10 0 R>>"
            });
        }

        public static byte[] CreateDocumentWithNeedAppearancesMultilineTextWidgetWithoutAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 220 220] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 160 80] /FT /Tx /Ff 4096 /DA (0 g /He 12 Tf) /DR <</Font 11 0 R>> /V (Line 1\\nLine 2\\rLine 3)>>",
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [11] = "<</He 10 0 R>>"
            });
        }

        public static byte[] CreateDocumentWithNeedAppearancesMixedTextWidgetsWithoutAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 500 500] /Resources 5 0 R /Annots [6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R] /Contents 12 0 R>>",
                [5] = "<</Font <</F1 13 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [40 360 200 440] /FT /Tx /Ff 4096 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (Line 1\\nLine 2)>>",
                [7] = "<</Type /Annot /Subtype /Widget /Rect [220 360 420 384] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (100.00)>>",
                [8] = "<</Type /Annot /Subtype /Widget /Rect [220 330 420 354] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (250.50)>>",
                [9] = "<</Type /Annot /Subtype /Widget /Rect [220 300 420 324] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (0.99)>>",
                [10] = "<</Type /Annot /Subtype /Widget /Rect [220 270 420 294] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (12.34)>>",
                [11] = "<</Type /Annot /Subtype /Widget /Rect [220 240 420 264] /FT /Tx /Q 2 /DA (0 g /He 12 Tf) /DR <</Font 14 0 R>> /V (999.01)>>",
                [12] = Stream("q Q"),
                [13] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [14] = "<</He 13 0 R>>"
            });
        }

        public static byte[] CreateDocumentWithNeedAppearancesWrappedMultilineTextWidgetWithoutAppearance()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 220 220] /Resources 5 0 R /Annots [6 0 R] /Contents 9 0 R>>",
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 100 80] /FT /Tx /Ff 4096 /DA (0 g /He 12 Tf) /DR <</Font 11 0 R>> /V (Hyrule Castle Courtyard)>>",
                [9] = Stream("q Q"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
                [11] = "<</He 10 0 R>>"
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
