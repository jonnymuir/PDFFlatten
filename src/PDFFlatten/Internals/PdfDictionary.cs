using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PDFFlatten.Internals;

/// <summary>
/// Represents a PDF dictionary with ordinal key semantics.
/// </summary>
internal sealed class PdfDictionary : PdfValue
{
    private readonly Dictionary<string, PdfValue> _items = new(StringComparer.Ordinal);

    internal PdfValue this[string key]
    {
        get => _items[key];
        set => _items[key] = value;
    }

    internal IReadOnlyDictionary<string, PdfValue> Items => new ReadOnlyDictionary<string, PdfValue>(_items);

    internal void Add(string key, PdfValue value)
    {
        _items.Add(key, value);
    }

    internal bool TryGetValue(string key, out PdfValue? value)
    {
        return _items.TryGetValue(key, out value);
    }

    internal PdfArray RequireArray(string key)
    {
        var value = GetRequiredValue(key);
        if (value is not PdfArray array)
        {
            throw new InvalidOperationException($"Dictionary key /{key} is not an array.");
        }

        return array;
    }

    internal PdfDictionary RequireDictionary(string key)
    {
        var value = GetRequiredValue(key);
        if (value is not PdfDictionary dictionary)
        {
            throw new InvalidOperationException($"Dictionary key /{key} is not a dictionary.");
        }

        return dictionary;
    }

    internal PdfIndirectReference RequireReference(string key)
    {
        var value = GetRequiredValue(key);
        if (value is not PdfIndirectReference reference)
        {
            throw new InvalidOperationException($"Dictionary key /{key} is not an indirect reference.");
        }

        return reference;
    }

    internal string GetNameValue(string key)
    {
        var value = GetRequiredValue(key);
        if (value is not PdfName name)
        {
            throw new InvalidOperationException($"Dictionary key /{key} is not a name.");
        }

        return name.Value;
    }

    internal void Remove(string key)
    {
        _items.Remove(key);
    }

    private PdfValue GetRequiredValue(string key)
    {
        if (!_items.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"Dictionary key /{key} was not found.");
        }

        return value;
    }
}
