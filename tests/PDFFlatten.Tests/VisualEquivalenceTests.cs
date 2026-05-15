using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using PDFFlatten;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace PDFFlatten.Tests;

public sealed class VisualEquivalenceTests
{
    private const int ThumbnailSize = 2048;
    private const double TransformSensitivePixelRatioTolerance = 0.01d;

    [Test]
    [Category("VisualRegression")]
    public void Flattened_generic_fixture_matches_the_original_render()
    {
        var result = RenderAndCompare(
            "generic-fixture",
            TestAssets.LoadSamplePdfBytes());

        Assert.That(result.DifferentPixelCount, Is.EqualTo(0), result.ToFailureMessage());
        CleanupArtifacts(result);
    }

    [Test]
    [Category("VisualRegression")]
    public void Flattened_transform_sensitive_fixture_matches_the_original_render()
    {
        var result = RenderAndCompare(
            "transform-sensitive-fixture",
            VisualFixtureFactory.CreateTransformSensitiveTextFieldPdf());

        Assert.That(
            result.DifferentPixelRatio,
            Is.LessThanOrEqualTo(TransformSensitivePixelRatioTolerance),
            result.ToFailureMessage($"Allowed tolerance: {TransformSensitivePixelRatioTolerance:P2}."));
        CleanupArtifacts(result);
    }

    private static VisualComparisonResult RenderAndCompare(string fixtureSlug, byte[] originalPdfBytes)
    {
        EnsureQuickLookAvailability();

        var outputDirectory = Path.Combine(
            TestAssets.RepositoryRoot,
            "artifacts",
            "test-output",
            "visual-equivalence",
            fixtureSlug,
            TestContext.CurrentContext.Test.ID);

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        var originalPdfPath = Path.Combine(outputDirectory, "original.pdf");
        var flattenedPdfPath = Path.Combine(outputDirectory, "flattened.pdf");
        var originalImagePath = Path.Combine(outputDirectory, "original.png");
        var flattenedImagePath = Path.Combine(outputDirectory, "flattened.png");
        var diffImagePath = Path.Combine(outputDirectory, "diff.png");

        File.WriteAllBytes(originalPdfPath, originalPdfBytes);

        using (var input = new MemoryStream(originalPdfBytes, writable: false))
        using (var flattened = PdfFlattener.Flatten(input))
        {
            File.WriteAllBytes(flattenedPdfPath, ReadAllBytes(flattened));
        }

        QuickLookRenderer.RenderFirstPage(originalPdfPath, originalImagePath);
        QuickLookRenderer.RenderFirstPage(flattenedPdfPath, flattenedImagePath);

        return VisualComparator.ComparePngs(originalImagePath, flattenedImagePath, diffImagePath);
    }

    private static void CleanupArtifacts(VisualComparisonResult result)
    {
        if (Directory.Exists(result.OutputDirectory))
        {
            Directory.Delete(result.OutputDirectory, recursive: true);
        }
    }

    private static void EnsureQuickLookAvailability()
    {
        if (!OperatingSystem.IsMacOS() || !QuickLookRenderer.IsAvailable)
        {
            Assert.Ignore("Visual equivalence checks require macOS Quick Look (qlmanage) and are skipped on runners where it is unavailable.");
        }
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

    private static class QuickLookRenderer
    {
        private const string QuickLookPath = "/usr/bin/qlmanage";

        internal static bool IsAvailable => File.Exists(QuickLookPath);

        internal static void RenderFirstPage(string pdfPath, string pngPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);

            var outputDirectory = Path.GetDirectoryName(pngPath)!;
            var generatedPath = Path.Combine(outputDirectory, Path.GetFileName(pdfPath) + ".png");

            if (File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
            }

            if (File.Exists(pngPath))
            {
                File.Delete(pngPath);
            }

            var startInfo = new ProcessStartInfo(QuickLookPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add(ThumbnailSize.ToString());
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputDirectory);
            startInfo.ArgumentList.Add(pdfPath);

            using var process = Process.Start(startInfo) ?? throw new AssertionException("Failed to start qlmanage.");

            if (!process.WaitForExit(120000))
            {
                process.Kill(entireProcessTree: true);
                throw new AssertionException($"Timed out rendering '{pdfPath}' with qlmanage.");
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                throw new AssertionException(
                    $"qlmanage failed while rendering '{pdfPath}'. Exit code: {process.ExitCode}.{Environment.NewLine}{standardOutput}{standardError}");
            }

            if (!File.Exists(generatedPath))
            {
                throw new AssertionException($"qlmanage did not emit the expected PNG for '{pdfPath}'.");
            }

            File.Move(generatedPath, pngPath);
        }
    }

