namespace PDFFlatten.Internals;

/// <summary>
/// Represents a single entry in a classic PDF cross-reference table.
/// </summary>
internal sealed class XrefEntry
{
    internal XrefEntry(int objectNumber, int offset, int generation, bool inUse)
    {
        ObjectNumber = objectNumber;
        Offset = offset;
        Generation = generation;
        InUse = inUse;
    }

    internal int ObjectNumber { get; }
    internal int Offset { get; }
    internal int Generation { get; }
    internal bool InUse { get; }
}
