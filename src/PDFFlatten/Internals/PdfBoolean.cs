namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF boolean literal.
/// </summary>
internal sealed class PdfBoolean : PdfValue
{
    internal PdfBoolean(bool value)
    {
        Value = value;
    }

    internal bool Value { get; }
}
