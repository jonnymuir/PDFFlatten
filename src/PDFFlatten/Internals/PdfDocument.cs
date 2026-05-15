using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a parsed PDF file together with the objects needed for rewrite operations.
/// </summary>
internal sealed class PdfDocument
{
    private readonly Dictionary<int, PdfIndirectObject> _objects;
    private int _nextObjectNumber;

    internal PdfDocument(byte[] originalBytes, byte[] preamble, PdfDictionary trailer, IEnumerable<PdfIndirectObject> objects)
    {
        OriginalBytes = originalBytes ?? throw new ArgumentNullException(nameof(originalBytes));
        Preamble = preamble ?? throw new ArgumentNullException(nameof(preamble));
        Trailer = trailer ?? throw new ArgumentNullException(nameof(trailer));

        if (objects is null)
        {
            throw new ArgumentNullException(nameof(objects));
        }

        _objects = objects.ToDictionary(item => item.Number);
        _nextObjectNumber = _objects.Count == 0 ? 1 : _objects.Keys.Max() + 1;
    }

    internal byte[] OriginalBytes { get; }
    internal byte[] Preamble { get; }
    internal PdfDictionary Trailer { get; set; }

    internal IReadOnlyCollection<PdfIndirectObject> Objects =>
        new ReadOnlyCollection<PdfIndirectObject>(_objects.Values.OrderBy(item => item.Number).ToList());

    internal int MaxObjectNumber()
    {
        return _objects.Count == 0 ? 0 : _objects.Keys.Max();
    }

    internal PdfIndirectObject GetRequiredObject(PdfIndirectReference reference)
    {
        if (!_objects.TryGetValue(reference.ObjectNumber, out var result))
        {
            throw new InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R was not found.");
        }

        return result;
    }

    internal PdfDictionary GetRequiredDictionary(PdfIndirectReference reference)
    {
        if (GetRequiredObject(reference).Value is not PdfDictionary dictionary)
        {
            throw new InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R is not a dictionary.");
        }

        return dictionary;
    }

    internal PdfStream GetRequiredStream(PdfIndirectReference reference)
    {
        if (GetRequiredObject(reference).Value is not PdfStream stream)
        {
            throw new InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R is not a stream.");
        }

        return stream;
    }

    internal PdfIndirectReference AddObject(PdfValue value)
    {
        var objectNumber = _nextObjectNumber;
        _nextObjectNumber += 1;
        _objects[objectNumber] = new PdfIndirectObject(objectNumber, 0, value);
        return new PdfIndirectReference(objectNumber, 0);
    }
}