    private static class VisualComparator
    {
        internal static VisualComparisonResult ComparePngs(string originalImagePath, string flattenedImagePath, string diffImagePath)
        {
            using var original = Image.Load<Rgba32>(originalImagePath);
            using var flattened = Image.Load<Rgba32>(flattenedImagePath);

            if (original.Width != flattened.Width || original.Height != flattened.Height)
            {
                throw new AssertionException(
                    $"Rendered PNG dimensions differ. Original: {original.Width}x{original.Height}; flattened: {flattened.Width}x{flattened.Height}.");
            }

            using var diff = new Image<Rgba32>(original.Width, original.Height);
            long differentPixelCount = 0;

            original.ProcessPixelRows(flattened, diff, (originalAccessor, flattenedAccessor, diffAccessor) =>
            {
                for (var y = 0; y < original.Height; y++)
                {
                    var originalRow = originalAccessor.GetRowSpan(y);
                    var flattenedRow = flattenedAccessor.GetRowSpan(y);
                    var diffRow = diffAccessor.GetRowSpan(y);

                    for (var x = 0; x < original.Width; x++)
                    {
                        if (originalRow[x].Equals(flattenedRow[x]))
                        {
                            diffRow[x] = new Rgba32(0, 0, 0, 0);
                            continue;
                        }

                        differentPixelCount++;
                        diffRow[x] = new Rgba32(
                            (byte)Math.Abs(originalRow[x].R - flattenedRow[x].R),
                            (byte)Math.Abs(originalRow[x].G - flattenedRow[x].G),
                            (byte)Math.Abs(originalRow[x].B - flattenedRow[x].B),
                            255);
                    }
                }
            });

            if (differentPixelCount > 0)
            {
                diff.Save(diffImagePath, new PngEncoder());
            }
            else if (File.Exists(diffImagePath))
            {
                File.Delete(diffImagePath);
            }

            return new VisualComparisonResult(
                differentPixelCount,
                (long)original.Width * original.Height,
                original.Width,
                original.Height,
                Path.GetDirectoryName(originalImagePath)!,
                originalImagePath,
                flattenedImagePath,
                diffImagePath);
        }
    }

    private sealed record VisualComparisonResult(
        long DifferentPixelCount,
        long TotalPixelCount,
        int Width,
        int Height,
        string OutputDirectory,
        string OriginalImagePath,
        string FlattenedImagePath,
        string DiffImagePath)
    {
        internal double DifferentPixelRatio => TotalPixelCount == 0 ? 0d : (double)DifferentPixelCount / TotalPixelCount;

        internal string ToFailureMessage()
            => ToFailureMessage(string.Empty);

        internal string ToFailureMessage(string suffix)
        {
            var diffArtifact = File.Exists(DiffImagePath)
                ? $" Diff image: {DiffImagePath}"
                : string.Empty;

            var extra = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $" {suffix}";

            return $"Rendered output changed in {DifferentPixelCount} of {TotalPixelCount} pixels ({DifferentPixelRatio:P4}). Size: {Width}x{Height}. Original: {OriginalImagePath}. Flattened: {FlattenedImagePath}.{diffArtifact}{extra}";
        }
    }

    private static class VisualFixtureFactory
    {
        internal static byte[] CreateTransformSensitiveTextFieldPdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<</Type /Catalog /Pages 2 0 R /AcroForm <</Fields [6 0 R 7 0 R]>>>>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<</Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources 5 0 R /Annots [6 0 R 7 0 R] /Contents 4 0 R>>",
                [4] = Stream("q Q"),
                [5] = "<</Font <</F1 10 0 R>> /XObject <<>>>>",
                [6] = "<</Type /Annot /Subtype /Widget /Rect [16 32 136 62] /FT /Tx /T (Left) /V (Left) /AP <</N 8 0 R>>>>",
                [7] = "<</Type /Annot /Subtype /Widget /Rect [90 110 270 160] /FT /Tx /T (Scaled) /V (Scaled) /AP <</N 9 0 R>>>>",
                [8] = Stream(
                    "BT /F1 12 Tf 4 4 Td (Left) Tj ET",
                    "/Type /XObject /Subtype /Form /BBox [0 0 100 20] /Resources <</Font <</F1 10 0 R>>>>"),
                [9] = Stream(
                    "BT /F1 12 Tf -6 3 Td (Scaled) Tj ET",
                    "/Type /XObject /Subtype /Form /BBox [-10 -5 90 15] /Resources <</Font <</F1 10 0 R>>>>"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>"
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
    }
}
