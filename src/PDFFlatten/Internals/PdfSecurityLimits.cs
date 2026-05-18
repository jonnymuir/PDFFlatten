using System;
using System.Globalization;

namespace PDFFlatten.Internals;

internal static class PdfSecurityLimits
{
    internal const int MaxInputBytes = 64 * 1024 * 1024;
    internal const int MaxStreamBytes = 16 * 1024 * 1024;
    internal const int MaxDecodedAppearanceBytes = 8 * 1024 * 1024;

    internal static int ParseInt32Token(string token, string context)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"Malformed PDF integer literal for {context}.");
        }

        return value;
    }

    internal static double ParseNumberToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
        {
            throw new InvalidOperationException($"Malformed PDF numeric literal '{token}'.");
        }

        return value;
    }

    internal static int RequireInt32(PdfNumber number, string context)
    {
        if (number is null)
        {
            throw new ArgumentNullException(nameof(number));
        }

        if (!number.IsInteger
            || number.NumericValue < int.MinValue
            || number.NumericValue > int.MaxValue
            || number.NumericValue != Math.Truncate(number.NumericValue))
        {
            throw new InvalidOperationException($"PDF integer value for {context} is outside the supported Int32 range.");
        }

        return (int)number.NumericValue;
    }
}
