using System;
using System.IO;

namespace PDFFlatten.Sample;

internal static class Program
{
    private const string Usage = "Usage: dotnet run --project samples/PDFFlatten.Sample -- <input-file> <output-file>";

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("PDFFlatten sample console");
            Console.Error.WriteLine(Usage);
            return 1;
        }

        var inputPath = Path.GetFullPath(args[0]);
        var outputPath = Path.GetFullPath(args[1]);

        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Input and output paths must be different.");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Input file not found: {inputPath}");
            return 1;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        try
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            PdfFlattener.Flatten(input, output);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Flattening failed: {exception.Message}");
            return 1;
        }

        Console.WriteLine($"Flattened '{inputPath}' to '{outputPath}'.");
        return 0;
    }
}
