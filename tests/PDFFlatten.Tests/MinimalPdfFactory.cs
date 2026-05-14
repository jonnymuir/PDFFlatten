using System.Text;

namespace PDFFlatten.Tests;

internal static class MinimalPdfFactory
{
    internal static byte[] CreateWithoutForms()
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);

        void Write(string text)
        {
            writer.Write(text);
            writer.Flush();
        }

        var offsets = new Dictionary<int, int>();

        Write("%PDF-1.4\n");

        void WriteObject(int number, string body)
        {
            offsets[number] = (int)stream.Position;
            Write($"{number} 0 obj\n{body}\nendobj\n");
        }

        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        WriteObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>");

        const string content = "BT /F1 12 Tf 72 120 Td (Hello) Tj ET";
        WriteObject(4, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");

        var xrefOffset = (int)stream.Position;
        Write("xref\n0 5\n");
        Write("0000000000 65535 f \n");

        for (var objectNumber = 1; objectNumber <= 4; objectNumber++)
        {
            Write($"{offsets[objectNumber]:D10} 00000 n \n");
        }

        Write("trailer\n<< /Size 5 /Root 1 0 R >>\n");
        Write($"startxref\n{xrefOffset}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }
}
