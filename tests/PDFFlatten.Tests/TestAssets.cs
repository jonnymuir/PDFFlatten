using NUnit.Framework;
using System.Text;
using System.Text.RegularExpressions;

namespace PDFFlatten.Tests;

internal static partial class TestAssets
{
    internal static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    internal static readonly string SampleProjectPath = Path.Combine(RepositoryRoot, "samples", "PDFFlatten.Sample", "PDFFlatten.Sample.vbproj");
    internal static readonly string SamplePdfPath = Path.Combine(AppContext.BaseDirectory, "BAPSL_P60_Populated.pdf");

    internal static readonly IReadOnlyDictionary<string, string> ExpectedFieldValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TaxYear"] = "2025-26",
            ["TitleInitialsLastName"] = "TEST PERSON",
            ["NationalInsuranceNumber"] = "AB123456C",
            ["MembershipNumber"] = "PR-00012345",
            ["PayeReference"] = "123/AB456",
            ["PreviousEmploymentIncome"] = "£2,345.67",
            ["PreviousEmploymentTax"] = "£234.56",
            ["BAPensionPay"] = "£12,345.67",
            ["BAPensionTax"] = "£1,234.56",
            ["TotalForYearPensionPay"] = "£14,691.34",
            ["TotalForYearPensionTax"] = "£1,469.12",
            ["FinalTaxCode"] = "1257L"
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
