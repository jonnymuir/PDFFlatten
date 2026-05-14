using NUnit.Framework;
using PDFFlatten;

namespace PDFFlatten.Tests;

public class FlattenOutputTests
{
    [Test]
    public void Flatten_removes_the_sample_forms_acroform_and_widget_annotations()
    {
        var originalBytes = TestAssets.LoadSamplePdfBytes();

        using var input = new MemoryStream(originalBytes, writable: false);
        using var output = PdfFlattener.Flatten(input);

        var flattenedBytes = ReadAllBytes(output);
        var flattened = TestAssets.PdfProbe.FromBytes(flattenedBytes);

        Assert.Multiple(() =>
        {
            Assert.That(flattenedBytes.SequenceEqual(originalBytes), Is.False, "Flatten should materially change the sample PDF.");
            Assert.That(flattened.HasAcroForm, Is.False, "Flattened PDFs should not retain an AcroForm catalog entry.");
            Assert.That(flattened.WidgetCount, Is.EqualTo(0), "Flattened PDFs should not retain interactive widget annotations.");
            Assert.That(flattened.DrawOperationCount, Is.GreaterThanOrEqualTo(TestAssets.ExpectedFieldValues.Count), "Flattened sample output should draw the appearance XObjects back onto the page.");
        });
    }

    [Test]
    public void Flatten_leaves_a_plain_pdf_unchanged()
    {
        var originalBytes = MinimalPdfFactory.CreateWithoutForms();

        using var input = new MemoryStream(originalBytes, writable: false);
        using var output = PdfFlattener.Flatten(input);

        var flattenedBytes = ReadAllBytes(output);

        Assert.That(flattenedBytes, Is.EqualTo(originalBytes));
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
}
