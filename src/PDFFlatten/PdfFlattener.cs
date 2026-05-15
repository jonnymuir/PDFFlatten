using PDFFlatten.Internals;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFFlatten;

/// <summary>
/// Flattens AcroForm widget appearances into ordinary page content streams.
/// </summary>
public static class PdfFlattener
{
    private static readonly Regex FontOperatorRegex = new(
        @"/(?<font>[^\s/]+)\s+[-+]?(?:\d+(?:\.\d+)?|\.\d+)\s+Tf",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns a flattened copy of the PDF in <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The source PDF stream.</param>
    /// <returns>A rewindable stream positioned at the beginning of the flattened PDF.</returns>
    /// <remarks>Caller-owned streams remain open.</remarks>
    public static Stream Flatten(Stream input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        var output = new MemoryStream();
        Flatten(input, output);
        output.Position = 0;
        return output;
    }

    /// <summary>
    /// Writes a flattened copy of the PDF from <paramref name="input"/> into <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The source PDF stream.</param>
    /// <param name="output">The destination stream that receives the flattened PDF.</param>
    /// <remarks>Caller-owned streams remain open. The output stream is left at the end of the written PDF.</remarks>
    public static void Flatten(Stream input, Stream output)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (!input.CanRead)
        {
            throw new ArgumentException("Input stream must be readable.", nameof(input));
        }

        if (!output.CanWrite)
        {
            throw new ArgumentException("Output stream must be writable.", nameof(output));
        }

        var originalBytes = ReadAllBytes(input);
        var document = PdfParser.Parse(originalBytes);
        var flattenedBytes = FlattenDocument(document);
        output.Write(flattenedBytes, 0, flattenedBytes.Length);
    }

    private static byte[] FlattenDocument(PdfDocument document)
    {
        var catalogReference = document.Trailer.RequireReference("Root");
        var catalog = document.GetRequiredDictionary(catalogReference);
        var pagesReference = catalog.RequireReference("Pages");
        var widgetsFlattened = 0;
        var placementIndex = 0;

        foreach (var pageReference in EnumeratePages(document, pagesReference))
        {
            widgetsFlattened += FlattenPage(document, pageReference, ref placementIndex);
        }

        if (widgetsFlattened == 0)
        {
            return (byte[])document.OriginalBytes.Clone();
        }

        catalog.Remove("AcroForm");
        return PdfSerializer.Serialize(document);
    }

