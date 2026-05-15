using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF stream object and its decoded dictionary metadata.
/// </summary>
internal sealed class PdfStream : PdfValue
{
    internal PdfStream(PdfDictionary dictionary, byte[] data)
    {
        Dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    internal PdfDictionary Dictionary { get; }
    internal byte[] Data { get; }
}
