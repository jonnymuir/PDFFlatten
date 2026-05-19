using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace PDFFlatten.Internals;

internal sealed class PdfStandardEncryption
{
    private static readonly byte[] PasswordPadding =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    };

    private readonly byte[] _fileKey;
    private readonly int _encryptObjectNumber;
    private readonly int _encryptGeneration;

    private PdfStandardEncryption(byte[] fileKey, int encryptObjectNumber, int encryptGeneration)
    {
        _fileKey = fileKey;
        _encryptObjectNumber = encryptObjectNumber;
        _encryptGeneration = encryptGeneration;
    }

    internal static PdfStandardEncryption? CreateIfSupported(
        PdfDictionary trailer,
        byte[] data,
        IDictionary<int, XrefEntry> xrefEntries)
    {
        if (!trailer.TryGetValue("Encrypt", out var encryptValue) || encryptValue is null)
        {
            return null;
        }

        if (encryptValue is not PdfIndirectReference encryptReference)
        {
            return null;
        }

        var encryptDictionary = ReadDictionaryObject(data, xrefEntries, encryptReference);
        if (!HasName(encryptDictionary, "Filter", "Standard")
            || !HasInteger(encryptDictionary, "V", 2)
            || !HasInteger(encryptDictionary, "R", 3)
            || !HasInteger(encryptDictionary, "Length", 128))
        {
            return null;
        }

        if (encryptDictionary.TryGetValue("CF", out _)
            || encryptDictionary.TryGetValue("EFF", out _)
            || encryptDictionary.TryGetValue("EncryptMetadata", out _)
            || encryptDictionary.TryGetValue("StmF", out _)
            || encryptDictionary.TryGetValue("StrF", out _))
        {
            return null;
        }

        if (!TryGetStringBytes(encryptDictionary, "O", out var ownerBytes)
            || !TryGetStringBytes(encryptDictionary, "U", out var userBytes)
            || ownerBytes.Length != 32
            || userBytes.Length < 16
            || !TryGetPermissions(encryptDictionary, out var permissions)
            || !TryGetDocumentId(trailer, out var documentId))
        {
            return null;
        }

        var keyLengthBytes = 16;
        var fileKey = ComputeFileKey(ownerBytes, permissions, documentId, keyLengthBytes);
        if (!MatchesEmptyUserPassword(userBytes, documentId, fileKey))
        {
            return null;
        }

        return new PdfStandardEncryption(fileKey, encryptReference.ObjectNumber, encryptReference.Generation);
    }

    internal bool AppliesTo(int objectNumber, int generation)
    {
        return objectNumber != _encryptObjectNumber || generation != _encryptGeneration;
    }

    internal PdfValue DecryptObjectValue(PdfValue value, int objectNumber, int generation)
    {
        switch (value)
        {
            case PdfLiteralString literalString:
                return new PdfLiteralString(PdfStringEncoding.GetLiteralString(DecryptBytes(PdfStringEncoding.GetBytes(literalString.Value), objectNumber, generation)));
            case PdfHexString hexString:
                return new PdfHexString(PdfStringEncoding.GetHexString(DecryptBytes(PdfStringEncoding.GetBytes(hexString, "encrypted hex string"), objectNumber, generation)));
            case PdfArray array:
            {
                var decryptedItems = new List<PdfValue>(array.Items.Count);
                foreach (var item in array.Items)
                {
                    decryptedItems.Add(DecryptObjectValue(item, objectNumber, generation));
                }

                return new PdfArray(decryptedItems);
            }
            case PdfDictionary dictionary:
            {
                var decryptedDictionary = new PdfDictionary();
                foreach (var entry in dictionary.Items)
                {
                    decryptedDictionary[entry.Key] = DecryptObjectValue(entry.Value, objectNumber, generation);
                }

                return decryptedDictionary;
            }
            case PdfStream stream:
            {
                var decryptedDictionary = (PdfDictionary)DecryptObjectValue(stream.Dictionary, objectNumber, generation);
                return new PdfStream(decryptedDictionary, DecryptBytes(stream.Data, objectNumber, generation));
            }
            default:
                return value;
        }
    }

    private byte[] DecryptBytes(byte[] encryptedBytes, int objectNumber, int generation)
    {
        if (encryptedBytes.Length == 0)
        {
            return Array.Empty<byte>();
        }

        return ApplyRc4(ComputeObjectKey(objectNumber, generation), encryptedBytes);
    }

    private byte[] ComputeObjectKey(int objectNumber, int generation)
    {
        var seed = new byte[_fileKey.Length + 5];
        Array.Copy(_fileKey, 0, seed, 0, _fileKey.Length);
        seed[_fileKey.Length] = (byte)(objectNumber & 0xFF);
        seed[_fileKey.Length + 1] = (byte)((objectNumber >> 8) & 0xFF);
        seed[_fileKey.Length + 2] = (byte)((objectNumber >> 16) & 0xFF);
        seed[_fileKey.Length + 3] = (byte)(generation & 0xFF);
        seed[_fileKey.Length + 4] = (byte)((generation >> 8) & 0xFF);

        using var md5 = MD5.Create();
        var digest = md5.ComputeHash(seed);
        var keyLength = Math.Min(_fileKey.Length + 5, 16);
        var key = new byte[keyLength];
        Array.Copy(digest, 0, key, 0, keyLength);
        return key;
    }

    private static PdfDictionary ReadDictionaryObject(byte[] data, IDictionary<int, XrefEntry> xrefEntries, PdfIndirectReference reference)
    {
        if (!xrefEntries.TryGetValue(reference.ObjectNumber, out var entry)
            || !entry.InUse
            || entry.Generation != reference.Generation)
        {
            throw new InvalidOperationException($"Encryption dictionary reference {reference.ObjectNumber} {reference.Generation} R was not found.");
        }

        var reader = new PdfReader(data, entry.Offset);
        var objectNumber = reader.ReadInteger();
        var generation = reader.ReadInteger();
        var keyword = reader.ReadKeyword();
        if (objectNumber != reference.ObjectNumber
            || generation != reference.Generation
            || !string.Equals(keyword, "obj", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Indirect object header is malformed.");
        }

        var value = reader.ReadValue();
        reader.SkipWhiteSpaceAndComments();
        if (value is PdfDictionary && reader.PeekKeyword() == "stream")
        {
            throw new NotSupportedException("Encryption dictionary streams are not supported.");
        }

        if (reader.ReadKeyword() != "endobj")
        {
            throw new InvalidOperationException("Encryption dictionary object did not terminate with endobj.");
        }

        if (value is not PdfDictionary dictionary)
        {
            throw new InvalidOperationException("Encryption dictionary object is not a dictionary.");
        }

        return dictionary;
    }

    private static bool HasName(PdfDictionary dictionary, string key, string expectedValue)
    {
        return dictionary.TryGetValue(key, out var value)
               && value is PdfName name
               && string.Equals(name.Value, expectedValue, StringComparison.Ordinal);
    }

    private static bool HasInteger(PdfDictionary dictionary, string key, int expectedValue)
    {
        return TryGetInt32(dictionary, key, out var value) && value == expectedValue;
    }

    private static bool TryGetInt32(PdfDictionary dictionary, string key, out int value)
    {
        value = 0;
        if (!dictionary.TryGetValue(key, out var rawValue)
            || rawValue is not PdfNumber number
            || !number.IsInteger)
        {
            return false;
        }

        try
        {
            value = PdfSecurityLimits.RequireInt32(number, $"encryption key /{key}");
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryGetStringBytes(PdfDictionary dictionary, string key, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (!dictionary.TryGetValue(key, out var rawValue) || rawValue is null)
        {
            return false;
        }

        try
        {
            value = PdfStringEncoding.GetBytes(rawValue, $"encryption key /{key}");
            return true;
        }
        catch (InvalidOperationException)
        {
            value = Array.Empty<byte>();
            return false;
        }
    }

    private static bool TryGetPermissions(PdfDictionary dictionary, out int permissions)
    {
        permissions = 0;
        if (!dictionary.TryGetValue("P", out var rawValue)
            || rawValue is not PdfNumber number
            || !number.IsInteger)
        {
            return false;
        }

        if (int.TryParse(number.RawValue, out permissions))
        {
            return true;
        }

        if (uint.TryParse(number.RawValue, out var unsignedPermissions))
        {
            permissions = unchecked((int)unsignedPermissions);
            return true;
        }

        return false;
    }

    private static bool TryGetDocumentId(PdfDictionary trailer, out byte[] documentId)
    {
        documentId = Array.Empty<byte>();
        if (!trailer.TryGetValue("ID", out var idValue)
            || idValue is not PdfArray idArray
            || idArray.Items.Count == 0)
        {
            return false;
        }

        try
        {
            documentId = PdfStringEncoding.GetBytes(idArray.Items[0], "trailer /ID");
            return true;
        }
        catch (InvalidOperationException)
        {
            documentId = Array.Empty<byte>();
            return false;
        }
    }

    private static byte[] ComputeFileKey(byte[] ownerBytes, int permissions, byte[] documentId, int keyLengthBytes)
    {
        var seed = new byte[32 + ownerBytes.Length + 4 + documentId.Length];
        var position = 0;
        Array.Copy(PasswordPadding, 0, seed, position, 32);
        position += 32;
        Array.Copy(ownerBytes, 0, seed, position, ownerBytes.Length);
        position += ownerBytes.Length;
        WriteInt32LittleEndian(seed, position, permissions);
        position += 4;
        Array.Copy(documentId, 0, seed, position, documentId.Length);

        using var md5 = MD5.Create();
        var digest = md5.ComputeHash(seed);
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var truncated = new byte[keyLengthBytes];
            Array.Copy(digest, 0, truncated, 0, keyLengthBytes);
            digest = md5.ComputeHash(truncated);
        }

        var fileKey = new byte[keyLengthBytes];
        Array.Copy(digest, 0, fileKey, 0, keyLengthBytes);
        return fileKey;
    }

    private static bool MatchesEmptyUserPassword(byte[] userBytes, byte[] documentId, byte[] fileKey)
    {
        using var md5 = MD5.Create();
        var seed = new byte[32 + documentId.Length];
        Array.Copy(PasswordPadding, 0, seed, 0, 32);
        Array.Copy(documentId, 0, seed, 32, documentId.Length);

        var encrypted = md5.ComputeHash(seed);
        for (var iteration = 0; iteration < 20; iteration++)
        {
            encrypted = ApplyRc4(XorKey(fileKey, (byte)iteration), encrypted);
        }

        for (var index = 0; index < 16; index++)
        {
            if (userBytes[index] != encrypted[index])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] XorKey(byte[] key, byte value)
    {
        var result = new byte[key.Length];
        for (var index = 0; index < key.Length; index++)
        {
            result[index] = (byte)(key[index] ^ value);
        }

        return result;
    }

    private static byte[] ApplyRc4(byte[] key, byte[] data)
    {
        var state = new byte[256];
        for (var index = 0; index < state.Length; index++)
        {
            state[index] = (byte)index;
        }

        var j = 0;
        for (var i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            var swap = state[i];
            state[i] = state[j];
            state[j] = swap;
        }

        var output = new byte[data.Length];
        var x = 0;
        var y = 0;
        for (var index = 0; index < data.Length; index++)
        {
            x = (x + 1) & 0xFF;
            y = (y + state[x]) & 0xFF;
            var swap = state[x];
            state[x] = state[y];
            state[y] = swap;
            var keyByte = state[(state[x] + state[y]) & 0xFF];
            output[index] = (byte)(data[index] ^ keyByte);
        }

        return output;
    }

    private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
