using System.Globalization;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF numeric literal while preserving its original token text.
/// </summary>
internal sealed class PdfNumber : PdfValue
{
    internal PdfNumber(string rawValue)
    {
        RawValue = rawValue;
        NumericValue = double.Parse(rawValue, CultureInfo.InvariantCulture);
        IsInteger = rawValue.IndexOf('.') == -1 && rawValue.IndexOf('E') == -1 && rawValue.IndexOf('e') == -1;
    }

    internal PdfNumber(int value)
    {
        RawValue = value.ToString(CultureInfo.InvariantCulture);
        NumericValue = value;
        IsInteger = true;
    }

    internal PdfNumber(double value)
    {
        RawValue = value.ToString("0.###", CultureInfo.InvariantCulture);
        NumericValue = value;
        IsInteger = false;
    }

    internal string RawValue { get; }
    internal double NumericValue { get; }
    internal bool IsInteger { get; }
}
