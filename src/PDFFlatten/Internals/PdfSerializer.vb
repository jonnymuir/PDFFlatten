Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace Internals
    Friend NotInheritable Class PdfSerializer
        Private Sub New()
        End Sub

        Public Shared Function Serialize(document As PdfDocument) As Byte()
            If document Is Nothing Then Throw New ArgumentNullException(NameOf(document))

            Using output As New MemoryStream()
                output.Write(document.Preamble, 0, document.Preamble.Length)
                If document.Preamble.Length = 0 OrElse Not EndsWithLineBreak(document.Preamble) Then
                    WriteAscii(output, Environment.NewLine)
                End If

                Dim serializableObjects As List(Of PdfIndirectObject) = CollectReachableObjects(document)
                Dim offsets As New Dictionary(Of Integer, Long)()
                For Each pdfObject As PdfIndirectObject In serializableObjects.OrderBy(Function(item) item.Number)
                    offsets(pdfObject.Number) = output.Position
                    WriteAscii(output, pdfObject.Number.ToString(CultureInfo.InvariantCulture))
                    WriteAscii(output, " ")
                    WriteAscii(output, pdfObject.Generation.ToString(CultureInfo.InvariantCulture))
                    WriteAscii(output, " obj" & Environment.NewLine)
                    WriteValue(output, pdfObject.Value)
                    WriteAscii(output, Environment.NewLine & "endobj" & Environment.NewLine)
                Next

                Dim xrefPosition As Long = output.Position
                Dim size As Integer = If(serializableObjects.Count = 0, 1, serializableObjects.Max(Function(item) item.Number) + 1)
                WriteAscii(output, "xref" & Environment.NewLine)
                WriteAscii(output, $"0 {size}" & Environment.NewLine)
                WriteAscii(output, "0000000000 65535 f " & Environment.NewLine)

                For objectNumber As Integer = 1 To size - 1
                    Dim offset As Long = 0
                    If offsets.TryGetValue(objectNumber, offset) Then
                        WriteAscii(output, offset.ToString("D10", CultureInfo.InvariantCulture))
                        WriteAscii(output, " 00000 n " & Environment.NewLine)
                    Else
                        WriteAscii(output, "0000000000 00000 f " & Environment.NewLine)
                    End If
                Next

                Dim trailer As PdfDictionary = document.Trailer
                trailer("Size") = New PdfNumber(size)
                WriteAscii(output, "trailer" & Environment.NewLine)
                WriteValue(output, trailer)
                WriteAscii(output, Environment.NewLine & "startxref" & Environment.NewLine)
                WriteAscii(output, xrefPosition.ToString(CultureInfo.InvariantCulture))
                WriteAscii(output, Environment.NewLine & "%%EOF")
                Return output.ToArray()
            End Using
        End Function


        Private Shared Function CollectReachableObjects(document As PdfDocument) As List(Of PdfIndirectObject)
            Dim reachable As New HashSet(Of Integer)()
            For Each entry As KeyValuePair(Of String, PdfValue) In document.Trailer.Items
                If String.Equals(entry.Key, "Size", StringComparison.Ordinal) Then
                    Continue For
                End If

                MarkReachable(document, entry.Value, reachable)
            Next

            Return document.Objects.Where(Function(item) reachable.Contains(item.Number)).ToList()
        End Function

        Private Shared Sub MarkReachable(document As PdfDocument, value As PdfValue, reachable As HashSet(Of Integer))
            If value Is Nothing Then
                Return
            End If

            Dim reference As PdfIndirectReference = TryCast(value, PdfIndirectReference)
            If reference IsNot Nothing Then
                If reachable.Add(reference.ObjectNumber) Then
                    Dim target As PdfIndirectObject = Nothing
                    Try
                        target = document.GetRequiredObject(reference)
                    Catch ex As InvalidOperationException
                        Return
                    End Try

                    MarkReachable(document, target.Value, reachable)
                End If

                Return
            End If

            Dim array As PdfArray = TryCast(value, PdfArray)
            If array IsNot Nothing Then
                For Each item As PdfValue In array.Items
                    MarkReachable(document, item, reachable)
                Next

                Return
            End If

            Dim stream As PdfStream = TryCast(value, PdfStream)
            If stream IsNot Nothing Then
                MarkReachable(document, stream.Dictionary, reachable)
                Return
            End If

            Dim dictionary As PdfDictionary = TryCast(value, PdfDictionary)
            If dictionary IsNot Nothing Then
                For Each entry As KeyValuePair(Of String, PdfValue) In dictionary.Items
                    MarkReachable(document, entry.Value, reachable)
                Next
            End If
        End Sub

        Private Shared Sub WriteValue(output As Stream, value As PdfValue)
            Select Case True
                Case TypeOf value Is PdfNull
                    WriteAscii(output, "null")
                Case TypeOf value Is PdfBoolean
                    WriteAscii(output, If(DirectCast(value, PdfBoolean).Value, "true", "false"))
                Case TypeOf value Is PdfNumber
                    WriteAscii(output, DirectCast(value, PdfNumber).RawValue)
                Case TypeOf value Is PdfName
                    WriteAscii(output, "/" & DirectCast(value, PdfName).Value)
                Case TypeOf value Is PdfLiteralString
                    WriteAscii(output, "(" & EscapeLiteralString(DirectCast(value, PdfLiteralString).Value) & ")")
                Case TypeOf value Is PdfHexString
                    WriteAscii(output, "<" & DirectCast(value, PdfHexString).Value & ">")
                Case TypeOf value Is PdfIndirectReference
                    Dim reference As PdfIndirectReference = DirectCast(value, PdfIndirectReference)
                    WriteAscii(output, $"{reference.ObjectNumber} {reference.Generation} R")
                Case TypeOf value Is PdfArray
                    Dim array As PdfArray = DirectCast(value, PdfArray)
                    WriteAscii(output, "[")
                    For index As Integer = 0 To array.Items.Count - 1
                        If index > 0 Then
                            WriteAscii(output, " ")
                        End If

                        WriteValue(output, array.Items(index))
                    Next

                    WriteAscii(output, "]")
                Case TypeOf value Is PdfStream
                    WriteStream(output, DirectCast(value, PdfStream))
                Case TypeOf value Is PdfDictionary
                    Dim dictionary As PdfDictionary = DirectCast(value, PdfDictionary)
                    WriteAscii(output, "<<")
                    For Each entry As KeyValuePair(Of String, PdfValue) In dictionary.Items
                        WriteAscii(output, "/" & entry.Key & " ")
                        WriteValue(output, entry.Value)
                    Next

                    WriteAscii(output, ">>")
                Case Else
                    Throw New NotSupportedException($"Cannot serialize PDF value type {value.GetType().Name}.")
            End Select
        End Sub

        Private Shared Sub WriteStream(output As Stream, stream As PdfStream)
            WriteAscii(output, "<<")
            For Each entry As KeyValuePair(Of String, PdfValue) In stream.Dictionary.Items
                If String.Equals(entry.Key, "Length", StringComparison.Ordinal) Then
                    Continue For
                End If

                WriteAscii(output, "/" & entry.Key & " ")
                WriteValue(output, entry.Value)
            Next

            WriteAscii(output, "/Length " & stream.Data.Length.ToString(CultureInfo.InvariantCulture) & ">>" & Environment.NewLine)
            WriteAscii(output, "stream" & Environment.NewLine)
            output.Write(stream.Data, 0, stream.Data.Length)
            WriteAscii(output, Environment.NewLine & "endstream")
        End Sub

        Private Shared Function EscapeLiteralString(value As String) As String
            Return value.Replace("\", "\\").Replace("(", "\(").Replace(")", "\)").Replace(vbCr, "\r").Replace(vbLf, "\n")
        End Function

        Private Shared Function EndsWithLineBreak(bytes As Byte()) As Boolean
            Dim last As Byte = bytes(bytes.Length - 1)
            Return last = 10 OrElse last = 13
        End Function

        Private Shared Sub WriteAscii(output As Stream, value As String)
            Dim bytes As Byte() = Encoding.ASCII.GetBytes(value)
            output.Write(bytes, 0, bytes.Length)
        End Sub
    End Class
End Namespace
