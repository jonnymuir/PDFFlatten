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
    public void Flatten_supports_single_hop_indirect_stream_lengths()
    {
        using var input = new MemoryStream(UnsupportedPdfFixtureFactory.CreateSingleHopIndirectStreamLengthWidgetPdf());
        AssertSingleHopIndirectStreamLengthFlattens(input);
    }

    [Test]
    public void Flatten_supports_single_hop_indirect_stream_lengths_when_length_objects_precede_streams()
    {
        using var input = new MemoryStream(UnsupportedPdfFixtureFactory.CreateSingleHopIndirectStreamLengthWidgetPdfWithLeadingLengthObjects());
        AssertSingleHopIndirectStreamLengthFlattens(input);
    }

    [Test]
    public void Flatten_supports_empty_password_rc4_encrypted_need_appearances_widgets()
    {
        using var input = new MemoryStream(EncryptedPdfFixtureFactory.CreateEmptyPasswordRc4EncryptedNeedAppearancesWidgetPdf(), writable: false);

        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var pageResources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, pageResources, "XObject");
        var flattenedAppearanceReference = xObjects.Items["FldFlat001"] as PdfIndirectReference;

        Assert.That(flattenedAppearanceReference, Is.Not.Null);

        var flattenedAppearance = document.GetRequiredStream(flattenedAppearanceReference!);
        var appearanceCommands = Encoding.Latin1.GetString(flattenedAppearance.Data);

        Assert.Multiple(() =>
        {
            PdfValue? ignored = null;
            Assert.That(document.Trailer.TryGetValue("Encrypt", out ignored), Is.False);
            Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
            Assert.That(page.TryGetValue("Annots", out ignored), Is.False);
            Assert.That(xObjects.Items.ContainsKey("FldFlat001"), Is.True);
            Assert.That(appearanceCommands, Does.Contain("£1.46"));
        });
    }

    private static void AssertSingleHopIndirectStreamLengthFlattens(Stream input)
    {
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var pageResources = ResolvePageResources(document, page);
        var xObjects = ResolveNestedDictionary(document, pageResources, "XObject");

        Assert.Multiple(() =>
        {
            PdfValue? ignored = null;
            Assert.That(catalog.TryGetValue("AcroForm", out ignored), Is.False);
            Assert.That(page.TryGetValue("Annots", out ignored), Is.False);
            Assert.That(xObjects.Items.ContainsKey("FldFlat001"), Is.True);
        });
    }

    [Test]
    public void Flatten_rejects_chained_indirect_stream_lengths()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateChainedIndirectStreamLengthPdf(),
            "Indirect stream /Length objects must resolve directly to an integer.");
    }

    [Test]
    public void Flatten_rejects_cyclic_indirect_stream_lengths()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateCyclicIndirectStreamLengthPdf(),
            "Indirect stream /Length objects must resolve directly to an integer.");
    }

    [Test]
    public void Flatten_rejects_non_integer_indirect_stream_lengths()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateNonIntegerIndirectStreamLengthPdf(),
            "Indirect stream /Length objects must resolve directly to an integer.");
    }

    [Test]
    public void Flatten_rejects_missing_indirect_stream_length_objects()
    {
        AssertRejects<InvalidOperationException>(
            UnsupportedPdfFixtureFactory.CreateMissingIndirectStreamLengthObjectPdf(),
            "Stream /Length reference 11 0 R was not found.");
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
    public void Flatten_rejects_widgets_without_appearance_dictionaries()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateWidgetWithoutAppearanceDictionaryPdf(),
            "widget-local /DR");
    }

    [Test]
    public void Flatten_rejects_widgets_with_non_dictionary_appearance_values()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateWidgetWithNonDictionaryAppearanceValuePdf(),
            "normal appearance dictionary at /AP");
    }

    [Test]
    public void Flatten_rejects_widgets_with_appearance_dictionaries_that_omit_normal_appearances()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateWidgetWithAppearanceDictionaryMissingNormalAppearancePdf(),
            "normal appearance at /AP /N");
    }

    [Test]
    public void Flatten_rejects_need_appearances_widgets_with_centered_quadding()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateNeedAppearancesWidgetWithCenteredQuaddingPdf(),
            "left-aligned or right-aligned widgets only");
    }

    [Test]
    public void Flatten_rejects_need_appearances_widgets_with_password_flags()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateNeedAppearancesWidgetWithPasswordFlagPdf(),
            "simple single-line widgets, right-aligned single-line widgets, and left-aligned multiline widgets only");
    }

    [Test]
    public void Flatten_rejects_need_appearances_widgets_with_multiline_right_aligned_quadding()
    {
        AssertRejects<NotSupportedException>(
            UnsupportedPdfFixtureFactory.CreateNeedAppearancesMultilineRightAlignedWidgetPdf(),
            "left-aligned multiline widgets only");
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

    [Test]
    public void Flatten_prunes_structure_tree_widget_object_references()
    {
        using var input = new MemoryStream(UnsupportedPdfFixtureFactory.CreateWidgetPdfWithStructureTreeObjectReference());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var structureTreeRoot = document.GetRequiredDictionary(catalog.RequireReference("StructTreeRoot"));
        var parentTree = document.GetRequiredDictionary(structureTreeRoot.RequireReference("ParentTree"));
        var parentTreeNums = parentTree.RequireArray("Nums");
        var widgetCount = document.Objects.Count(ContainsWidgetSubtype);
        var objrCount = CountObjrReferences(document, structureTreeRoot);

        Assert.Multiple(() =>
        {
            Assert.That(widgetCount, Is.Zero);
            Assert.That(objrCount, Is.Zero);
            Assert.That(parentTreeNums.Items.Count, Is.EqualTo(2));
            Assert.That(parentTreeNums.Items[0], Is.TypeOf<PdfNumber>());
            Assert.That(((PdfNumber)parentTreeNums.Items[0]).RawValue, Is.EqualTo("0"));
        });
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

        public static byte[] CreateSingleHopIndirectStreamLengthWidgetPdf()
        {
            const string pageContent = "q % endstream marker\nQ";
            const string appearanceContent = "q (endstream marker) Tj Q";

            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = StreamWithIndirectLength(pageContent, 11),
                [6] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP <</N 8 0 R>>>>",
                [8] = StreamWithIndirectLength(appearanceContent, 12, "/Type /XObject /Subtype /Form /BBox [0 0 100 24]"),
                [11] = Encoding.ASCII.GetByteCount(pageContent).ToString(),
                [12] = Encoding.ASCII.GetByteCount(appearanceContent).ToString()
            });
        }

        public static byte[] CreateSingleHopIndirectStreamLengthWidgetPdfWithLeadingLengthObjects()
        {
            const string pageContent = "q % endstream marker\nQ";
            const string appearanceContent = "q (endstream marker) Tj Q";

            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [7 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [7 0 R] /Contents 5 0 R>>",
                [4] = Encoding.ASCII.GetByteCount(pageContent).ToString(),
                [5] = StreamWithIndirectLength(pageContent, 4),
                [6] = Encoding.ASCII.GetByteCount(appearanceContent).ToString(),
                [7] = "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP <</N 8 0 R>>>>",
                [8] = StreamWithIndirectLength(appearanceContent, 6, "/Type /XObject /Subtype /Form /BBox [0 0 100 24]")
            });
        }

        public static byte[] CreateChainedIndirectStreamLengthPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R>>",
                [4] = StreamWithIndirectLength("BT ET", 11),
                [11] = "12 0 R",
                [12] = "5"
            });
        }

        public static byte[] CreateCyclicIndirectStreamLengthPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R>>",
                [4] = "<< /Length 4 0 R >>\nstream\nBT ET\nendstream"
            });
        }

        public static byte[] CreateNonIntegerIndirectStreamLengthPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R>>",
                [4] = StreamWithIndirectLength("BT ET", 11),
                [11] = "5.5"
            });
        }

        public static byte[] CreateMissingIndirectStreamLengthObjectPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R>>",
                [4] = StreamWithIndirectLength("BT ET", 11)
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

        public static byte[] CreateWidgetWithoutAppearanceDictionaryPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) >>"
            });
        }

        public static byte[] CreateWidgetWithNonDictionaryAppearanceValuePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP 8 0 R >>",
                [8] = FormXObjectStream("q 0 0 100 24 re W n Q")
            });
        }

        public static byte[] CreateWidgetWithAppearanceDictionaryMissingNormalAppearancePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP <<>> >>"
            });
        }

        public static byte[] CreateNeedAppearancesWidgetWithCenteredQuaddingPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /Q 1 /DA (0 g /He 12 Tf) /DR <</Font 8 0 R>> /V (Value 01) >>",
                [8] = "<</He 9 0 R>>",
                [9] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>"
            });
        }

        public static byte[] CreateNeedAppearancesWidgetWithPasswordFlagPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /Ff 8192 /DA (0 g /He 12 Tf) /DR <</Font 8 0 R>> /V (secret) >>",
                [8] = "<</He 9 0 R>>",
                [9] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>"
            });
        }

        public static byte[] CreateNeedAppearancesMultilineRightAlignedWidgetPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R] /NeedAppearances true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 80] /FT /Tx /Q 2 /Ff 4096 /DA (0 g /He 12 Tf) /DR <</Font 8 0 R>> /V (Line 1\nLine 2) >>",
                [8] = "<</He 9 0 R>>",
                [9] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>"
            });
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

        private static string StreamWithIndirectLength(string content, int lengthObjectNumber, string dictionaryEntries = "")
        {
            var prefix = string.IsNullOrWhiteSpace(dictionaryEntries) ? string.Empty : dictionaryEntries + " ";
            return $"<< {prefix}/Length {lengthObjectNumber} 0 R >>\nstream\n{content}\nendstream";
        }

        public static byte[] CreateWidgetPdfWithStructureTreeObjectReference()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>> /StructTreeRoot 11 0 R /MarkInfo <</Marked true>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /StructParents 0 /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R>>",
                [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
                [6] = "<</Type /Annot /Subtype /Widget /StructParent 1 /Rect [20 20 120 44] /FT /Tx /P 3 0 R /Parent 7 0 R /T (Field01) /V (Value 01) /AP <</N 8 0 R>>>>",
                [7] = "<</Kids [6 0 R] /T (RootField)>>",
                [8] = FormXObjectStream("q 0 0 100 24 re W n Q"),
                [11] = "<</Type /StructTreeRoot /ParentTree 12 0 R /K [13 0 R]>>",
                [12] = "<</Nums [0 [13 0 R] 1 13 0 R]>>",
                [13] = "<</Type /StructElem /S /Form /P 11 0 R /Pg 3 0 R /K [<</Type /OBJR /Obj 6 0 R>>]>>"
            });
        }
    }

    private static bool ContainsWidgetSubtype(PdfIndirectObject pdfObject)
    {
        return pdfObject.Value is PdfDictionary dictionary
               && dictionary.TryGetValue("Subtype", out var subtypeValue)
               && subtypeValue is PdfName subtypeName
               && subtypeName.Value == "Widget";
    }

    private static int CountObjrReferences(PdfDocument document, PdfValue value)
    {
        return CountObjrReferences(document, value, new HashSet<int>());
    }

    private static int CountObjrReferences(PdfDocument document, PdfValue value, ISet<int> visitedObjects)
    {
        switch (value)
        {
            case PdfIndirectReference reference:
                return visitedObjects.Add(reference.ObjectNumber)
                    ? CountObjrReferences(document, document.GetRequiredObject(reference).Value, visitedObjects)
                    : 0;
            case PdfStream stream:
                return CountObjrReferences(document, stream.Dictionary, visitedObjects);
            case PdfArray array:
                return array.Items.Sum(item => CountObjrReferences(document, item, visitedObjects));
            case PdfDictionary dictionary:
                var count = dictionary.TryGetValue("Type", out var typeValue)
                            && typeValue is PdfName typeName
                            && typeName.Value == "OBJR"
                    ? 1
                    : 0;
                return count + dictionary.Items.Values.Sum(item => CountObjrReferences(document, item, visitedObjects));
            default:
                return 0;
        }
    }
}
