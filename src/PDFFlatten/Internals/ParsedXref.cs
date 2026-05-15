using System.Collections.Generic;

namespace PDFFlatten.Internals;

/// <summary>
/// Holds the parsed cross-reference table and trailer dictionary for a PDF file.
/// </summary>
internal sealed class ParsedXref
{
    internal ParsedXref(IDictionary<int, XrefEntry> entries, PdfDictionary trailer)
    {
        Entries = entries;
        Trailer = trailer;
    }

    internal IDictionary<int, XrefEntry> Entries { get; }
    internal PdfDictionary Trailer { get; }
}
