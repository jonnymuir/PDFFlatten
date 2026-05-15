using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents an indirect object entry within a PDF file.
/// </summary>
internal sealed class PdfIndirectObject
{
    internal PdfIndirectObject(int number, int generation, PdfValue value)
    {
        Number = number;
        Generation = generation;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal int Number { get; }
    internal int Generation { get; }
    internal PdfValue Value { get; set; }
}