    private static IEnumerable<PdfIndirectReference> EnumeratePages(PdfDocument document, PdfIndirectReference pagesReference)
    {
        var node = document.GetRequiredDictionary(pagesReference);
        var nodeType = node.GetNameValue("Type");

        if (string.Equals(nodeType, "Page", StringComparison.Ordinal))
        {
            yield return pagesReference;
            yield break;
        }

        if (!string.Equals(nodeType, "Pages", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Unsupported page tree node type.");
        }

        var kids = node.RequireArray("Kids");
        foreach (var kidValue in kids.Items)
        {
            if (kidValue is not PdfIndirectReference kidReference)
            {
                throw new NotSupportedException("Page tree kids must be indirect references.");
            }

            foreach (var pageReference in EnumeratePages(document, kidReference))
            {
                yield return pageReference;
            }
        }
    }

    private static int FlattenPage(PdfDocument document, PdfIndirectReference pageReference, ref int placementIndex)
    {
        var page = document.GetRequiredDictionary(pageReference);
        if (!page.TryGetValue("Annots", out var annotsValue))
        {
            return 0;
        }

        if (annotsValue is not PdfArray annots)
        {
            throw new NotSupportedException("Only direct annotation arrays are supported.");
        }

        var remainingAnnotations = new List<PdfValue>();
        var placements = new List<FlattenPlacement>();

        foreach (var annotationValue in annots.Items)
        {
            if (annotationValue is not PdfIndirectReference annotationReference)
            {
                remainingAnnotations.Add(annotationValue);
                continue;
            }

            var annotation = document.GetRequiredDictionary(annotationReference);
            var subtype = annotation.GetNameValue("Subtype");
            if (!string.Equals(subtype, "Widget", StringComparison.Ordinal))
            {
                remainingAnnotations.Add(annotationValue);
                continue;
            }

            placements.Add(CreatePlacement(document, annotation, placementIndex));
            placementIndex += 1;
        }

        if (placements.Count == 0)
        {
            return 0;
        }

        var resources = ResolveResourceDictionary(document, page);
        var xObjectDictionary = ResolveNestedDictionary(document, resources, "XObject");
        var contentBuilder = new StringBuilder();

        foreach (var placement in placements)
        {
            xObjectDictionary[placement.ResourceName] = placement.AppearanceReference;
            contentBuilder.Append("q ");
            contentBuilder.Append(FormatNumber(placement.ScaleX));
            contentBuilder.Append(" 0 0 ");
            contentBuilder.Append(FormatNumber(placement.ScaleY));
            contentBuilder.Append(' ');
            contentBuilder.Append(FormatNumber(placement.TranslateX));
            contentBuilder.Append(' ');
            contentBuilder.Append(FormatNumber(placement.TranslateY));
            contentBuilder.Append(" cm /");
            contentBuilder.Append(placement.ResourceName);
            contentBuilder.AppendLine(" Do Q");
        }

        var appendedStream = new PdfStream(new PdfDictionary(), Encoding.ASCII.GetBytes(contentBuilder.ToString()));
        var appendedReference = document.AddObject(appendedStream);
        page["Contents"] = AppendContentReference(page["Contents"], appendedReference);

        if (remainingAnnotations.Count == 0)
        {
            page.Remove("Annots");
        }
        else
        {
            page["Annots"] = new PdfArray(remainingAnnotations);
        }

        return placements.Count;
    }

    private static FlattenPlacement CreatePlacement(PdfDocument document, PdfDictionary annotation, int placementIndex)
    {
        var rect = annotation.RequireArray("Rect");
        if (rect.Items.Count != 4)
        {
            throw new NotSupportedException("Widget rectangles must contain four numbers.");
        }

        var appearanceDictionary = annotation.RequireDictionary("AP");
        var normalAppearanceReference = appearanceDictionary.RequireReference("N");
        var normalAppearance = document.GetRequiredStream(normalAppearanceReference);
        RepairTextAppearanceResources(document, annotation, normalAppearance);

        var box = normalAppearance.Dictionary.RequireArray("BBox");
        if (box.Items.Count != 4)
        {
            throw new NotSupportedException("Appearance BBoxes must contain four numbers.");
        }

        var left = PdfValueConversions.RequireNumber(rect.Items[0]);
        var bottom = PdfValueConversions.RequireNumber(rect.Items[1]);
        var right = PdfValueConversions.RequireNumber(rect.Items[2]);
        var top = PdfValueConversions.RequireNumber(rect.Items[3]);
        var boxLeft = PdfValueConversions.RequireNumber(box.Items[0]);
        var boxBottom = PdfValueConversions.RequireNumber(box.Items[1]);
        var boxRight = PdfValueConversions.RequireNumber(box.Items[2]);
        var boxTop = PdfValueConversions.RequireNumber(box.Items[3]);

        var boxWidth = boxRight - boxLeft;
        var boxHeight = boxTop - boxBottom;
        if (boxWidth <= 0 || boxHeight <= 0)
        {
            throw new NotSupportedException("Appearance BBoxes must be positive.");
        }

        var rectWidth = right - left;
        var rectHeight = top - bottom;
        var scaleX = rectWidth / boxWidth;
        var scaleY = rectHeight / boxHeight;

        return new FlattenPlacement(
            $"FldFlat{placementIndex + 1:000}",
            normalAppearanceReference,
            scaleX,
            scaleY,
            left - (boxLeft * scaleX),
            bottom - (boxBottom * scaleY));
    }

    private static void RepairTextAppearanceResources(PdfDocument document, PdfDictionary annotation, PdfStream appearance)
    {
        if (!IsTextField(annotation))
        {
            return;
        }

        var referencedFonts = GetReferencedFontNames(appearance);
        if (referencedFonts.Count == 0)
        {
            return;
        }

        var resources = ResolveOrCreateNestedDictionary(document, appearance.Dictionary, "Resources");
        var fonts = ResolveOrCreateNestedDictionary(document, resources, "Font");

        foreach (var fontName in referencedFonts)
        {
            if (HasResolvableFont(document, fonts, fontName))
            {
                continue;
            }

            if (TryResolveWidgetDefaultFont(document, annotation, out var repairedFont) && repairedFont is not null)
            {
                fonts[fontName] = repairedFont;
                continue;
            }

            var standardFont = CreateStandardFont(fontName);
            if (standardFont is not null)
            {
                fonts[fontName] = document.AddObject(standardFont);
            }
        }
    }

    private static bool IsTextField(PdfDictionary annotation)
    {
        if (!annotation.TryGetValue("FT", out var fieldType))
        {
            return false;
        }

        return fieldType is PdfName fieldTypeName && string.Equals(fieldTypeName.Value, "Tx", StringComparison.Ordinal);
    }

    private static List<string> GetReferencedFontNames(PdfStream appearance)
    {
        var decodedData = DecodeStreamData(appearance);
        if (decodedData is null || decodedData.Length == 0)
        {
            return new List<string>();
        }

        var content = Encoding.ASCII.GetString(decodedData);
        var fontNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in FontOperatorRegex.Matches(content))
        {
            var fontName = match.Groups["font"].Value;
            if (fontName.Length > 0 && seen.Add(fontName))
            {
                fontNames.Add(fontName);
            }
        }

        return fontNames;
    }

