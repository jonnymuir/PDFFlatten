using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using PDFFlatten;
using PDFFlatten.Internals;

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
    public void Flatten_rejects_digital_signature_fields()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateSignatureWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("/Sig"));
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

    [Test]
    public void Flatten_rejects_input_streams_larger_than_supported_limit()
    {
        using var input = new RepeatingByteStream(PdfSecurityLimits.MaxInputBytes + 1L);

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("PDF inputs larger than"));
    }

    [Test]
    public void Flatten_rejects_stream_lengths_larger_than_supported_limit()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateOversizedDirectStreamLengthPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("Streams longer than"));
    }

    [Test]
    public void Flatten_rejects_flate_decode_appearance_amplification_beyond_inspection_cap()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateOversizedFlateAppearanceWidgetPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("FlateDecode appearance content"));
    }

    [Test]
    public void Flatten_normalizes_malformed_startxref_overflow_to_invalid_operation()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateStartXrefOverflowPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("startxref offset"));
    }

    [Test]
    public void Flatten_normalizes_negative_stream_lengths_to_invalid_operation()
    {
        using var input = new MemoryStream(UnsupportedPdfFactory.CreateNegativeStreamLengthPdf());

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Stream /Length must be non-negative"));
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

        public static byte[] CreateSignatureWidgetPdf()
        {
            return BuildStandardWidgetPdf(
                widgetBody: "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Sig /T (SignedApproval) /V <</Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached>> /AP <</N 8 0 R>>>>");
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

        public static byte[] CreateOversizedFlateAppearanceWidgetPdf()
        {
            var decodedBytes = Enumerable.Repeat((byte)'A', PdfSecurityLimits.MaxDecodedAppearanceBytes + 1024).ToArray();
            var compressedBytes = Compress(decodedBytes);

            return BuildStandardWidgetPdf(
                binaryAdditionalObjects: new Dictionary<int, byte[]>
                {
                    [8] = Stream(
                        compressedBytes,
                        "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font <</F1 10 0 R>>>> /Filter /FlateDecode")
                });
        }

        public static byte[] CreateOversizedDirectStreamLengthPdf()
        {
            return BuildStandardWidgetPdf(
                additionalObjects: new Dictionary<int, string>
                {
                    [7] = $"<< /Length {PdfSecurityLimits.MaxStreamBytes + 1} >>\nstream\nq Q\nendstream"
                });
        }

        public static byte[] CreateStartXrefOverflowPdf()
        {
            const string pdf = "%PDF-1.4\n"
                               + "1 0 obj\n"
                               + "<</Type /Catalog /Pages 2 0 R>>\n"
                               + "endobj\n"
                               + "2 0 obj\n"
                               + "<</Type /Pages /Count 0 /Kids []>>\n"
                               + "endobj\n"
                               + "xref\n"
                               + "0 3\n"
                               + "0000000000 65535 f \n"
                               + "0000000009 00000 n \n"
                               + "0000000058 00000 n \n"
                               + "trailer\n"
                               + "<</Size 3 /Root 1 0 R>>\n"
                               + "startxref\n"
                               + "999999999999999999999\n"
                               + "%%EOF";
            return Encoding.ASCII.GetBytes(pdf);
        }

        public static byte[] CreateNegativeStreamLengthPdf()
        {
            return BuildStandardWidgetPdf(
                additionalObjects: new Dictionary<int, string>
                {
                    [7] = "<< /Length -1 >>\nstream\nq Q\nendstream"
                });
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
            IDictionary<int, byte[]>? binaryAdditionalObjects = null,
            string trailerEntries = "")
        {
            var bodies = new Dictionary<int, byte[]>
            {
                [1] = Ascii("<</Type /Catalog /Pages 2 0 R /AcroForm 5 0 R>>"),
                [2] = Ascii(pagesBody ?? "<</Type /Pages /Count 1 /Kids [3 0 R]>>"),
                [3] = Ascii(pageBody ?? "<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 9 0 R /Annots [6 0 R] /Contents 7 0 R>>"),
                [5] = Ascii(acroFormBody ?? "<</Fields [6 0 R]>>"),
                [6] = Ascii(widgetBody ?? "<</Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /AP <</N 8 0 R>> /V (Filled)>>"),
                [7] = Ascii(Stream("q Q")),
                [8] = Ascii(Stream("q 0 0 100 24 re W n BT /F1 12 Tf 2 8 Td (Filled) Tj ET Q", "/Type /XObject /Subtype /Form /BBox [0 0 100 24] /Resources <</Font <</F1 10 0 R>>>>")),
                [9] = Ascii(resourcesBody ?? "<</Font <</F1 10 0 R>> /XObject <<>>>>"),
                [10] = Ascii("<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>")
            };

            if (additionalObjects is not null)
            {
                foreach (var pair in additionalObjects)
                {
                    bodies[pair.Key] = Ascii(pair.Value);
                }
            }

            if (binaryAdditionalObjects is not null)
            {
                foreach (var pair in binaryAdditionalObjects)
                {
                    bodies[pair.Key] = pair.Value;
                }
            }

            return BuildPdf(bodies, trailerEntries);
        }

        private static byte[] BuildPdf(IDictionary<int, byte[]> bodies, string trailerEntries)
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

        private static byte[] Stream(byte[] content, string dictionaryEntries = "")
        {
            var prefix = Encoding.ASCII.GetBytes($"<<{dictionaryEntries} /Length {content.Length}>>\nstream\n");
            var suffix = Encoding.ASCII.GetBytes("\nendstream");
            using var stream = new MemoryStream();
            stream.Write(prefix, 0, prefix.Length);
            stream.Write(content, 0, content.Length);
            stream.Write(suffix, 0, suffix.Length);
            return stream.ToArray();
        }

        private static byte[] Compress(byte[] content)
        {
            using var stream = new MemoryStream();
            using (var deflater = new DeflateStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                deflater.Write(content, 0, content.Length);
            }

            return stream.ToArray();
        }

        private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
    }

    private sealed class RepeatingByteStream : Stream
    {
        private readonly long _length;

        public RepeatingByteStream(long length)
        {
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _length - Position;
            if (remaining <= 0)
            {
                return 0;
            }

            var read = (int)Math.Min(count, remaining);
            Array.Clear(buffer, offset, read);
            Position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return Position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }
    }
}
