using System;

namespace PDFFlatten.Internals;

/// <summary>
/// Provides strict conversions between parsed PDF values and CLR primitives.
/// </summary>
internal static class PdfValueConversions
{
    internal static double RequireNumber(PdfValue value)
    {
        if (value is not PdfNumber number)
        {
            throw new InvalidOperationException("Expected a PDF number.");
        }

        return number.NumericValue;
    }
}
