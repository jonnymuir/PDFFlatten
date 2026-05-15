using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a hexadecimal PDF string.
/// </summary>
internal sealed class PdfHexString : PdfValue
{
    internal PdfHexString(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal string Value { get; }
}
