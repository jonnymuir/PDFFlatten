using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PDFFlatten;
using PDFFlatten.Internals;

namespace PDFFlatten.Tests;

public sealed partial class AppearanceResourceRegressionTests
{
    [Test]
    public void Flatten_keeps_fonts_resolvable_when_widget_appearances_depend_on_form_resources()
    {
        using var input = new MemoryStream(AcroFormResourceFixtureFactory.CreatePdf());
        using var flattened = PdfFlattener.Flatten(input);

        var document = PdfParser.Parse(ReadAllBytes(flattened));
        var catalog = document.GetRequiredDictionary(document.Trailer.RequireReference("Root"));
        var page = GetFirstPage(document, catalog);
        var pageResources = ResolveResourceDictionary(document, page);
        var pageFonts = ResolveFontDictionary(document, pageResources);

        var flattenedXObjects = ResolveNestedDictionary(document, pageResources, "XObject").Items
            .Where(entry => entry.Key.StartsWith("FldFlat", StringComparison.Ordinal))
            .Select(entry => (entry.Key, Reference: entry.Value as PdfIndirectReference))
            .ToArray();

        Assert.That(flattenedXObjects, Has.Length.EqualTo(1));

        var appearance = document.GetRequiredStream(flattenedXObjects[0].Reference!);
        var fontNames = ExtractFontNames(appearance.Data);

        Assert.That(fontNames, Is.EquivalentTo(new[] { "Helv" }));

        foreach (var fontName in fontNames)
        {
            Assert.That(
                HasResolvableFont(document, pageFonts, appearance, fontName),
                Is.True,
                $"Flattened appearance '{flattenedXObjects[0].Key}' uses /{fontName} but does not keep a usable font resource after removing /AcroForm.");
        }
    }

    private static bool HasResolvableFont(PdfDocument document, PdfDictionary? pageFonts, PdfStream appearance, string fontName)
    {
        var appearanceResources = ResolveDirectOrIndirectDictionary(document, GetValue(appearance.Dictionary, "Resources"));
        var appearanceFonts = ResolveFontDictionary(document, appearanceResources);

        return ContainsFont(appearanceFonts, fontName) || ContainsFont(pageFonts, fontName);
    }

    private static bool ContainsFont(PdfDictionary? fontDictionary, string fontName)
    {
        if (fontDictionary is null)
        {
            return false;
        }

        return fontDictionary.Items.TryGetValue(fontName, out var value) &&
               value is PdfIndirectReference or PdfDictionary;
    }

    private static PdfDictionary GetFirstPage(PdfDocument document, PdfDictionary catalog)
    {
        var pages = document.GetRequiredDictionary(catalog.RequireReference("Pages"));
        var firstPageRef = pages.RequireArray("Kids").Items.Single() as PdfIndirectReference;
        Assert.That(firstPageRef, Is.Not.Null);
        return document.GetRequiredDictionary(firstPageRef!);
    }

    private static PdfDictionary ResolveResourceDictionary(PdfDocument document, PdfDictionary owner)
    {
        var resources = ResolveDirectOrIndirectDictionary(document, GetValue(owner, "Resources"));
        Assert.That(resources, Is.Not.Null, "Expected /Resources to resolve to a dictionary.");
        return resources!;
    }

    private static PdfDictionary? ResolveFontDictionary(PdfDocument document, PdfDictionary? resources)
    {
        return ResolveDirectOrIndirectDictionary(document, GetValue(resources, "Font"));
    }

    private static PdfDictionary ResolveNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        var dictionary = ResolveDirectOrIndirectDictionary(document, GetValue(parent, key));
        Assert.That(dictionary, Is.Not.Null, $"Expected /{key} to resolve to a dictionary.");
        return dictionary!;
    }

    private static PdfDictionary? ResolveDirectOrIndirectDictionary(PdfDocument document, PdfValue? value)
    {
        return value switch
        {
            PdfDictionary direct => direct,
            PdfIndirectReference reference => TryGetDictionary(document, reference),
            _ => null
        };
    }

    private static PdfDictionary? TryGetDictionary(PdfDocument document, PdfIndirectReference reference)
    {
        try
        {
            return document.GetRequiredDictionary(reference);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static PdfValue? GetValue(PdfDictionary? dictionary, string key)
    {
        if (dictionary is null)
        {
            return null;
        }

        return dictionary.Items.TryGetValue(key, out var value) ? value : null;
    }

    private static string[] ExtractFontNames(byte[] streamData)
    {
        var text = Encoding.ASCII.GetString(streamData);
        return FontRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    [GeneratedRegex(@"/(?<name>[A-Za-z0-9]+)\s+[0-9.]+\s+Tf", RegexOptions.CultureInvariant)]
    private static partial Regex FontRegex();

    private static class AcroFormResourceFixtureFactory
    {
        public static byte[] CreatePdf()
        {
            return BuildPdf(new Dictionary<int, string>
            {
                [1] = "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R] /DR << /Font 12 0 R >> >> >>",
                [2] = "<</Type /Pages /Count 1 /Kids [3 0 R]>>",
                [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 10 0 R >> /XObject << >> >> /Annots [6 0 R] /Contents 4 0 R >>",
                [4] = Stream("BT /F1 12 Tf 20 150 Td (Base page) Tj ET"),
                [6] = "<< /Type /Annot /Subtype /Widget /Rect [20 20 120 44] /FT /Tx /T (GenericField) /V (Generic Value) /AP << /N 8 0 R >> /DA (/Helv 10 Tf 0 g) >>",
                [8] = Stream("q 0 0 100 24 re W n BT /Helv 10 Tf 0 g 2 8 Td (Generic Value) Tj ET Q", "<</Type /XObject /Subtype /Form /BBox [0 0 100 24]>>"),
                [10] = "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>",
                [12] = "<</Helv 10 0 R>>"
            });
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

        private static string Stream(string content, string dictionaryPrefix = "")
        {
            var data = Encoding.ASCII.GetBytes(content);
            return $"<<{dictionaryPrefix.Replace("<<", string.Empty).Replace(">>", string.Empty)} /Length {data.Length} >>\nstream\n{content}\nendstream";
        }
    }
}
