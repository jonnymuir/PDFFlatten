using System.Text;
using NUnit.Framework;
using PDFFlatten;

namespace PDFFlatten.Tests;

public sealed class ParserHardeningTests
{
    [Test]
    public void Flatten_rejects_encrypted_pdfs()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateEncryptedWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("/Encrypt"));
    }

    [Test]
    public void Flatten_rejects_xref_stream_pdfs()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateXrefStreamPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Cross-reference streams"));
    }

    [Test]
    public void Flatten_rejects_object_stream_pdfs()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateObjectStreamPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("/ObjStm"));
    }

    [Test]
    public void Flatten_rejects_incremental_update_pdfs()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateIncrementalUpdateLikePdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("/Prev"));
    }

    [Test]
    public void Flatten_rejects_xfa_forms()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateXfaWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("XFA"));
    }

    [Test]
    public void Flatten_rejects_indirect_page_annotation_arrays()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateIndirectAnnotsWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("/Annots"));
    }

    [Test]
    public void Flatten_rejects_pages_with_inherited_resources()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateInheritedResourcesWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("inherit /Resources"));
    }

    [Test]
    public void Flatten_rejects_state_based_widget_appearances()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateStateAppearanceWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("state dictionaries"));
    }

    [Test]
    public void Flatten_rejects_unresolved_indirect_references_during_serialization()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateWidgetPdfWithUnresolvedResourceReference());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("unresolved indirect reference"));
    }

    private static class UnsupportedPdfFactory
    {
        public static byte[] CreateEncryptedWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                additionalObjects: new Dictionary<int, string>
                {
                    [11] = "<</Filter /Standard /V 1 /R 2 /Length 40>>"
                },
                trailerEntries: "/Encrypt 11 0 R");
        }

        public static byte[] CreateIncrementalUpdateLikePdf()
        {
            return BuildStandardWidgetPdf(trailerEntries: "/Prev 12");
        }

        public static byte[] CreateXfaWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                acroFormBody: "<</Fields [6 0 R] /XFA 11 0 R>>",
                additionalObjects: new Dictionary<int, string>
                {
                    [11] = Stream("xfa-template")
                });
        }

        public static byte[] CreateIndirectAnnotsWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                pageBody: "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 9 0 R /Annots 11 0 R /Contents 7 0 R>>",
                additionalObjects: new Dictionary<int, string>
                {
                    [11] = "[6 0 R]"
                });
        }

        public static byte[] CreateInheritedResourcesWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                pagesBody: "<</Type /Pages /Count 1 /Kids [3 0 R] /Resources 9 0 R>>",
                pageBody: "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [6 0 R] /Contents 7 0 R>>");
        }

        public static byte[] CreateStateAppearanceWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                widgetBody: "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Btn /AP <</N <</Yes 8 0 R>>>> /AS /Yes>>");
        }

        public static byte[] CreateObjectStreamPdf()
        {
            return BuildStandardWidgetPdf(
                additionalObjects: new Dictionary<int, string>
                {
                    [11] = Stream(string.Empty, "/Type /ObjStm /N 0 /First 0")
                });
        }

        public static byte[] CreateWidgetPdfWithUnresolvedResourceReference()
        {
            return BuildStandardWidgetPdf(
                resourcesBody: "<</Font <</F1 10 0 R>> /XObject <</Logo 99 0 R>>>>");
        }

        public static byte[] CreateXrefStreamPdf()
        {
            const string pdf = "%PDF-1.5\n"
                               + "1 0 obj\n"
                               + "<</Type /XRef /Length 0 /W [1 2 1] /Root 2 0 R /Size 3>>\n"
                               + "stream\n"
                               + "\n"
                               + "endstream\n"
                               + "endobj\n"
                               + "2 0 obj\n"
                               + "<</Type /Catalog /Pages 3 0 R>>\n"
                               + "endobj\n"
                               + "3 0 obj\n"
                               + "<</Type /Pages /Count 0 /Kids []>>\n"
                               + "endobj\n"
                               + "startxref\n"
                               + "9\n"
                               + "%%EOF";
            return Encoding.ASCII.GetBytes(pdf);
        }

        private static byte[] BuildStandardWidgetPdf(
            string? pagesBody = null,
            string? pageBody = null,
            string? acroFormBody = null,
            string? widgetBody = null,
            string? resourcesBody = null,
            IDictionary<int, string>? additionalObjects = null,
            string trailerEntries = "")
        {
            var bodies = new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm 5 0 R>>",
                [2] = pagesBody ?? "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = pageBody ?? "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 9 0 R /Annots [6 0 R] /Contents 7 0 R>>",
                [5] = acroFormBody ?? "<</Fields [6 0 R]>>",
                [6] = widgetBody ?? "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /AP <</N 8 0 R>> /V (Filled)>>",
                [7] = Stream("q Q"),
                [8] = Stream("q 0 0 100 24 re W n BT /F1 12 Tf 2 8 Td (Filled) Tj ET Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font <</F1 10 0 R>>>>"),
                [9] = resourcesBody ?? "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"
            };

            if (additionalObjects is not null)
            {
                foreach (var pair in additionalObjects)
                {
                    bodies[pair.Key] = pair.Value;
                }
            }

            return BuildPdf(bodies, trailerEntries);
        }

        private static byte[] BuildPdf(IDictionary<int, string> bodies, string trailerEntries)
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
                writer.Write(bodies[objectNumber]);
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
            writer.Write($"<</Size {size} /Root 1 0 R {trailerEntries}>>\n");
            writer.Write("startxref\n");
            writer.Write($"{xrefPosition}\n%%EOF");
            writer.Flush();
            return stream.ToArray();
        }

        private static string Stream(string content, string dictionaryEntries = "")
        {
            var bytes = Encoding.ASCII.GetBytes(content);
            return $"<<{dictionaryEntries} /Length {bytes.Length}>>\nstream\n{content}\nendstream";
        }
    }
}
