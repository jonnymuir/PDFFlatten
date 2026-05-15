using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF name object.
/// </summary>
internal sealed class PdfName : PdfValue
{
    internal PdfName(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal string Value { get; }
}
