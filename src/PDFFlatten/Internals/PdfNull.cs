namespace PDFFlatten.Internals;

/// <summary>
/// Represents the PDF <c>null</c> literal.
/// </summary>
internal sealed class PdfNull : PdfValue
{
    private PdfNull()
    {
    }

    internal static PdfNull Value { get; } = new();
}
