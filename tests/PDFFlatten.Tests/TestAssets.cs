using NUnit.Framework;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFFlatten.Tests;

internal static partial class TestAssets
{
    internal static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    internal static readonly string SampleProjectPath = Path.GetFullPath(
        Path.Combine(RepositoryRoot, "samples", "PDFFlatten.Sample", "PDFFlatten.Sample.csproj"));
    internal static readonly string SamplePdfPath = Path.Combine(AppContext.BaseDirectory, "GenericAcroFormFixture.pdf");

    internal static readonly IReadOnlyDictionary<string, string> ExpectedFieldValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Field01"] = "Value 01",
            ["Field02"] = "Value 02",
            ["Field03"] = "Value 03",
            ["Field04"] = "Value 04",
            ["Field05"] = "Value 05",
            ["Field06"] = "Value 06",
            ["Field07"] = "Value 07",
            ["Field08"] = "Value 08",
            ["Field09"] = "Value 09",
            ["Field10"] = "Value 10",
            ["Field11"] = "Value 11",
            ["Field12"] = "Value 12"
        };

    internal static byte[] LoadSamplePdfBytes() => File.ReadAllBytes(SamplePdfPath);

    internal static PdfProbe LoadSamplePdfProbe() => PdfProbe.FromBytes(LoadSamplePdfBytes());

    [GeneratedRegex(@"/T\((?<name>(?:\\.|[^\\)])*)\).*?/V\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex FieldRegex();

    internal sealed partial class PdfProbe
    {
        private readonly string _text;

        private PdfProbe(string text)
        {
            _text = text;
        }

        internal bool StartsWithPdfHeader => _text.StartsWith("%PDF-", StringComparison.Ordinal);
        internal bool HasAcroForm => _text.Contains("/AcroForm", StringComparison.Ordinal);
        internal int WidgetCount => WidgetRegex().Matches(_text).Count;
        internal int DrawOperationCount => DrawOperationRegex().Matches(_text).Count;

        internal IReadOnlyDictionary<string, string> ExtractFieldValues()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in FieldRegex().Matches(_text))
            {
                var name = DecodePdfLiteral(match.Groups["name"].Value);
                var value = DecodePdfLiteral(match.Groups["value"].Value);
                values[name] = value;
            }

            return values;
        }

        internal static PdfProbe FromBytes(byte[] pdfBytes) => new(Encoding.Latin1.GetString(pdfBytes));

        [GeneratedRegex(@"/Subtype\s*/Widget", RegexOptions.CultureInvariant)]
        private static partial Regex WidgetRegex();

        [GeneratedRegex(@"/[A-Za-z0-9]+\s+Do", RegexOptions.CultureInvariant)]
        private static partial Regex DrawOperationRegex();

        private static string DecodePdfLiteral(string value)
        {
            var builder = new StringBuilder(value.Length);

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];

                if (current != '\\' || index == value.Length - 1)
                {
                    builder.Append(current);
                    continue;
                }

                index++;
                current = value[index];

                switch (current)
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case '(':
                    case ')':
                    case '\\':
                        builder.Append(current);
                        break;
                    default:
                        if (current >= '0' && current <= '7')
                        {
                            var octal = new string(new[] { current });

                            while (index + 1 < value.Length &&
                                   octal.Length < 3 &&
                                   value[index + 1] >= '0' &&
                                   value[index + 1] <= '7')
                            {
                                index++;
                                octal += value[index];
                            }

                            builder.Append((char)Convert.ToInt32(octal, 8));
                        }
                        else
                        {
                            builder.Append(current);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
