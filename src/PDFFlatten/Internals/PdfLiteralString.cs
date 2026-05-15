using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a literal PDF string.
/// </summary>
internal sealed class PdfLiteralString : PdfValue
{
    internal PdfLiteralString(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal string Value { get; }
}
