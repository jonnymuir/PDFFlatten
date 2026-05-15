namespace PDFFlatten.Internals;

/// <summary>
/// Represents an indirect object reference.
/// </summary>
internal sealed class PdfIndirectReference : PdfValue
{
    internal PdfIndirectReference(int objectNumber, int generation)
    {
        ObjectNumber = objectNumber;
        Generation = generation;
    }

    internal int ObjectNumber { get; }
    internal int Generation { get; }
}
