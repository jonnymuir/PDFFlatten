Imports System
Imports System.IO
Imports PDFFlatten

Module Program
    Private Const Usage As String = "Usage: dotnet run --project samples/PDFFlatten.Sample -- <input-file> <output-file>"

    Function Main(args As String()) As Integer
        If args.Length <> 2 Then
            Console.Error.WriteLine("PDFFlatten sample console")
            Console.Error.WriteLine(Usage)
            Return 1
        End If

        Dim inputPath As String = Path.GetFullPath(args(0))
        Dim outputPath As String = Path.GetFullPath(args(1))

        If String.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase) Then
            Console.Error.WriteLine("Input and output paths must be different.")
            Return 1
        End If

        If Not File.Exists(inputPath) Then
            Console.Error.WriteLine($"Input file not found: {inputPath}")
            Return 1
        End If

        Dim outputDirectory As String = Path.GetDirectoryName(outputPath)
        If Not String.IsNullOrEmpty(outputDirectory) Then
            Directory.CreateDirectory(outputDirectory)
        End If

        Try
            Using input As Stream = File.OpenRead(inputPath)
                Using output As Stream = File.Create(outputPath)
                    PdfFlattener.Flatten(input, output)
                End Using
            End Using
        Catch ex As Exception
            Console.Error.WriteLine($"Flattening failed: {ex.Message}")
            Return 1
        End Try

        Console.WriteLine($"Flattened '{inputPath}' to '{outputPath}'.")
        Return 0
    End Function
End Module
