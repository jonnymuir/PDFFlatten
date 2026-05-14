Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text

Namespace Internals
    Friend NotInheritable Class PdfParser
        Private Sub New()
        End Sub

        Public Shared Function Parse(data As Byte()) As PdfDocument
            If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))
            If data.Length = 0 Then Throw New InvalidOperationException("The PDF stream is empty.")

            Dim startXref As Integer = FindStartXref(data)
            Dim parsedXref As ParsedXref = ParseXref(data, startXref)
            Dim firstObjectOffset As Integer = parsedXref.Entries.Values.Where(Function(entry) entry.InUse).Min(Function(entry) entry.Offset)
            Dim objects As New List(Of PdfIndirectObject)()

            For Each entry As XrefEntry In parsedXref.Entries.Values.Where(Function(item) item.InUse).OrderBy(Function(item) item.ObjectNumber)
                objects.Add(ParseObject(data, entry.Offset))
            Next

            Dim preamble(firstObjectOffset - 1) As Byte
            Array.Copy(data, 0, preamble, 0, firstObjectOffset)
            Return New PdfDocument(DirectCast(data.Clone(), Byte()), preamble, parsedXref.Trailer, objects)
        End Function

        Private Shared Function FindStartXref(data As Byte()) As Integer
            Dim marker As Byte() = Encoding.ASCII.GetBytes("startxref")
            For index As Integer = data.Length - marker.Length To 0 Step -1
                Dim matched As Boolean = True
                For markerIndex As Integer = 0 To marker.Length - 1
                    If data(index + markerIndex) <> marker(markerIndex) Then
                        matched = False
                        Exit For
                    End If
                Next

                If matched Then
                    Dim position As Integer = index + marker.Length
                    SkipWhiteSpaceAndComments(data, position)
                    Dim offsetToken As String = ReadSimpleToken(data, position)
                    Return Integer.Parse(offsetToken, CultureInfo.InvariantCulture)
                End If
            Next

            Throw New InvalidOperationException("Could not find the PDF cross-reference table.")
        End Function

        Private Shared Function ParseXref(data As Byte(), startXref As Integer) As ParsedXref
            Dim position As Integer = startXref
            Dim keyword As String = ReadSimpleToken(data, position)
            If Not String.Equals(keyword, "xref", StringComparison.Ordinal) Then
                Throw New NotSupportedException("Cross-reference streams are not supported in this version.")
            End If

            Dim entries As New Dictionary(Of Integer, XrefEntry)()

            While True
                SkipWhiteSpaceAndComments(data, position)
                Dim token As String = PeekSimpleToken(data, position)
                If String.Equals(token, "trailer", StringComparison.Ordinal) Then
                    ReadSimpleToken(data, position)
                    SkipWhiteSpaceAndComments(data, position)
                    Dim reader As New PdfReader(data, position)
                    Dim trailerDictionary As PdfDictionary = TryCast(reader.ReadValue(), PdfDictionary)
                    If trailerDictionary Is Nothing Then
                        Throw New InvalidOperationException("Trailer dictionary is missing.")
                    End If

                    Return New ParsedXref(entries, trailerDictionary)
                End If

                Dim firstObject As Integer = Integer.Parse(ReadSimpleToken(data, position), CultureInfo.InvariantCulture)
                SkipWhiteSpaceAndComments(data, position)
                Dim count As Integer = Integer.Parse(ReadSimpleToken(data, position), CultureInfo.InvariantCulture)
                ConsumeLineEnding(data, position)

                For offsetIndex As Integer = 0 To count - 1
                    Dim line As String = ReadLine(data, position)
                    If line.Length < 18 Then
                        Throw New InvalidOperationException("Malformed cross-reference entry.")
                    End If

                    Dim offset As Integer = Integer.Parse(line.Substring(0, 10), CultureInfo.InvariantCulture)
                    Dim generation As Integer = Integer.Parse(line.Substring(11, 5), CultureInfo.InvariantCulture)
                    Dim inUse As Boolean = line(17) = "n"c
                    entries(firstObject + offsetIndex) = New XrefEntry(firstObject + offsetIndex, offset, generation, inUse)
                Next
            End While

            Throw New InvalidOperationException("Cross-reference table ended unexpectedly.")
        End Function

        Private Shared Function ParseObject(data As Byte(), offset As Integer) As PdfIndirectObject
            Dim reader As New PdfReader(data, offset)
            Dim objectNumber As Integer = reader.ReadInteger()
            Dim generation As Integer = reader.ReadInteger()
            Dim keyword As String = reader.ReadKeyword()
            If Not String.Equals(keyword, "obj", StringComparison.Ordinal) Then
                Throw New InvalidOperationException("Indirect object header is malformed.")
            End If

            Dim value As PdfValue = reader.ReadValue()
            reader.SkipWhiteSpaceAndComments()

            Dim dictionary As PdfDictionary = TryCast(value, PdfDictionary)
            If dictionary IsNot Nothing AndAlso reader.PeekKeyword() = "stream" Then
                reader.ReadKeyword()
                reader.ConsumeStreamLineEnding()

                Dim lengthValue As PdfValue = Nothing
                If Not dictionary.TryGetValue("Length", lengthValue) Then
                    Throw New NotSupportedException("Streams without /Length are not supported.")
                End If

                Dim lengthNumber As PdfNumber = TryCast(lengthValue, PdfNumber)
                If lengthNumber Is Nothing OrElse Not lengthNumber.IsInteger Then
                    Throw New NotSupportedException("Only streams with direct integer /Length values are supported.")
                End If

                Dim streamLength As Integer = CInt(lengthNumber.NumericValue)
                Dim streamData As Byte() = reader.ReadBytes(streamLength)
                reader.SkipPotentialStreamTerminator()
                If reader.ReadKeyword() <> "endstream" Then
                    Throw New InvalidOperationException("Stream was not terminated correctly.")
                End If

                value = New PdfStream(dictionary, streamData)
            End If

            reader.SkipWhiteSpaceAndComments()
            If reader.ReadKeyword() <> "endobj" Then
                Throw New InvalidOperationException("Indirect object did not terminate with endobj.")
            End If

            Return New PdfIndirectObject(objectNumber, generation, value)
        End Function

        Private Shared Sub SkipWhiteSpaceAndComments(data As Byte(), ByRef position As Integer)
            While position < data.Length
                Dim current As Byte = data(position)
                If current = AscW("%"c) Then
                    While position < data.Length AndAlso data(position) <> 10 AndAlso data(position) <> 13
                        position += 1
                    End While
                ElseIf IsWhiteSpace(current) Then
                    position += 1
                Else
                    Exit While
                End If
            End While
        End Sub

        Private Shared Function ReadSimpleToken(data As Byte(), ByRef position As Integer) As String
            SkipWhiteSpaceAndComments(data, position)
            Dim start As Integer = position
            While position < data.Length AndAlso Not IsDelimiter(data(position))
                position += 1
            End While

            Return Encoding.ASCII.GetString(data, start, position - start)
        End Function

        Private Shared Function PeekSimpleToken(data As Byte(), position As Integer) As String
            Dim copy As Integer = position
            Return ReadSimpleToken(data, copy)
        End Function

        Private Shared Function ReadLine(data As Byte(), ByRef position As Integer) As String
            Dim start As Integer = position
            While position < data.Length AndAlso data(position) <> 10 AndAlso data(position) <> 13
                position += 1
            End While

            Dim line As String = Encoding.ASCII.GetString(data, start, position - start)
            ConsumeLineEnding(data, position)
            Return line
        End Function

        Private Shared Sub ConsumeLineEnding(data As Byte(), ByRef position As Integer)
            If position < data.Length AndAlso data(position) = 13 Then
                position += 1
                If position < data.Length AndAlso data(position) = 10 Then
                    position += 1
                End If
            ElseIf position < data.Length AndAlso data(position) = 10 Then
                position += 1
            End If
        End Sub

        Private Shared Function IsDelimiter(value As Byte) As Boolean
            Return IsWhiteSpace(value) OrElse value = AscW("("c) OrElse value = AscW(")"c) OrElse value = AscW("<"c) OrElse value = AscW(">"c) OrElse value = AscW("["c) OrElse value = AscW("]"c) OrElse value = AscW("{"c) OrElse value = AscW("}"c) OrElse value = AscW("/"c) OrElse value = AscW("%"c)
        End Function

        Private Shared Function IsWhiteSpace(value As Byte) As Boolean
            Return value = 0 OrElse value = 9 OrElse value = 10 OrElse value = 12 OrElse value = 13 OrElse value = 32
        End Function

        Private NotInheritable Class ParsedXref
            Public Sub New(entries As IDictionary(Of Integer, XrefEntry), trailer As PdfDictionary)
                Me.Entries = entries
                Me.Trailer = trailer
            End Sub

            Public ReadOnly Property Entries As IDictionary(Of Integer, XrefEntry)
            Public ReadOnly Property Trailer As PdfDictionary
        End Class

        Private NotInheritable Class XrefEntry
            Public Sub New(objectNumber As Integer, offset As Integer, generation As Integer, inUse As Boolean)
                Me.ObjectNumber = objectNumber
                Me.Offset = offset
                Me.Generation = generation
                Me.InUse = inUse
            End Sub

            Public ReadOnly Property ObjectNumber As Integer
            Public ReadOnly Property Offset As Integer
            Public ReadOnly Property Generation As Integer
            Public ReadOnly Property InUse As Boolean
        End Class

        Private NotInheritable Class PdfReader
            Private ReadOnly _data As Byte()
            Private _position As Integer

            Public Sub New(data As Byte(), position As Integer)
                _data = data
                _position = position
            End Sub

            Public Function ReadValue() As PdfValue
                SkipWhiteSpaceAndComments()
                If _position >= _data.Length Then
                    Throw New InvalidOperationException("Unexpected end of file while reading a PDF value.")
                End If

                Dim current As Byte = _data(_position)
                Select Case current
                    Case AscW("<"c)
                        If _position + 1 < _data.Length AndAlso _data(_position + 1) = AscW("<"c) Then
                            Return ReadDictionary()
                        End If

                        Return ReadHexString()
                    Case AscW("("c)
                        Return ReadLiteralString()
                    Case AscW("["c)
                        Return ReadArray()
                    Case AscW("/"c)
                        Return ReadName()
                    Case Else
                        If IsNumericToken(current) OrElse current = AscW("+"c) OrElse current = AscW("-"c) Then
                            Return ReadNumberOrReference()
                        End If

                        Dim keyword As String = ReadKeyword()
                        Select Case keyword
                            Case "true"
                                Return New PdfBoolean(True)
                            Case "false"
                                Return New PdfBoolean(False)
                            Case "null"
                                Return PdfNull.Value
                            Case Else
                                Throw New NotSupportedException($"Unsupported PDF keyword '{keyword}'.")
                        End Select
                End Select
            End Function

            Public Function ReadInteger() As Integer
                Dim token As String = ReadSimpleToken()
                Return Integer.Parse(token, CultureInfo.InvariantCulture)
            End Function

            Public Function ReadKeyword() As String
                Return ReadSimpleToken()
            End Function

            Public Function PeekKeyword() As String
                Dim snapshot As Integer = _position
                Dim token As String = ReadSimpleToken()
                _position = snapshot
                Return token
            End Function

            Public Function ReadBytes(length As Integer) As Byte()
                Dim buffer(length - 1) As Byte
                Array.Copy(_data, _position, buffer, 0, length)
                _position += length
                Return buffer
            End Function

            Public Sub SkipWhiteSpaceAndComments()
                PdfParser.SkipWhiteSpaceAndComments(_data, _position)
            End Sub

            Public Sub ConsumeStreamLineEnding()
                If _position < _data.Length AndAlso _data(_position) = 13 Then
                    _position += 1
                    If _position < _data.Length AndAlso _data(_position) = 10 Then
                        _position += 1
                    End If
                ElseIf _position < _data.Length AndAlso _data(_position) = 10 Then
                    _position += 1
                End If
            End Sub

            Public Sub SkipPotentialStreamTerminator()
                If _position < _data.Length AndAlso _data(_position) = 13 Then
                    _position += 1
                    If _position < _data.Length AndAlso _data(_position) = 10 Then
                        _position += 1
                    End If
                ElseIf _position < _data.Length AndAlso _data(_position) = 10 Then
                    _position += 1
                End If
            End Sub

            Private Function ReadDictionary() As PdfDictionary
                _position += 2
                Dim dictionary As New PdfDictionary()

                While True
                    SkipWhiteSpaceAndComments()
                    If _position + 1 < _data.Length AndAlso _data(_position) = AscW(">"c) AndAlso _data(_position + 1) = AscW(">"c) Then
                        _position += 2
                        Exit While
                    End If

                    Dim key As PdfName = ReadName()
                    Dim value As PdfValue = ReadValue()
                    dictionary(key.Value) = value
                End While

                Return dictionary
            End Function

            Private Function ReadArray() As PdfArray
                _position += 1
                Dim items As New List(Of PdfValue)()

                While True
                    SkipWhiteSpaceAndComments()
                    If _position >= _data.Length Then
                        Throw New InvalidOperationException("Array was not terminated.")
                    End If

                    If _data(_position) = AscW("]"c) Then
                        _position += 1
                        Exit While
                    End If

                    items.Add(ReadValue())
                End While

                Return New PdfArray(items)
            End Function

            Private Function ReadName() As PdfName
                _position += 1
                Dim start As Integer = _position
                While _position < _data.Length AndAlso Not IsDelimiter(_data(_position))
                    _position += 1
                End While

                Dim name As String = Encoding.ASCII.GetString(_data, start, _position - start)
                Return New PdfName(name)
            End Function

            Private Function ReadLiteralString() As PdfLiteralString
                _position += 1
                Dim depth As Integer = 1
                Dim builder As New StringBuilder()

                While _position < _data.Length AndAlso depth > 0
                    Dim current As Byte = _data(_position)
                    _position += 1
                    If current = AscW("\"c) Then
                        If _position >= _data.Length Then Exit While
                        Dim escaped As Byte = _data(_position)
                        _position += 1
                        Select Case escaped
                            Case AscW("n"c)
                                builder.Append(ChrW(10))
                            Case AscW("r"c)
                                builder.Append(ChrW(13))
                            Case AscW("t"c)
                                builder.Append(ChrW(9))
                            Case AscW("b"c)
                                builder.Append(ChrW(8))
                            Case AscW("f"c)
                                builder.Append(ChrW(12))
                            Case AscW("("c), AscW(")"c), AscW("\"c)
                                builder.Append(ChrW(escaped))
                            Case 10, 13
                                If escaped = 13 AndAlso _position < _data.Length AndAlso _data(_position) = 10 Then
                                    _position += 1
                                End If
                            Case Else
                                builder.Append(ChrW(escaped))
                        End Select
                    ElseIf current = AscW("("c) Then
                        depth += 1
                        builder.Append("("c)
                    ElseIf current = AscW(")"c) Then
                        depth -= 1
                        If depth > 0 Then
                            builder.Append(")"c)
                        End If
                    Else
                        builder.Append(ChrW(current))
                    End If
                End While

                If depth <> 0 Then
                    Throw New InvalidOperationException("String literal was not terminated.")
                End If

                Return New PdfLiteralString(builder.ToString())
            End Function

            Private Function ReadHexString() As PdfHexString
                _position += 1
                Dim builder As New StringBuilder()
                While _position < _data.Length AndAlso _data(_position) <> AscW(">"c)
                    Dim current As Char = ChrW(_data(_position))
                    If Not Char.IsWhiteSpace(current) Then
                        builder.Append(current)
                    End If

                    _position += 1
                End While

                If _position >= _data.Length Then
                    Throw New InvalidOperationException("Hex string was not terminated.")
                End If

                _position += 1
                Return New PdfHexString(builder.ToString())
            End Function

            Private Function ReadNumberOrReference() As PdfValue
                Dim firstToken As String = ReadSimpleToken()
                Dim snapshot As Integer = _position

                Try
                    SkipWhiteSpaceAndComments()
                    Dim secondToken As String = ReadSimpleToken()
                    SkipWhiteSpaceAndComments()
                    Dim maybeR As String = ReadSimpleToken()
                    If IsIntegerToken(firstToken) AndAlso IsIntegerToken(secondToken) AndAlso maybeR = "R" Then
                        Return New PdfIndirectReference(Integer.Parse(firstToken, CultureInfo.InvariantCulture), Integer.Parse(secondToken, CultureInfo.InvariantCulture))
                    End If
                Catch
                End Try

                _position = snapshot
                Return New PdfNumber(firstToken)
            End Function

            Private Function ReadSimpleToken() As String
                SkipWhiteSpaceAndComments()
                Dim start As Integer = _position
                While _position < _data.Length AndAlso Not IsDelimiter(_data(_position))
                    _position += 1
                End While

                Return Encoding.ASCII.GetString(_data, start, _position - start)
            End Function

            Private Shared Function IsIntegerToken(value As String) As Boolean
                If String.IsNullOrEmpty(value) Then
                    Return False
                End If

                For index As Integer = 0 To value.Length - 1
                    Dim character As Char = value(index)
                    If index = 0 AndAlso (character = "+"c OrElse character = "-"c) Then
                        Continue For
                    End If

                    If Not Char.IsDigit(character) Then
                        Return False
                    End If
                Next

                Return True
            End Function

            Private Shared Function IsNumericToken(value As Byte) As Boolean
                Return value >= AscW("0"c) AndAlso value <= AscW("9"c)
            End Function
        End Class
    End Class
End Namespace