    private static byte[]? DecodeStreamData(PdfStream appearance)
    {
        if (!appearance.Dictionary.TryGetValue("Filter", out var filterValue))
        {
            return appearance.Data;
        }

        if (filterValue is not PdfName filterName || !string.Equals(filterName.Value, "FlateDecode", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return InflateData(appearance.Data);
        }
        catch (InvalidDataException)
        {
            if (appearance.Data.Length <= 6)
            {
                return null;
            }

            var rawDeflate = new byte[appearance.Data.Length - 6];
            Array.Copy(appearance.Data, 2, rawDeflate, 0, rawDeflate.Length);

            try
            {
                return InflateData(rawDeflate);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }

    private static byte[] InflateData(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var inflater = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }

    private static bool HasResolvableFont(PdfDocument document, PdfDictionary fontDictionary, string fontName)
    {
        if (!fontDictionary.TryGetValue(fontName, out var fontValue))
        {
            return false;
        }

        if (fontValue is PdfDictionary)
        {
            return true;
        }

        if (fontValue is not PdfIndirectReference fontReference)
        {
            return false;
        }

        try
        {
            document.GetRequiredDictionary(fontReference);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryResolveWidgetDefaultFont(PdfDocument document, PdfDictionary annotation, out PdfValue? fontValue)
    {
        fontValue = null;
        if (!TryGetLiteralString(annotation, "DA", out var defaultAppearance))
        {
            return false;
        }

        var match = FontOperatorRegex.Match(defaultAppearance);
        if (!match.Success)
        {
            return false;
        }

        var widgetResources = TryResolveDictionary(document, annotation, "DR");
        if (widgetResources is null)
        {
            return false;
        }

        var widgetFonts = TryResolveNestedDictionary(document, widgetResources, "Font");
        if (widgetFonts is null)
        {
            return false;
        }

        return widgetFonts.TryGetValue(match.Groups["font"].Value, out fontValue) && fontValue is not null;
    }

    private static PdfDictionary? TryResolveDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        if (!parent.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is PdfIndirectReference reference)
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

        return value as PdfDictionary;
    }

    private static PdfDictionary ResolveOrCreateNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        var existing = TryResolveDictionary(document, parent, key);
        if (existing is not null)
        {
            return existing;
        }

        var created = new PdfDictionary();
        parent[key] = created;
        return created;
    }

    private static PdfDictionary? TryResolveNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        return TryResolveDictionary(document, parent, key);
    }

    private static bool TryGetLiteralString(PdfDictionary dictionary, string key, out string value)
    {
        value = string.Empty;
        if (!dictionary.TryGetValue(key, out var rawValue) || rawValue is not PdfLiteralString literal)
        {
            return false;
        }

        value = literal.Value;
        return true;
    }

    private static PdfDictionary? CreateStandardFont(string fontName)
    {
        string? baseFontName = fontName switch
        {
            "Helv" => "Helvetica",
            "HeBo" => "Helvetica-Bold",
            "HeOb" => "Helvetica-Oblique",
            "HeBO" => "Helvetica-BoldOblique",
            "Cour" => "Courier",
            "CoBo" => "Courier-Bold",
            "CoOb" => "Courier-Oblique",
            "CoBO" => "Courier-BoldOblique",
            "TiRo" => "Times-Roman",
            "TiBo" => "Times-Bold",
            "TiIt" => "Times-Italic",
            "TiBI" => "Times-BoldItalic",
            "ZaDb" => "ZapfDingbats",
            _ => null
        };

        if (baseFontName is null)
        {
            return null;
        }

        var font = new PdfDictionary
        {
            ["Type"] = new PdfName("Font"),
            ["Subtype"] = new PdfName("Type1"),
            ["BaseFont"] = new PdfName(baseFontName)
        };

        if (!string.Equals(baseFontName, "ZapfDingbats", StringComparison.Ordinal))
        {
            font["Encoding"] = new PdfName("WinAnsiEncoding");
        }

        return font;
    }

    private static PdfDictionary ResolveResourceDictionary(PdfDocument document, PdfDictionary page)
    {
        if (!page.TryGetValue("Resources", out var resourcesValue))
        {
            var resources = new PdfDictionary();
            page["Resources"] = resources;
            return resources;
        }

        if (resourcesValue is PdfIndirectReference resourcesReference)
        {
            return document.GetRequiredDictionary(resourcesReference);
        }

        if (resourcesValue is not PdfDictionary resourcesDictionary)
        {
            throw new NotSupportedException("Page resources must be a dictionary or indirect reference.");
        }

        return resourcesDictionary;
    }

    private static PdfDictionary ResolveNestedDictionary(PdfDocument document, PdfDictionary parent, string key)
    {
        if (!parent.TryGetValue(key, out var value))
        {
            var created = new PdfDictionary();
            parent[key] = created;
            return created;
        }

        if (value is PdfIndirectReference reference)
        {
            return document.GetRequiredDictionary(reference);
        }

        if (value is not PdfDictionary dictionary)
        {
            throw new NotSupportedException($"/{key} must resolve to a dictionary.");
        }

        return dictionary;
    }

    private static PdfValue AppendContentReference(PdfValue existingContents, PdfIndirectReference appendedReference)
    {
        if (existingContents is PdfArray arrayContents)
        {
            var updatedItems = new List<PdfValue>(arrayContents.Items)
            {
                appendedReference
            };

            return new PdfArray(updatedItems);
        }

        return new PdfArray(new PdfValue[] { existingContents, appendedReference });
    }

    private static byte[] ReadAllBytes(Stream input)
    {
        if (input is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
