using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Describes how an annotation appearance stream should be replayed onto a page.
/// </summary>
internal sealed class FlattenPlacement
{
    internal FlattenPlacement(
        string resourceName,
        PdfIndirectReference appearanceReference,
        double scaleX,
        double scaleY,
        double translateX,
        double translateY)
    {
        ResourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        AppearanceReference = appearanceReference ?? throw new ArgumentNullException(nameof(appearanceReference));
        ScaleX = scaleX;
        ScaleY = scaleY;
        TranslateX = translateX;
        TranslateY = translateY;
    }

    internal string ResourceName { get; }
    internal PdfIndirectReference AppearanceReference { get; }
    internal double ScaleX { get; }
    internal double ScaleY { get; }
    internal double TranslateX { get; }
    internal double TranslateY { get; }
}
