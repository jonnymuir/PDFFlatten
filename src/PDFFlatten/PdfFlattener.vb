Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports PDFFlatten.Internals

''' <summary>
''' Flattens AcroForm widget appearances into page content streams.
''' </summary>
Public NotInheritable Class PdfFlattener
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
