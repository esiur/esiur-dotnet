#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using Esiur.Resource;

namespace Esiur.Data.Types;

/// <summary>
/// Computes the id-independent signature of a type definition (docs/typedef-sync.md).
/// </summary>
/// <remarks>
/// The signature is the first 8 bytes of SHA-256 over the definition's normal wire encoding,
/// with every id replaced by a hash of the name it stands for. It is a change detector: a node
/// computes it for its own definitions, a client stores and echoes what the node sent.
/// </remarks>
internal static class TypeDefSignature
{
    [ThreadStatic] private static int _depth;

    /// <summary>True while a signature is being composed on this thread.</summary>
    internal static bool IsComputing => _depth > 0;

    /// <summary>First 8 bytes of SHA-256(UTF-8(name)), little endian.</summary>
    internal static ulong Name64(string name)
    {
        return ReadUInt64(Sha256(Encoding.UTF8.GetBytes(name)));
    }

    internal static ulong Compute(TypeDef definition)
    {
        var info = TypeDefInfo.FromTypeDef(definition);
        info.Id = 0;
        info.Signature = null;
        if (info.Parent != null)
        {
            var parentName = (definition as LocalTypeDef)?.ParentTypeDef?.Name;
            // A definition whose parent cannot be named keeps no parent in the hash rather than an id.
            info.Parent = parentName == null ? null : Name64(parentName);
        }

        byte[] bytes;
        _depth++;
        try
        {
            bytes = Codec.Compose(info, Warehouse.Default, null!);
        }
        finally
        {
            _depth--;
        }

        var signature = ReadUInt64(Sha256(bytes));
        // Zero is reserved for "unknown".
        return signature == 0 ? 1 : signature;
    }

    private static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    // Explicit little endian so the value does not depend on the platform.
    private static ulong ReadUInt64(byte[] hash)
    {
        ulong value = 0;
        for (var i = 7; i >= 0; i--)
            value = (value << 8) | hash[i];
        return value;
    }
}
