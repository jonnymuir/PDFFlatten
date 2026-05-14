using NUnit.Framework;

namespace PDFFlatten.Tests;

public class SamplePdfCharacterizationTests
{
    [Test]
    public void Sample_pdf_fixture_contains_the_expected_form_fields_and_values()
    {
        var sample = TestAssets.LoadSamplePdfProbe();
        var fieldValues = sample.ExtractFieldValues();

        Assert.Multiple(() =>
        {
            Assert.That(sample.StartsWithPdfHeader, Is.True);
            Assert.That(sample.HasAcroForm, Is.True);
            Assert.That(sample.WidgetCount, Is.EqualTo(TestAssets.ExpectedFieldValues.Count));
            Assert.That(fieldValues.Keys, Is.EquivalentTo(TestAssets.ExpectedFieldValues.Keys));
        });

        foreach (var expectedField in TestAssets.ExpectedFieldValues)
        {
            Assert.That(fieldValues[expectedField.Key], Is.EqualTo(expectedField.Value), $"Unexpected value for sample field '{expectedField.Key}'.");
        }
    }
}
