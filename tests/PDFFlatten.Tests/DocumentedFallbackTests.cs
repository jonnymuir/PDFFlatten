using System.Text;
using NUnit.Framework;
using PDFFlatten;

namespace PDFFlatten.Tests;

public sealed class DocumentedFallbackTests
{
    [Test]
    public void Fallback_example_logs_warning_preserves_source_and_copies_original_for_unsupported_pdfs()
    {
        var originalBytes = File.ReadAllBytes(GetProducerCorpusFixturePath("reportlab-textfields-raw.pdf"));

        AssertDocumentedFallback(
            originalBytes,
            expectedWarningFragment: "outside PDFFlatten's supported slice",
            expectedExceptionMessageFragment: "Unsupported PDF keyword '.1'.");
    }

    [Test]
    public void Fallback_example_logs_warning_preserves_source_and_copies_original_for_malformed_pdfs()
    {
        var originalBytes = CreateMissingAppearanceReferencePdf();

        AssertDocumentedFallback(
            originalBytes,
            expectedWarningFragment: "malformed or incomplete",
            expectedExceptionMessageFragment: "Object 99 0 R was not found.");
    }

    private static void AssertDocumentedFallback(
        byte[] originalBytes,
        string expectedWarningFragment,
        string expectedExceptionMessageFragment)
    {
        var outputDirectory = Path.Combine(
            TestAssets.RepositoryRoot,
            "artifacts",
            "test-output",
            "documented-fallback",
            TestContext.CurrentContext.Test.ID);
        var inputPath = Path.Combine(outputDirectory, "input.pdf");
        var outputPath = Path.Combine(outputDirectory, "flattened-or-original.pdf");
        var warningLog = new StringWriter();

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(inputPath, originalBytes);

        try
        {
            FlattenWithDocumentedFallback(
                inputPath,
                outputPath,
                message => warningLog.WriteLine(message));

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(outputPath), Is.True, "Fallback flow should still leave an output PDF behind.");
                Assert.That(File.ReadAllBytes(inputPath), Is.EqualTo(originalBytes), "Fallback must leave the source PDF untouched.");
                Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(originalBytes), "Fallback output should be the original PDF bytes.");
                Assert.That(warningLog.ToString(), Does.Contain("Warning: PDF left unflattened"));
                Assert.That(warningLog.ToString(), Does.Contain(expectedWarningFragment));
                Assert.That(warningLog.ToString(), Does.Contain(expectedExceptionMessageFragment));
            });
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void FlattenWithDocumentedFallback(string inputPath, string outputPath, Action<string> logWarning)
    {
        try
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            PdfFlattener.Flatten(input, output);
        }
        catch (NotSupportedException exception)
        {
            logWarning($"Warning: PDF left unflattened because it is outside PDFFlatten's supported slice. {exception.Message}");
            File.Copy(inputPath, outputPath, overwrite: true);
        }
        catch (InvalidOperationException exception)
        {
            logWarning($"Warning: PDF left unflattened because PDFFlatten rejected the file as malformed or incomplete. {exception.Message}");
            File.Copy(inputPath, outputPath, overwrite: true);
        }
    }

    private static string GetProducerCorpusFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProducerCorpus", fileName);

    private static byte[] CreateMissingAppearanceReferencePdf()
    {
        return BuildPdf(new Dictionary<int, string>
        {
            [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R]>>>>",
            [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
            [4] = "<< /Length 3 >>\nstream\nq Q\nendstream",
            [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (Field01) /V (Value 01) /AP << /N 99 0 R >> >>"
        });
    }

    private static byte[] BuildPdf(IDictionary<int, string> bodies)
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
}
