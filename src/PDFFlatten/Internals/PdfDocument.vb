Imports System
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Linq

Namespace Internals
    Friend NotInheritable Class PdfDocument
        Private ReadOnly _objects As Dictionary(Of Integer, PdfIndirectObject)
        Private _nextObjectNumber As Integer

        Public Sub New(originalBytes As Byte(), preamble As Byte(), trailer As PdfDictionary, objects As IEnumerable(Of PdfIndirectObject))
            If originalBytes Is Nothing Then Throw New ArgumentNullException(NameOf(originalBytes))
            If preamble Is Nothing Then Throw New ArgumentNullException(NameOf(preamble))
            If trailer Is Nothing Then Throw New ArgumentNullException(NameOf(trailer))
            If objects Is Nothing Then Throw New ArgumentNullException(NameOf(objects))

            Me.OriginalBytes = originalBytes
            Me.Preamble = preamble
            Me.Trailer = trailer
            _objects = objects.ToDictionary(Function(item) item.Number)
            _nextObjectNumber = If(_objects.Count = 0, 1, _objects.Keys.Max() + 1)
        End Sub

        Public ReadOnly Property OriginalBytes As Byte()
        Public ReadOnly Property Preamble As Byte()
        Public Property Trailer As PdfDictionary

        Public ReadOnly Property Objects As IReadOnlyCollection(Of PdfIndirectObject)
            Get
                Return New ReadOnlyCollection(Of PdfIndirectObject)(_objects.Values.OrderBy(Function(item) item.Number).ToList())
            End Get
        End Property

        Public Function MaxObjectNumber() As Integer
            If _objects.Count = 0 Then
                Return 0
            End If

            Return _objects.Keys.Max()
        End Function

        Public Function GetRequiredObject(reference As PdfIndirectReference) As PdfIndirectObject
            Dim result As PdfIndirectObject = Nothing
            If Not _objects.TryGetValue(reference.ObjectNumber, result) Then
                Throw New InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R was not found.")
            End If

            Return result
        End Function

        Public Function GetRequiredDictionary(reference As PdfIndirectReference) As PdfDictionary
            Dim dictionary As PdfDictionary = TryCast(GetRequiredObject(reference).Value, PdfDictionary)
            If dictionary Is Nothing Then
                Throw New InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R is not a dictionary.")
            End If

            Return dictionary
        End Function

        Public Function GetRequiredStream(reference As PdfIndirectReference) As PdfStream
            Dim stream As PdfStream = TryCast(GetRequiredObject(reference).Value, PdfStream)
            If stream Is Nothing Then
                Throw New InvalidOperationException($"Object {reference.ObjectNumber} {reference.Generation} R is not a stream.")
            End If

            Return stream
        End Function

        Public Function AddObject(value As PdfValue) As PdfIndirectReference
            Dim objectNumber As Integer = _nextObjectNumber
            _nextObjectNumber += 1
            _objects(objectNumber) = New PdfIndirectObject(objectNumber, 0, value)
            Return New PdfIndirectReference(objectNumber, 0)
        End Function
    End Class

    Friend NotInheritable Class PdfIndirectObject
        Public Sub New(number As Integer, generation As Integer, value As PdfValue)
            Me.Number = number
            Me.Generation = generation
            Me.Value = value
        End Sub

        Public ReadOnly Property Number As Integer
        Public ReadOnly Property Generation As Integer
        Public Property Value As PdfValue
    End Class

    Friend MustInherit Class PdfValue
    End Class

    Friend NotInheritable Class PdfNull
        Inherits PdfValue

        Public Shared ReadOnly Property Value As New PdfNull()

        Private Sub New()
        End Sub
    End Class

    Friend NotInheritable Class PdfBoolean
        Inherits PdfValue

        Public Sub New(value As Boolean)
            Me.Value = value
        End Sub

        Public ReadOnly Property Value As Boolean
    End Class

    Friend NotInheritable Class PdfNumber
        Inherits PdfValue

        Public Sub New(rawValue As String)
            Me.RawValue = rawValue
            Me.NumericValue = Double.Parse(rawValue, CultureInfo.InvariantCulture)
            Me.IsInteger = rawValue.IndexOf("."c) = -1 AndAlso rawValue.IndexOf("E"c) = -1 AndAlso rawValue.IndexOf("e"c) = -1
        End Sub

        Public Sub New(value As Integer)
            Me.RawValue = value.ToString(CultureInfo.InvariantCulture)
            Me.NumericValue = value
            Me.IsInteger = True
        End Sub

        Public Sub New(value As Double)
            Me.RawValue = value.ToString("0.###", CultureInfo.InvariantCulture)
            Me.NumericValue = value
            Me.IsInteger = False
        End Sub

        Public ReadOnly Property RawValue As String
        Public ReadOnly Property NumericValue As Double
        Public ReadOnly Property IsInteger As Boolean
    End Class

    Friend NotInheritable Class PdfName
        Inherits PdfValue

        Public Sub New(value As String)
            Me.Value = value
        End Sub

        Public ReadOnly Property Value As String
    End Class

    Friend NotInheritable Class PdfLiteralString
        Inherits PdfValue

        Public Sub New(value As String)
            Me.Value = value
        End Sub

        Public ReadOnly Property Value As String
    End Class

    Friend NotInheritable Class PdfHexString
        Inherits PdfValue

        Public Sub New(value As String)
            Me.Value = value
        End Sub

        Public ReadOnly Property Value As String
    End Class

    Friend NotInheritable Class PdfArray
        Inherits PdfValue

        Public Sub New(items As IEnumerable(Of PdfValue))
            Me.Items = New List(Of PdfValue)(items)
        End Sub

        Public ReadOnly Property Items As List(Of PdfValue)
    End Class

    Friend NotInheritable Class PdfIndirectReference
        Inherits PdfValue

        Public Sub New(objectNumber As Integer, generation As Integer)
            Me.ObjectNumber = objectNumber
            Me.Generation = generation
        End Sub

        Public ReadOnly Property ObjectNumber As Integer
        Public ReadOnly Property Generation As Integer
    End Class

    Friend NotInheritable Class PdfDictionary
        Inherits PdfValue

        Private ReadOnly _items As New Dictionary(Of String, PdfValue)(StringComparer.Ordinal)

        Default Public Property Item(key As String) As PdfValue
            Get
                Return _items(key)
            End Get
            Set(value As PdfValue)
                _items(key) = value
            End Set
        End Property

        Public ReadOnly Property Items As IReadOnlyDictionary(Of String, PdfValue)
            Get
                Return New ReadOnlyDictionary(Of String, PdfValue)(_items)
            End Get
        End Property

        Public Sub Add(key As String, value As PdfValue)
            _items.Add(key, value)
        End Sub

        Public Function TryGetValue(key As String, ByRef value As PdfValue) As Boolean
            Return _items.TryGetValue(key, value)
        End Function

        Public Function RequireArray(key As String) As PdfArray
            Dim value As PdfValue = Nothing
            If Not _items.TryGetValue(key, value) Then
                Throw New InvalidOperationException($"Dictionary key /{key} was not found.")
            End If

            Dim array As PdfArray = TryCast(value, PdfArray)
            If array Is Nothing Then
                Throw New InvalidOperationException($"Dictionary key /{key} is not an array.")
            End If

            Return array
        End Function

        Public Function RequireDictionary(key As String) As PdfDictionary
            Dim value As PdfValue = Nothing
            If Not _items.TryGetValue(key, value) Then
                Throw New InvalidOperationException($"Dictionary key /{key} was not found.")
            End If

            Dim dictionary As PdfDictionary = TryCast(value, PdfDictionary)
            If dictionary Is Nothing Then
                Throw New InvalidOperationException($"Dictionary key /{key} is not a dictionary.")
            End If

            Return dictionary
        End Function

        Public Function RequireReference(key As String) As PdfIndirectReference
            Dim value As PdfValue = Nothing
            If Not _items.TryGetValue(key, value) Then
                Throw New InvalidOperationException($"Dictionary key /{key} was not found.")
            End If

            Dim reference As PdfIndirectReference = TryCast(value, PdfIndirectReference)
            If reference Is Nothing Then
                Throw New InvalidOperationException($"Dictionary key /{key} is not an indirect reference.")
            End If

            Return reference
        End Function

        Public Function GetNameValue(key As String) As String
            Dim value As PdfValue = Nothing
            If Not _items.TryGetValue(key, value) Then
                Throw New InvalidOperationException($"Dictionary key /{key} was not found.")
            End If

            Dim name As PdfName = TryCast(value, PdfName)
            If name Is Nothing Then
                Throw New InvalidOperationException($"Dictionary key /{key} is not a name.")
            End If

            Return name.Value
        End Function

        Public Sub Remove(key As String)
            _items.Remove(key)
        End Sub
    End Class

    Friend NotInheritable Class PdfStream
        Inherits PdfValue

        Public Sub New(dictionary As PdfDictionary, data As Byte())
            If dictionary Is Nothing Then Throw New ArgumentNullException(NameOf(dictionary))
            If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))

            Me.Dictionary = dictionary
            Me.Data = data
        End Sub

        Public ReadOnly Property Dictionary As PdfDictionary
        Public ReadOnly Property Data As Byte()
    End Class

    Friend NotInheritable Class PdfValueConversions
        Private Sub New()
        End Sub

        Public Shared Function RequireNumber(value As PdfValue) As Double
            Dim number As PdfNumber = TryCast(value, PdfNumber)
            If number Is Nothing Then
                Throw New InvalidOperationException("Expected a PDF number.")
            End If

            Return number.NumericValue
        End Function
    End Class
End Namespace
