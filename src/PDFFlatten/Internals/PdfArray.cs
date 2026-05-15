using System;
using System.Collections.Generic;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF array.
/// </summary>
internal sealed class PdfArray : PdfValue
{
    internal PdfArray(IEnumerable<PdfValue> items)
    {
        Items = new List<PdfValue>(items ?? throw new ArgumentNullException(nameof(items)));
    }

    internal List<PdfValue> Items { get; }
}
