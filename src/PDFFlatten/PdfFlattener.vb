Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.RegularExpressions
Imports PDFFlatten.Internals

''' <summary>
''' Flattens AcroForm widget appearances into page content streams.
''' </summary>
Public NotInheritable Class PdfFlattener
    Private Shared ReadOnly FontOperatorRegex As New Regex("/(?<font>[^\s/]+)\s+[-+]?(?:\d+(?:\.\d+)?|\.\d+)\s+Tf", RegexOptions.CultureInvariant)

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Returns a flattened copy of the PDF in <paramref name="input"/>.
    ''' </summary>
    ''' <remarks>
    ''' Caller-owned streams remain open. The returned stream is rewindable and positioned at the beginning.
    ''' </remarks>
    Public Shared Function Flatten(input As Stream) As Stream
        If input Is Nothing Then Throw New ArgumentNullException(NameOf(input))
        If Not input.CanRead Then Throw New ArgumentException("Input stream must be readable.", NameOf(input))

        Dim output As New MemoryStream()
        Flatten(input, output)
        output.Position = 0
        Return output
    End Function

    ''' <summary>
    ''' Writes a flattened copy of the PDF from <paramref name="input"/> into <paramref name="output"/>.
    ''' </summary>
    ''' <remarks>
    ''' Caller-owned streams remain open. The output stream is left at the end of the written PDF.
    ''' </remarks>
    Public Shared Sub Flatten(input As Stream, output As Stream)
        If input Is Nothing Then Throw New ArgumentNullException(NameOf(input))
        If output Is Nothing Then Throw New ArgumentNullException(NameOf(output))
        If Not input.CanRead Then Throw New ArgumentException("Input stream must be readable.", NameOf(input))
        If Not output.CanWrite Then Throw New ArgumentException("Output stream must be writable.", NameOf(output))

        Dim originalBytes As Byte() = ReadAllBytes(input)
        Dim document As PdfDocument = PdfParser.Parse(originalBytes)
        Dim flattenedBytes As Byte() = FlattenDocument(document)
        output.Write(flattenedBytes, 0, flattenedBytes.Length)
    End Sub

    Private Shared Function FlattenDocument(document As PdfDocument) As Byte()
        Dim catalogRef As PdfIndirectReference = document.Trailer.RequireReference("Root")
        Dim catalog As PdfDictionary = document.GetRequiredDictionary(catalogRef)
        Dim pagesRef As PdfIndirectReference = catalog.RequireReference("Pages")
        Dim widgetsFlattened As Integer = 0
        Dim placementIndex As Integer = 0

        For Each pageRef As PdfIndirectReference In EnumeratePages(document, pagesRef)
            widgetsFlattened += FlattenPage(document, pageRef, placementIndex)
        Next

        If widgetsFlattened = 0 Then
            Return DirectCast(document.OriginalBytes.Clone(), Byte())
        End If

        catalog.Remove("AcroForm")
        Return PdfSerializer.Serialize(document)
    End Function

    Private Shared Iterator Function EnumeratePages(document As PdfDocument, pagesRef As PdfIndirectReference) As IEnumerable(Of PdfIndirectReference)
        Dim node As PdfDictionary = document.GetRequiredDictionary(pagesRef)
        Dim nodeType As String = node.GetNameValue("Type")

        If String.Equals(nodeType, "Page", StringComparison.Ordinal) Then
            Yield pagesRef
            Return
        End If

        If Not String.Equals(nodeType, "Pages", StringComparison.Ordinal) Then
            Throw New NotSupportedException("Unsupported page tree node type.")
        End If

        Dim kids As PdfArray = node.RequireArray("Kids")
        For Each kidValue As PdfValue In kids.Items
            Dim kidRef As PdfIndirectReference = TryCast(kidValue, PdfIndirectReference)
            If kidRef Is Nothing Then
                Throw New NotSupportedException("Page tree kids must be indirect references.")
            End If

            For Each pageRef As PdfIndirectReference In EnumeratePages(document, kidRef)
                Yield pageRef
            Next
        Next
    End Function

    Private Shared Function FlattenPage(document As PdfDocument, pageRef As PdfIndirectReference, ByRef placementIndex As Integer) As Integer
        Dim page As PdfDictionary = document.GetRequiredDictionary(pageRef)
        Dim annotsValue As PdfValue = Nothing
        If Not page.TryGetValue("Annots", annotsValue) Then
            Return 0
        End If

        Dim annots As PdfArray = TryCast(annotsValue, PdfArray)
        If annots Is Nothing Then
            Throw New NotSupportedException("Only direct annotation arrays are supported.")
        End If

        Dim remainingAnnotations As New List(Of PdfValue)()
        Dim placements As New List(Of FlattenPlacement)()

        For Each annotationValue As PdfValue In annots.Items
            Dim annotationRef As PdfIndirectReference = TryCast(annotationValue, PdfIndirectReference)
            If annotationRef Is Nothing Then
                remainingAnnotations.Add(annotationValue)
                Continue For
            End If

            Dim annotation As PdfDictionary = document.GetRequiredDictionary(annotationRef)
            Dim subtype As String = annotation.GetNameValue("Subtype")
            If Not String.Equals(subtype, "Widget", StringComparison.Ordinal) Then
                remainingAnnotations.Add(annotationValue)
                Continue For
            End If

            placements.Add(CreatePlacement(document, annotation, placementIndex))
            placementIndex += 1
        Next

        If placements.Count = 0 Then
            Return 0
        End If

        Dim resources As PdfDictionary = ResolveResourceDictionary(document, page)
        Dim xObjectDictionary As PdfDictionary = ResolveNestedDictionary(document, resources, "XObject")

        Dim contentBuilder As New StringBuilder()
        For Each placement As FlattenPlacement In placements
            xObjectDictionary(placement.ResourceName) = placement.AppearanceReference
            contentBuilder.Append("q ")
            contentBuilder.Append(FormatNumber(placement.ScaleX))
            contentBuilder.Append(" 0 0 ")
            contentBuilder.Append(FormatNumber(placement.ScaleY))
            contentBuilder.Append(" ")
            contentBuilder.Append(FormatNumber(placement.TranslateX))
            contentBuilder.Append(" ")
            contentBuilder.Append(FormatNumber(placement.TranslateY))
            contentBuilder.Append(" cm /")
            contentBuilder.Append(placement.ResourceName)
            contentBuilder.AppendLine(" Do Q")
        Next

        Dim appendedStream As New PdfStream(New PdfDictionary(), Encoding.ASCII.GetBytes(contentBuilder.ToString()))
        Dim appendedRef As PdfIndirectReference = document.AddObject(appendedStream)
        page("Contents") = AppendContentReference(page("Contents"), appendedRef)

        If remainingAnnotations.Count = 0 Then
            page.Remove("Annots")
        Else
            page("Annots") = New PdfArray(remainingAnnotations)
        End If

        Return placements.Count
    End Function

    Private Shared Function CreatePlacement(document As PdfDocument, annotation As PdfDictionary, placementIndex As Integer) As FlattenPlacement
        Dim rect As PdfArray = annotation.RequireArray("Rect")
        If rect.Items.Count <> 4 Then
            Throw New NotSupportedException("Widget rectangles must contain four numbers.")
        End If

        Dim appearanceDictionary As PdfDictionary = annotation.RequireDictionary("AP")
        Dim normalAppearanceRef As PdfIndirectReference = appearanceDictionary.RequireReference("N")
        Dim normalAppearance As PdfStream = document.GetRequiredStream(normalAppearanceRef)
        RepairTextAppearanceResources(document, annotation, normalAppearance)
        Dim box As PdfArray = normalAppearance.Dictionary.RequireArray("BBox")
        If box.Items.Count <> 4 Then
            Throw New NotSupportedException("Appearance BBoxes must contain four numbers.")
        End If

        Dim left As Double = PdfValueConversions.RequireNumber(rect.Items(0))
        Dim bottom As Double = PdfValueConversions.RequireNumber(rect.Items(1))
        Dim right As Double = PdfValueConversions.RequireNumber(rect.Items(2))
        Dim top As Double = PdfValueConversions.RequireNumber(rect.Items(3))
        Dim boxLeft As Double = PdfValueConversions.RequireNumber(box.Items(0))
        Dim boxBottom As Double = PdfValueConversions.RequireNumber(box.Items(1))
        Dim boxRight As Double = PdfValueConversions.RequireNumber(box.Items(2))
        Dim boxTop As Double = PdfValueConversions.RequireNumber(box.Items(3))

        Dim boxWidth As Double = boxRight - boxLeft
        Dim boxHeight As Double = boxTop - boxBottom
        If boxWidth <= 0 OrElse boxHeight <= 0 Then
            Throw New NotSupportedException("Appearance BBoxes must be positive.")
        End If

        Dim rectWidth As Double = right - left
        Dim rectHeight As Double = top - bottom
        Dim scaleX As Double = rectWidth / boxWidth
        Dim scaleY As Double = rectHeight / boxHeight

        Return New FlattenPlacement(
            $"FldFlat{placementIndex + 1:000}",
            normalAppearanceRef,
            scaleX,
            scaleY,
            left - (boxLeft * scaleX),
            bottom - (boxBottom * scaleY))
    End Function

    Private Shared Sub RepairTextAppearanceResources(document As PdfDocument, annotation As PdfDictionary, appearance As PdfStream)
        If Not IsTextField(annotation) Then
            Return
        End If

        Dim referencedFonts As List(Of String) = GetReferencedFontNames(appearance)
        If referencedFonts.Count = 0 Then
            Return
        End If

        Dim resources As PdfDictionary = ResolveOrCreateNestedDictionary(document, appearance.Dictionary, "Resources")
        Dim fonts As PdfDictionary = ResolveOrCreateNestedDictionary(document, resources, "Font")

        For Each fontName As String In referencedFonts
            If HasResolvableFont(document, fonts, fontName) Then
                Continue For
            End If

            Dim repairedFont As PdfValue = Nothing
            If TryResolveWidgetDefaultFont(document, annotation, repairedFont) Then
                fonts(fontName) = repairedFont
                Continue For
            End If

            Dim standardFont As PdfDictionary = CreateStandardFont(fontName)
            If standardFont IsNot Nothing Then
                fonts(fontName) = document.AddObject(standardFont)
            End If
        Next
    End Sub

    Private Shared Function IsTextField(annotation As PdfDictionary) As Boolean
        Dim fieldType As PdfValue = Nothing
        If Not annotation.TryGetValue("FT", fieldType) Then
            Return False
        End If

        Dim fieldTypeName As PdfName = TryCast(fieldType, PdfName)
        Return fieldTypeName IsNot Nothing AndAlso String.Equals(fieldTypeName.Value, "Tx", StringComparison.Ordinal)
    End Function

    Private Shared Function GetReferencedFontNames(appearance As PdfStream) As List(Of String)
        Dim decodedData As Byte() = DecodeStreamData(appearance)
        If decodedData Is Nothing OrElse decodedData.Length = 0 Then
            Return New List(Of String)()
        End If

        Dim content As String = Encoding.ASCII.GetString(decodedData)
        Dim fontNames As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)

        For Each match As Match In FontOperatorRegex.Matches(content)
            Dim fontName As String = match.Groups("font").Value
            If fontName.Length > 0 AndAlso seen.Add(fontName) Then
                fontNames.Add(fontName)
            End If
        Next

        Return fontNames
    End Function

    Private Shared Function DecodeStreamData(appearance As PdfStream) As Byte()
        Dim filterValue As PdfValue = Nothing
        If Not appearance.Dictionary.TryGetValue("Filter", filterValue) Then
            Return appearance.Data
        End If

        Dim filterName As PdfName = TryCast(filterValue, PdfName)
        If filterName Is Nothing OrElse Not String.Equals(filterName.Value, "FlateDecode", StringComparison.Ordinal) Then
            Return Nothing
        End If

        Try
            Return InflateData(appearance.Data)
        Catch ex As InvalidDataException
            If appearance.Data.Length <= 6 Then
                Return Nothing
            End If

            Dim rawDeflate(appearance.Data.Length - 7) As Byte
            Array.Copy(appearance.Data, 2, rawDeflate, 0, rawDeflate.Length)

            Try
                Return InflateData(rawDeflate)
            Catch innerEx As InvalidDataException
                Return Nothing
            End Try
        End Try
    End Function

    Private Shared Function InflateData(data As Byte()) As Byte()
        Using input As New MemoryStream(data)
            Using inflater As New DeflateStream(input, CompressionMode.Decompress)
                Using output As New MemoryStream()
                    inflater.CopyTo(output)
                    Return output.ToArray()
                End Using
            End Using
        End Using
    End Function

    Private Shared Function HasResolvableFont(document As PdfDocument, fontDictionary As PdfDictionary, fontName As String) As Boolean
        Dim fontValue As PdfValue = Nothing
        If Not fontDictionary.TryGetValue(fontName, fontValue) Then
            Return False
        End If

        If TypeOf fontValue Is PdfDictionary Then
            Return True
        End If

        Dim fontReference As PdfIndirectReference = TryCast(fontValue, PdfIndirectReference)
        If fontReference Is Nothing Then
            Return False
        End If

        Try
            document.GetRequiredDictionary(fontReference)
            Return True
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

    Private Shared Function TryResolveWidgetDefaultFont(document As PdfDocument, annotation As PdfDictionary, ByRef fontValue As PdfValue) As Boolean
        fontValue = Nothing

        Dim defaultAppearance As String = Nothing
        If Not TryGetLiteralString(annotation, "DA", defaultAppearance) Then
            Return False
        End If

        Dim match As Match = FontOperatorRegex.Match(defaultAppearance)
        If Not match.Success Then
            Return False
        End If

        Dim widgetResources As PdfDictionary = TryResolveDictionary(document, annotation, "DR")
        If widgetResources Is Nothing Then
            Return False
        End If

        Dim widgetFonts As PdfDictionary = TryResolveNestedDictionary(document, widgetResources, "Font")
        If widgetFonts Is Nothing Then
            Return False
        End If

        Return widgetFonts.TryGetValue(match.Groups("font").Value, fontValue) AndAlso fontValue IsNot Nothing
    End Function

    Private Shared Function TryResolveDictionary(document As PdfDocument, parent As PdfDictionary, key As String) As PdfDictionary
        Dim value As PdfValue = Nothing
        If Not parent.TryGetValue(key, value) Then
            Return Nothing
        End If

        Dim reference As PdfIndirectReference = TryCast(value, PdfIndirectReference)
        If reference IsNot Nothing Then
            Try
                Return document.GetRequiredDictionary(reference)
            Catch ex As InvalidOperationException
                Return Nothing
            End Try
        End If

        Return TryCast(value, PdfDictionary)
    End Function

    Private Shared Function ResolveOrCreateNestedDictionary(document As PdfDocument, parent As PdfDictionary, key As String) As PdfDictionary
        Dim existing As PdfDictionary = TryResolveDictionary(document, parent, key)
        If existing IsNot Nothing Then
            Return existing
        End If

        Dim created As New PdfDictionary()
        parent(key) = created
        Return created
    End Function

    Private Shared Function TryResolveNestedDictionary(document As PdfDocument, parent As PdfDictionary, key As String) As PdfDictionary
        Return TryResolveDictionary(document, parent, key)
    End Function

    Private Shared Function TryGetLiteralString(dictionary As PdfDictionary, key As String, ByRef value As String) As Boolean
        value = Nothing

        Dim rawValue As PdfValue = Nothing
        If Not dictionary.TryGetValue(key, rawValue) Then
            Return False
        End If

        Dim literal As PdfLiteralString = TryCast(rawValue, PdfLiteralString)
        If literal Is Nothing Then
            Return False
        End If

        value = literal.Value
        Return True
    End Function

    Private Shared Function CreateStandardFont(fontName As String) As PdfDictionary
        Dim baseFontName As String = Nothing
        Select Case fontName
            Case "Helv"
                baseFontName = "Helvetica"
            Case "HeBo"
                baseFontName = "Helvetica-Bold"
            Case "HeOb"
                baseFontName = "Helvetica-Oblique"
            Case "HeBO"
                baseFontName = "Helvetica-BoldOblique"
            Case "Cour"
                baseFontName = "Courier"
            Case "CoBo"
                baseFontName = "Courier-Bold"
            Case "CoOb"
                baseFontName = "Courier-Oblique"
            Case "CoBO"
                baseFontName = "Courier-BoldOblique"
            Case "TiRo"
                baseFontName = "Times-Roman"
            Case "TiBo"
                baseFontName = "Times-Bold"
            Case "TiIt"
                baseFontName = "Times-Italic"
            Case "TiBI"
                baseFontName = "Times-BoldItalic"
            Case "ZaDb"
                baseFontName = "ZapfDingbats"
            Case Else
                Return Nothing
        End Select

        Dim font As New PdfDictionary()
        font("Type") = New PdfName("Font")
        font("Subtype") = New PdfName("Type1")
        font("BaseFont") = New PdfName(baseFontName)

        If Not String.Equals(baseFontName, "ZapfDingbats", StringComparison.Ordinal) Then
            font("Encoding") = New PdfName("WinAnsiEncoding")
        End If

        Return font
    End Function

    Private Shared Function ResolveResourceDictionary(document As PdfDocument, page As PdfDictionary) As PdfDictionary
        Dim resourcesValue As PdfValue = Nothing
        If Not page.TryGetValue("Resources", resourcesValue) Then
            Dim resources As New PdfDictionary()
            page("Resources") = resources
            Return resources
        End If

        Dim resourcesReference As PdfIndirectReference = TryCast(resourcesValue, PdfIndirectReference)
        If resourcesReference IsNot Nothing Then
            Return document.GetRequiredDictionary(resourcesReference)
        End If

        Dim resourcesDictionary As PdfDictionary = TryCast(resourcesValue, PdfDictionary)
        If resourcesDictionary Is Nothing Then
            Throw New NotSupportedException("Page resources must be a dictionary or indirect reference.")
        End If

        Return resourcesDictionary
    End Function

    Private Shared Function ResolveNestedDictionary(document As PdfDocument, parent As PdfDictionary, key As String) As PdfDictionary
        Dim value As PdfValue = Nothing
        If Not parent.TryGetValue(key, value) Then
            Dim created As New PdfDictionary()
            parent(key) = created
            Return created
        End If

        Dim reference As PdfIndirectReference = TryCast(value, PdfIndirectReference)
        If reference IsNot Nothing Then
            Return document.GetRequiredDictionary(reference)
        End If

        Dim dictionary As PdfDictionary = TryCast(value, PdfDictionary)
        If dictionary Is Nothing Then
            Throw New NotSupportedException($"/{key} must resolve to a dictionary.")
        End If

        Return dictionary
    End Function

    Private Shared Function AppendContentReference(existingContents As PdfValue, appendedReference As PdfIndirectReference) As PdfValue
        Dim arrayContents As PdfArray = TryCast(existingContents, PdfArray)
        If arrayContents IsNot Nothing Then
            Dim updatedItems As New List(Of PdfValue)(arrayContents.Items)
            updatedItems.Add(appendedReference)
            Return New PdfArray(updatedItems)
        End If

        Return New PdfArray(New PdfValue() {existingContents, appendedReference})
    End Function

    Private Shared Function ReadAllBytes(input As Stream) As Byte()
        If TypeOf input Is MemoryStream Then
            Return DirectCast(DirectCast(input, MemoryStream).ToArray(), Byte())
        End If

        If input.CanSeek Then
            input.Position = 0
        End If

        Using buffer As New MemoryStream()
            input.CopyTo(buffer)
            Return buffer.ToArray()
        End Using
    End Function

    Private Shared Function FormatNumber(value As Double) As String
        Return value.ToString("0.###", CultureInfo.InvariantCulture)
    End Function

    Private NotInheritable Class FlattenPlacement
        Public Sub New(resourceName As String, appearanceReference As PdfIndirectReference, scaleX As Double, scaleY As Double, translateX As Double, translateY As Double)
            Me.ResourceName = resourceName
            Me.AppearanceReference = appearanceReference
            Me.ScaleX = scaleX
            Me.ScaleY = scaleY
            Me.TranslateX = translateX
            Me.TranslateY = translateY
        End Sub

        Public ReadOnly Property ResourceName As String
        Public ReadOnly Property AppearanceReference As PdfIndirectReference
        Public ReadOnly Property ScaleX As Double
        Public ReadOnly Property ScaleY As Double
        Public ReadOnly Property TranslateX As Double
        Public ReadOnly Property TranslateY As Double
    End Class
End Class
