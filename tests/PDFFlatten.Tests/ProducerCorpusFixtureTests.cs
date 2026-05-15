using System.Text;
using NUnit.Framework;
using PDFFlatten;

namespace PDFFlatten.Tests;

public sealed class ProducerCorpusFixtureTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedFieldValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = "Alice",
            ["City"] = "Hyrule"
        };

    private static readonly FixtureDescriptor[] CorpusFixtures =
    {
        new("reportlab-textfields-raw.pdf", "ReportLab PDF Library - \\(opensource\\)", false, "Unsupported PDF keyword '.1'."),
        new("reportlab-textfields-classic-xref.pdf", "ReportLab PDF Library - \\(opensource\\)", true, null),
        new("pdfrw-textfields-raw.pdf", "\\(pdfrw\\)", false, "Unsupported PDF keyword '.1'."),
        new("pdfrw-textfields-classic-xref.pdf", "\\(pdfrw\\)", true, null),
        new("pypdf-textfields-classic-xref.pdf", "pypdf", true, null)
    };

    private static IEnumerable<TestCaseData> AllCorpusFixtures() =>
        CorpusFixtures.Select(descriptor =>
            new TestCaseData(descriptor.FileName, descriptor.ProducerLiteral, descriptor.Supported, descriptor.RejectionMessageFragment)
                .SetName($"Producer_corpus_fixture_{descriptor.FileName}"));

    [TestCaseSource(nameof(AllCorpusFixtures))]
    public void Producer_corpus_fixture_contains_only_generic_acroform_content(
        string fileName,
        string producerLiteral,
        bool supported,
        string? rejectionMessageFragment)
    {
        var bytes = File.ReadAllBytes(GetFixturePath(fileName));
        var probe = TestAssets.PdfProbe.FromBytes(bytes);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(probe.StartsWithPdfHeader, Is.True);
            Assert.That(probe.HasAcroForm, Is.True);
            Assert.That(probe.WidgetCount, Is.EqualTo(ExpectedFieldValues.Count));
            Assert.That(text, Does.Contain($"/Producer ({producerLiteral})"));
            Assert.That(text, Does.Not.Contain("/Encrypt"));
            Assert.That(text, Does.Not.Contain("/XFA"));
            Assert.That(text, Does.Not.Contain("/EmbeddedFiles"));
            Assert.That(text, Does.Contain("/T (Name)"));
            Assert.That(text, Does.Contain("/V (Alice)"));
            Assert.That(text, Does.Contain("/T (City)"));
            Assert.That(text, Does.Contain("/V (Hyrule)"));
        });

        if (supported)
        {
            Assert.That(text, Does.Not.Contain(" Tf .1 .1 .1 rg"));
            Assert.That(text, Does.Not.Contain("[.1 .1 .1]"));
        }
        else
        {
            Assert.That(rejectionMessageFragment, Is.Not.Null);
            Assert.That(text.Contains(" Tf .1 .1 .1 rg", StringComparison.Ordinal) || text.Contains("[.1 .1 .1]", StringComparison.Ordinal), Is.True);
        }
    }

    [TestCaseSource(nameof(SupportedCorpusFixtures))]
    public void Flatten_succeeds_for_supported_producer_corpus_fixture(
        string fileName,
        string producerLiteral)
    {
        using var input = File.OpenRead(GetFixturePath(fileName));
        using var flattened = PdfFlattener.Flatten(input);
        using var copy = new MemoryStream();
        flattened.CopyTo(copy);

        var flattenedProbe = TestAssets.PdfProbe.FromBytes(copy.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(flattenedProbe.StartsWithPdfHeader, Is.True);
            Assert.That(flattenedProbe.HasAcroForm, Is.False);
            Assert.That(flattenedProbe.WidgetCount, Is.EqualTo(0));
            Assert.That(flattenedProbe.DrawOperationCount, Is.GreaterThanOrEqualTo(ExpectedFieldValues.Count), $"{producerLiteral} fixture should repaint each widget appearance onto page content.");
        });
    }

    [TestCaseSource(nameof(RejectedCorpusFixtures))]
    public void Flatten_rejects_out_of_slice_producer_corpus_fixture(
        string fileName,
        string producerLiteral,
        string rejectionMessageFragment)
    {
        using var input = File.OpenRead(GetFixturePath(fileName));

        Assert.That(
            () => PdfFlattener.Flatten(input),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains(rejectionMessageFragment),
            $"{producerLiteral} fixture should stay fail-closed until the parser explicitly broadens its supported numeric grammar.");
    }

    private static IEnumerable<TestCaseData> SupportedCorpusFixtures()
    {
        return CorpusFixtures
            .Where(descriptor => descriptor.Supported)
            .Select(descriptor => new TestCaseData(descriptor.FileName, descriptor.ProducerLiteral).SetName($"Flatten_succeeds_for_{descriptor.FileName}"));
    }

    private static IEnumerable<TestCaseData> RejectedCorpusFixtures()
    {
        return CorpusFixtures
            .Where(descriptor => !descriptor.Supported)
            .Select(descriptor => new TestCaseData(descriptor.FileName, descriptor.ProducerLiteral, descriptor.RejectionMessageFragment!).SetName($"Flatten_rejects_{descriptor.FileName}"));
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProducerCorpus", fileName);

    private sealed record FixtureDescriptor(
        string FileName,
        string ProducerLiteral,
        bool Supported,
        string? RejectionMessageFragment);
}
