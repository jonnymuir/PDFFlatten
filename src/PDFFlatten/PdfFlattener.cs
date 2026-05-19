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
    private const int SupportedNeedAppearancesTextFieldFlags = 1 | 2 | 4 | 4_194_304 | 8_388_608;
    private static readonly string[] UnsupportedInheritedFieldAttributeKeys = { "FT", "DA", "DR", "V" };
    private static readonly Regex FontOperatorRegex = new(
        @"/(?<font>[^\s/]+)\s+(?<size>[-+]?(?:\d+(?:\.\d+)?|\.\d+))\s+Tf",
        RegexOptions.CultureInvariant);
    private static readonly Regex FontSelectionRegex = new(
        @"/(?<font>[^\s/]+)\s+(?<size>[-+]?(?:\d+(?:\.\d+)?|\.\d+))(?:\s|$)",
        RegexOptions.CultureInvariant);
    private static readonly Regex RgbColorOperatorRegex = new(
        @"(?<r>[-+]?(?:\d+(?:\.\d+)?|\.\d+))\s+(?<g>[-+]?(?:\d+(?:\.\d+)?|\.\d+))\s+(?<b>[-+]?(?:\d+(?:\.\d+)?|\.\d+))\s+rg",
        RegexOptions.CultureInvariant);
    private static readonly Regex GrayColorOperatorRegex = new(
        @"(?<gray>[-+]?(?:\d+(?:\.\d+)?|\.\d+))\s+g",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns a flattened copy of the PDF in <paramref name="input"/>.
    /// </summary>
    /// <param name="input">A readable stream containing the source PDF.</param>
    /// <returns>A rewindable stream positioned at the beginning of the flattened PDF.</returns>
    /// <remarks>
    /// <para>PDFFlatten is production-ready only for a constrained slice of AcroForm PDFs: classic xref-table files, no incremental-update trailer chain, no XFA, no digital signature fields, either no encryption or Standard-security RC4-128 encryption (<c>/V 2</c>, <c>/R 3</c>) that opens with an empty user password and no crypt filters, stream <c>/Length</c> values that are either direct integers or a single indirect reference to an integer object, direct page <c>/Annots</c>, non-inherited page <c>/Resources</c>, self-contained widget/terminal-field dictionaries for operative field attributes (<c>/FT</c>, <c>/DA</c>, <c>/DR</c>, <c>/V</c>), widget normal appearances that resolve to a single indirect stream at <c>/AP /N</c> without rotation or <c>/Matrix</c> transforms, or a much narrower <c>/NeedAppearances</c>-only text-field path that synthesizes a left-aligned single-line appearance from widget-local <c>/DA</c> + <c>/DR</c> + string <c>/V</c>, and bounded file/stream sizes that fit the in-memory implementation. Supported encrypted inputs are rewritten as unencrypted output PDFs.</para>
    /// <para>Inputs outside that slice fail closed with descriptive exceptions instead of a best-effort rewrite. Unencrypted PDFs with no AcroForm/widgets are returned unchanged; supported encrypted inputs are rewritten as unencrypted output even when no widgets flatten.</para>
    /// <para>Caller-owned streams remain open.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using System;
    /// using System.IO;
    /// using PDFFlatten;
    ///
    /// using Stream input = File.OpenRead("classic-acroform.pdf");
    ///
    /// try
    /// {
    ///     using Stream flattened = PdfFlattener.Flatten(input);
    ///     using Stream output = File.Create("flattened.pdf");
    ///     flattened.CopyTo(output);
    /// }
    /// catch (NotSupportedException)
    /// {
    ///     // The PDF is outside PDFFlatten's supported slice.
    /// }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> is not readable.</exception>
    /// <exception cref="NotSupportedException">The PDF uses known-unsupported structures such as xref streams, object streams, incremental updates, encryption outside the narrow empty-password RC4-128 Standard-security slice, XFA, digital signature fields, inherited page resources, inherited operative field attributes, indirect page annotations, widgets without usable <c>/AP /N</c> appearances or without the narrow widget-local inputs required for the limited <c>/NeedAppearances</c> text fallback, unsupported transforms, or file/stream payloads that exceed the library's explicit in-memory safety caps.</exception>
    /// <exception cref="InvalidOperationException">The PDF is malformed or incomplete for the supported classic-parser slice.</exception>
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
    /// <param name="input">A readable stream containing the source PDF.</param>
    /// <param name="output">A writable stream that receives the flattened PDF.</param>
    /// <remarks>
    /// <para>The same supported-slice preconditions as <see cref="Flatten(Stream)"/> apply, including bounded file/stream sizes that fit the in-memory implementation.</para>
    /// <para>PDFFlatten rejects known-unsupported structures with descriptive exceptions before writing output bytes, rather than emitting a best-effort partial rewrite.</para>
    /// <para>Caller-owned streams remain open. The output stream is left at the end of the written PDF.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> is not readable or <paramref name="output"/> is not writable.</exception>
    /// <exception cref="NotSupportedException">The PDF uses known-unsupported structures such as xref streams, object streams, incremental updates, encryption outside the narrow empty-password RC4-128 Standard-security slice, XFA, digital signature fields, inherited page resources, inherited operative field attributes, indirect page annotations, widgets without usable <c>/AP /N</c> appearances or without the narrow widget-local inputs required for the limited <c>/NeedAppearances</c> text fallback, unresolved indirect references, unsupported transforms, or file/stream payloads that exceed the library's explicit in-memory safety caps.</exception>
    /// <exception cref="InvalidOperationException">The PDF is malformed or incomplete for the supported classic-parser slice.</exception>
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
        RejectUnsupportedCatalog(document, catalog);
        var allowNeedAppearancesTextGeneration = HasNeedAppearances(document, catalog);
        var pagesReference = catalog.RequireReference("Pages");
        var widgetsFlattened = 0;
        var placementIndex = 0;
        var flattenedWidgetObjectNumbers = new HashSet<int>();
        var flattenedWidgetStructParents = new HashSet<int>();

        foreach (var pageReference in EnumeratePages(document, pagesReference))
        {
            widgetsFlattened += FlattenPage(
                document,
                pageReference,
                ref placementIndex,
                allowNeedAppearancesTextGeneration,
                flattenedWidgetObjectNumbers,
                flattenedWidgetStructParents);
        }

        if (widgetsFlattened == 0)
        {
            return document.WasDecrypted
                ? PdfSerializer.Serialize(document)
                : (byte[])document.OriginalBytes.Clone();
        }

        PruneStructureTreeWidgetReferences(document, catalog, flattenedWidgetObjectNumbers, flattenedWidgetStructParents);
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

    private static int FlattenPage(
        PdfDocument document,
        PdfIndirectReference pageReference,
        ref int placementIndex,
        bool allowNeedAppearancesTextGeneration,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents)
    {
        var page = document.GetRequiredDictionary(pageReference);
        if (!page.TryGetValue("Annots", out var annotsValue))
        {
            return 0;
        }

        if (annotsValue is not PdfArray annots)
        {
            throw new NotSupportedException("Indirect page /Annots arrays are not supported.");
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

            placements.Add(CreatePlacement(document, annotation, placementIndex, allowNeedAppearancesTextGeneration));
            flattenedWidgetObjectNumbers.Add(annotationReference.ObjectNumber);
            if (TryResolveInteger(document, annotation, "StructParent", out var structParent))
            {
                flattenedWidgetStructParents.Add(structParent);
            }

            placementIndex += 1;
        }

        if (placements.Count == 0)
        {
            return 0;
        }

        RejectUnsupportedPageRotation(document, page);
        RejectInheritedPageResources(document, page);
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

    private static void PruneStructureTreeWidgetReferences(
        PdfDocument document,
        PdfDictionary catalog,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents)
    {
        if (flattenedWidgetObjectNumbers.Count == 0
            || !catalog.TryGetValue("StructTreeRoot", out var structureTreeRootValue)
            || structureTreeRootValue is null)
        {
            return;
        }

        PruneStructureTreeValue(
            document,
            structureTreeRootValue,
            flattenedWidgetObjectNumbers,
            flattenedWidgetStructParents,
            new HashSet<int>());
    }

    private static void PruneStructureTreeValue(
        PdfDocument document,
        PdfValue value,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents,
        ISet<int> visitedObjects)
    {
        switch (value)
        {
            case PdfIndirectReference reference:
                if (!visitedObjects.Add(reference.ObjectNumber))
                {
                    return;
                }

                PruneStructureTreeValue(
                    document,
                    document.GetRequiredObject(reference).Value,
                    flattenedWidgetObjectNumbers,
                    flattenedWidgetStructParents,
                    visitedObjects);
                return;
            case PdfDictionary dictionary:
                PruneStructureTreeDictionary(document, dictionary, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
                return;
            case PdfArray array:
                PruneStructureTreeArray(document, array, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
                return;
            case PdfStream stream:
                PruneStructureTreeDictionary(document, stream.Dictionary, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
                return;
        }
    }

    private static void PruneStructureTreeDictionary(
        PdfDocument document,
        PdfDictionary dictionary,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents,
        ISet<int> visitedObjects)
    {
        foreach (var key in dictionary.Items.Keys.ToArray())
        {
            var child = dictionary.Items[key];
            if (string.Equals(key, "Nums", StringComparison.Ordinal) && child is PdfArray numberTreeEntries)
            {
                PruneParentTreeNumberArray(document, numberTreeEntries, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
                continue;
            }

            if (IsWidgetObjectReference(document, child, flattenedWidgetObjectNumbers))
            {
                if (string.Equals(key, "K", StringComparison.Ordinal))
                {
                    dictionary[key] = new PdfArray(Array.Empty<PdfValue>());
                }
                else
                {
                    dictionary.Remove(key);
                }

                continue;
            }

            PruneStructureTreeValue(document, child, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
        }
    }

    private static void PruneStructureTreeArray(
        PdfDocument document,
        PdfArray array,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents,
        ISet<int> visitedObjects)
    {
        for (var index = array.Items.Count - 1; index >= 0; index--)
        {
            var item = array.Items[index];
            if (IsWidgetObjectReference(document, item, flattenedWidgetObjectNumbers))
            {
                array.Items.RemoveAt(index);
                continue;
            }

            PruneStructureTreeValue(document, item, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
        }
    }

    private static void PruneParentTreeNumberArray(
        PdfDocument document,
        PdfArray numberTreeEntries,
        ISet<int> flattenedWidgetObjectNumbers,
        ISet<int> flattenedWidgetStructParents,
        ISet<int> visitedObjects)
    {
        if (numberTreeEntries.Items.Count % 2 != 0)
        {
            PruneStructureTreeArray(document, numberTreeEntries, flattenedWidgetObjectNumbers, flattenedWidgetStructParents, visitedObjects);
            return;
        }

        for (var index = numberTreeEntries.Items.Count - 2; index >= 0; index -= 2)
        {
            if (numberTreeEntries.Items[index] is PdfNumber key
                && key.IsInteger
                && flattenedWidgetStructParents.Contains(PdfSecurityLimits.RequireInt32(key, "ParentTree /Nums key")))
            {
                numberTreeEntries.Items.RemoveAt(index + 1);
                numberTreeEntries.Items.RemoveAt(index);
                continue;
            }

            PruneStructureTreeValue(
                document,
                numberTreeEntries.Items[index + 1],
                flattenedWidgetObjectNumbers,
                flattenedWidgetStructParents,
                visitedObjects);
        }
    }

    private static bool IsWidgetObjectReference(PdfDocument document, PdfValue value, ISet<int> flattenedWidgetObjectNumbers)
    {
        var candidate = value switch
        {
            PdfDictionary dictionary => dictionary,
            PdfIndirectReference reference => document.GetRequiredObject(reference).Value as PdfDictionary,
            _ => null
        };

        if (candidate is null)
        {
            return false;
        }

        if (!candidate.TryGetValue("Type", out var typeValue)
            || typeValue is not PdfName typeName
            || !string.Equals(typeName.Value, "OBJR", StringComparison.Ordinal))
        {
            return false;
        }

        return candidate.TryGetValue("Obj", out var objectValue)
               && objectValue is PdfIndirectReference objectReference
               && flattenedWidgetObjectNumbers.Contains(objectReference.ObjectNumber);
    }

    private static FlattenPlacement CreatePlacement(PdfDocument document, PdfDictionary annotation, int placementIndex, bool allowNeedAppearancesTextGeneration)
    {
        RejectInheritedFieldAttributes(document, annotation);
        RejectDigitalSignatureFields(document, annotation);
        var rect = annotation.RequireArray("Rect");
        if (rect.Items.Count != 4)
        {
            throw new NotSupportedException("Widget rectangles must contain four numbers.");
        }

        if (!annotation.TryGetValue("AP", out var appearanceValue) || appearanceValue is null)
        {
            if (allowNeedAppearancesTextGeneration)
            {
                return CreateSimpleTextAppearancePlacement(document, annotation, rect, placementIndex);
            }

            throw new NotSupportedException("Widget annotations must define a normal appearance at /AP /N; NeedAppearances-only appearance generation is not supported.");
        }

        if (appearanceValue is not PdfDictionary appearanceDictionary)
        {
            throw new NotSupportedException("Widget annotations must define a normal appearance dictionary at /AP.");
        }

        if (!appearanceDictionary.TryGetValue("N", out var normalAppearanceValue) || normalAppearanceValue is null)
        {
            throw new NotSupportedException("Widget annotations must define a normal appearance at /AP /N.");
        }

        if (normalAppearanceValue is not PdfIndirectReference normalAppearanceReference)
        {
            throw new NotSupportedException("Only indirect stream /AP /N appearances are supported; state dictionaries are not supported.");
        }

        var normalAppearance = document.GetRequiredStream(normalAppearanceReference);
        RejectUnsupportedAppearanceMatrix(normalAppearance);
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

    private static FlattenPlacement CreateSimpleTextAppearancePlacement(PdfDocument document, PdfDictionary annotation, PdfArray rect, int placementIndex)
    {
        if (!IsTextField(annotation))
        {
            throw new NotSupportedException("NeedAppearances-only appearance generation is supported only for text widgets (/FT /Tx).");
        }

        if (annotation.TryGetValue("MaxLen", out _))
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation does not support widgets with /MaxLen.");
        }

        if (TryResolveInteger(document, annotation, "Q", out var alignment) && alignment != 0)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation supports left-aligned widgets only (/Q 0 or absent).");
        }

        if (TryResolveInteger(document, annotation, "Ff", out var flags)
            && (flags & ~SupportedNeedAppearancesTextFieldFlags) != 0)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation supports simple single-line text fields only.");
        }

        if (!annotation.TryGetValue("DR", out _))
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires widget-local /DR font resources.");
        }

        var left = PdfValueConversions.RequireNumber(rect.Items[0]);
        var bottom = PdfValueConversions.RequireNumber(rect.Items[1]);
        var right = PdfValueConversions.RequireNumber(rect.Items[2]);
        var top = PdfValueConversions.RequireNumber(rect.Items[3]);
        var width = right - left;
        var height = top - bottom;
        if (width <= 0 || height <= 0)
        {
            throw new NotSupportedException("Widget rectangles must be positive.");
        }

        var settings = GetSimpleTextAppearanceSettings(annotation);
        var fontValue = ResolveWidgetTextFontResource(document, annotation, settings.FontName);
        var appearanceReference = document.AddObject(CreateSimpleTextAppearanceStream(annotation, width, height, settings, fontValue));

        return new FlattenPlacement(
            $"FldFlat{placementIndex + 1:000}",
            appearanceReference,
            1,
            1,
            left,
            bottom);
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

    private static SimpleTextAppearanceSettings GetSimpleTextAppearanceSettings(PdfDictionary annotation)
    {
        if (!TryGetLiteralString(annotation, "DA", out var defaultAppearance))
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires a widget-local string /DA.");
        }

        var fontMatch = GetSimpleTextFontMatch(defaultAppearance);
        if (!fontMatch.Success)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires a widget-local /DA with a parseable font selection.");
        }

        var fontSize = double.Parse(fontMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
        if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires a positive /DA font size.");
        }

        return new SimpleTextAppearanceSettings(
            fontMatch.Groups["font"].Value,
            fontSize,
            BuildSimpleTextColorOperators(defaultAppearance));
    }

    private static string BuildSimpleTextColorOperators(string defaultAppearance)
    {
        var rgbMatch = RgbColorOperatorRegex.Match(defaultAppearance);
        if (rgbMatch.Success)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} rg",
                FormatColorOperand(rgbMatch.Groups["r"].Value),
                FormatColorOperand(rgbMatch.Groups["g"].Value),
                FormatColorOperand(rgbMatch.Groups["b"].Value));
        }

        var grayMatch = GrayColorOperatorRegex.Match(defaultAppearance);
        if (grayMatch.Success)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} g",
                FormatColorOperand(grayMatch.Groups["gray"].Value));
        }

        return "0 g";
    }

    private static Match GetSimpleTextFontMatch(string defaultAppearance)
    {
        var strictMatch = FontOperatorRegex.Match(defaultAppearance);
        if (strictMatch.Success)
        {
            return strictMatch;
        }

        return FontSelectionRegex.Match(defaultAppearance);
    }

    private static string FormatColorOperand(string value)
    {
        var parsed = double.Parse(value, CultureInfo.InvariantCulture);
        if (double.IsNaN(parsed) || double.IsInfinity(parsed) || parsed < 0 || parsed > 1)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires /DA gray or RGB color operands in the 0..1 range.");
        }

        return FormatNumber(parsed);
    }

    private static PdfValue ResolveWidgetTextFontResource(PdfDocument document, PdfDictionary annotation, string fontName)
    {
        var widgetResources = TryResolveDictionary(document, annotation, "DR");
        if (widgetResources is null)
        {
            throw new NotSupportedException("NeedAppearances-only text appearance generation requires widget-local /DR font resources.");
        }

        var widgetFonts = TryResolveNestedDictionary(document, widgetResources, "Font");
        if (widgetFonts is null
            || !widgetFonts.TryGetValue(fontName, out var fontValue)
            || fontValue is null)
        {
            throw new NotSupportedException($"NeedAppearances-only text appearance generation could not resolve /{fontName} from widget-local /DR /Font resources.");
        }

        if (fontValue is not PdfDictionary && fontValue is not PdfIndirectReference)
        {
            throw new NotSupportedException($"NeedAppearances-only text appearance generation requires /{fontName} to resolve to a font dictionary or indirect reference.");
        }

        return fontValue;
    }

    private static PdfStream CreateSimpleTextAppearanceStream(
        PdfDictionary annotation,
        double width,
        double height,
        SimpleTextAppearanceSettings settings,
        PdfValue fontValue)
    {
        var fonts = new PdfDictionary
        {
            [settings.FontName] = fontValue
        };
        var resources = new PdfDictionary
        {
            ["Font"] = fonts
        };
        var dictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Form"),
            ["BBox"] = new PdfArray(new PdfValue[]
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(width),
                new PdfNumber(height)
            }),
            ["Resources"] = resources
        };

        return new PdfStream(dictionary, BuildSimpleTextAppearanceContent(annotation, width, height, settings));
    }

    private static byte[] BuildSimpleTextAppearanceContent(PdfDictionary annotation, double width, double height, SimpleTextAppearanceSettings settings)
    {
        using var output = new MemoryStream();
        WriteAscii(output, "q 0 0 ");
        WriteAscii(output, FormatNumber(width));
        WriteAscii(output, " ");
        WriteAscii(output, FormatNumber(height));
        WriteAscii(output, " re W n BT ");
        WriteAscii(output, settings.ColorOperators);
        WriteAscii(output, " /");
        WriteAscii(output, settings.FontName);
        WriteAscii(output, " ");
        WriteAscii(output, FormatNumber(settings.FontSize));
        WriteAscii(output, " Tf ");
        WriteAscii(output, FormatNumber(Math.Min(2d, Math.Max(1d, width * 0.02d))));
        WriteAscii(output, " ");
        WriteAscii(output, FormatNumber(Math.Max(0d, ((height - settings.FontSize) / 2d) + (settings.FontSize * 0.2d))));
        WriteAscii(output, " Td ");
        WritePdfTextLiteral(output, GetTextWidgetValueBytes(annotation));
        WriteAscii(output, " Tj ET Q");
        return output.ToArray();
    }

    private static byte[] GetTextWidgetValueBytes(PdfDictionary annotation)
    {
        if (!annotation.TryGetValue("V", out var rawValue) || rawValue is null || rawValue is PdfNull)
        {
            return Array.Empty<byte>();
        }

        return PdfStringEncoding.GetBytes(rawValue, "text widget /V");
    }

    private static void WritePdfTextLiteral(Stream output, byte[] value)
    {
        output.WriteByte((byte)'(');
        foreach (var current in value)
        {
            switch (current)
            {
                case (byte)'\\':
                case (byte)'(':
                case (byte)')':
                    output.WriteByte((byte)'\\');
                    output.WriteByte(current);
                    break;
                case 13:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)'r');
                    break;
                case 10:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)'n');
                    break;
                default:
                    output.WriteByte(current);
                    break;
            }
        }

        output.WriteByte((byte)')');
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
        return ReadAllBytesWithLimit(
            inflater,
            PdfSecurityLimits.MaxDecodedAppearanceBytes,
            $"FlateDecode appearance content larger than {PdfSecurityLimits.MaxDecodedAppearanceBytes} bytes when inflated is not supported.");
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

    private static bool TryResolveInteger(PdfDocument document, PdfDictionary parent, string key, out int value)
    {
        value = 0;
        if (!parent.TryGetValue(key, out var rawValue))
        {
            return false;
        }

        var resolvedValue = rawValue;
        if (rawValue is PdfIndirectReference reference)
        {
            try
            {
                resolvedValue = document.GetRequiredObject(reference).Value;
            }
            catch (InvalidOperationException ex)
            {
                throw new NotSupportedException($"Dictionary key /{key} must resolve to an integer.", ex);
            }
        }

        if (resolvedValue is not PdfNumber number || !number.IsInteger)
        {
            throw new NotSupportedException($"Dictionary key /{key} must resolve to an integer.");
        }

        value = PdfSecurityLimits.RequireInt32(number, $"dictionary key /{key}");
        return true;
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
        if (!dictionary.TryGetValue(key, out var rawValue) || rawValue is null)
        {
            return false;
        }

        switch (rawValue)
        {
            case PdfLiteralString literal:
                value = literal.Value;
                return true;
            case PdfHexString hex:
                value = PdfStringEncoding.GetLiteralString(PdfStringEncoding.GetBytes(hex, $"dictionary key /{key}"));
                return true;
            default:
                return false;
        }
    }

    private static void RejectInheritedFieldAttributes(PdfDocument document, PdfDictionary annotation)
    {
        foreach (var key in UnsupportedInheritedFieldAttributeKeys)
        {
            if (annotation.TryGetValue(key, out _))
            {
                continue;
            }

            if (!TryResolveInheritedFieldValue(document, annotation, key, out _))
            {
                continue;
            }

            throw new NotSupportedException(
                $"Inherited field attribute /{key} is not supported for widget field '{GetFieldDisplayName(document, annotation)}'; operative field attributes must be self-contained on the terminal field/widget dictionary.");
        }
    }

    private static void RejectDigitalSignatureFields(PdfDocument document, PdfDictionary annotation)
    {
        if (!annotation.TryGetValue("FT", out var fieldType)
            || fieldType is not PdfName fieldTypeName
            || !string.Equals(fieldTypeName.Value, "Sig", StringComparison.Ordinal))
        {
            return;
        }

        throw new NotSupportedException(
            $"Digital signature fields (/FT /Sig) are not supported for widget field '{GetFieldDisplayName(document, annotation)}' because flattening would remove signature verification semantics.");
    }

    private static string GetFieldDisplayName(PdfDocument document, PdfDictionary annotation)
    {
        var names = new Stack<string>();
        var current = annotation;

        while (true)
        {
            if (TryGetPartialFieldName(current, out var partialName) && partialName.Length > 0)
            {
                names.Push(partialName);
            }

            if (TryResolveDictionary(document, current, "Parent") is not { } parent)
            {
                break;
            }

            current = parent;
        }

        return names.Count == 0 ? "<unnamed field>" : string.Join(".", names);
    }

    private static bool TryGetPartialFieldName(PdfDictionary dictionary, out string value)
    {
        value = string.Empty;
        if (!dictionary.TryGetValue("T", out var rawValue))
        {
            return false;
        }

        switch (rawValue)
        {
            case PdfLiteralString literal:
                value = literal.Value;
                return true;
            case PdfHexString hex:
                value = hex.Value;
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveInheritedFieldValue(PdfDocument document, PdfDictionary dictionary, string key, out PdfValue? value)
    {
        value = null;
        var current = dictionary;
        while (TryResolveDictionary(document, current, "Parent") is { } parent)
        {
            if (parent.TryGetValue(key, out value))
            {
                return true;
            }

            current = parent;
        }

        value = null;
        return false;
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

    private static void RejectUnsupportedCatalog(PdfDocument document, PdfDictionary catalog)
    {
        var acroForm = TryResolveDictionary(document, catalog, "AcroForm");
        if (acroForm is not null && acroForm.TryGetValue("XFA", out _))
        {
            throw new NotSupportedException("XFA forms are not supported.");
        }
    }

    private static bool HasNeedAppearances(PdfDocument document, PdfDictionary catalog)
    {
        var acroForm = TryResolveDictionary(document, catalog, "AcroForm");
        return acroForm is not null
               && acroForm.TryGetValue("NeedAppearances", out var value)
               && value is PdfBoolean boolean
               && boolean.Value;
    }

    private static void RejectUnsupportedPageRotation(PdfDocument document, PdfDictionary page)
    {
        var current = page;
        while (true)
        {
            if (TryResolveInteger(document, current, "Rotate", out var rotation))
            {
                var normalized = ((rotation % 360) + 360) % 360;
                if (normalized != 0)
                {
                    throw new NotSupportedException("Pages with non-zero /Rotate values are not supported.");
                }
            }

            if (TryResolveDictionary(document, current, "Parent") is not { } parent)
            {
                break;
            }

            current = parent;
        }
    }

    private static void RejectUnsupportedAppearanceMatrix(PdfStream appearance)
    {
        if (!appearance.Dictionary.TryGetValue("Matrix", out var matrixValue))
        {
            return;
        }

        if (matrixValue is not PdfArray matrix || matrix.Items.Count != 6)
        {
            throw new NotSupportedException("Appearance /Matrix transforms are not supported.");
        }

        var expectedIdentity = new[] { 1d, 0d, 0d, 1d, 0d, 0d };
        for (var index = 0; index < expectedIdentity.Length; index++)
        {
            if (Math.Abs(PdfValueConversions.RequireNumber(matrix.Items[index]) - expectedIdentity[index]) > 0.0001)
            {
                throw new NotSupportedException("Appearance /Matrix transforms are not supported.");
            }
        }
    }

    private static void RejectInheritedPageResources(PdfDocument document, PdfDictionary page)
    {
        if (page.TryGetValue("Resources", out _))
        {
            return;
        }

        var current = page;
        while (TryResolveDictionary(document, current, "Parent") is { } parent)
        {
            if (parent.TryGetValue("Resources", out _))
            {
                throw new NotSupportedException("Pages that inherit /Resources are not supported because flattening could shadow ancestor resources.");
            }

            current = parent;
        }
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
        if (input.CanSeek)
        {
            if (input.Length > PdfSecurityLimits.MaxInputBytes)
            {
                throw new NotSupportedException($"PDF inputs larger than {PdfSecurityLimits.MaxInputBytes} bytes are not supported.");
            }

            input.Position = 0;
        }

        return ReadAllBytesWithLimit(
            input,
            PdfSecurityLimits.MaxInputBytes,
            $"PDF inputs larger than {PdfSecurityLimits.MaxInputBytes} bytes are not supported.");
    }

    private static byte[] ReadAllBytesWithLimit(Stream input, int maxBytes, string exceptionMessage)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        while (true)
        {
            var remaining = (long)maxBytes - buffer.Length;
            if (remaining <= 0)
            {
                if (input.ReadByte() != -1)
                {
                    throw new NotSupportedException(exceptionMessage);
                }

                break;
            }

            var read = input.Read(chunk, 0, (int)Math.Min(chunk.Length, remaining));
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void WriteAscii(Stream output, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }

    private sealed class SimpleTextAppearanceSettings
    {
        internal SimpleTextAppearanceSettings(string fontName, double fontSize, string colorOperators)
        {
            FontName = fontName;
            FontSize = fontSize;
            ColorOperators = colorOperators;
        }

        internal string FontName { get; }
        internal double FontSize { get; }
        internal string ColorOperators { get; }
    }
}
