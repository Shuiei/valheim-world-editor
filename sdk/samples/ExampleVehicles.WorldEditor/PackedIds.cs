using System;
using System.Collections.Generic;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// A list of ids the mod packs into one byte-array value (Keys.Packed): a version byte (1), then each id as four
/// bytes (little-endian). Shown in the inspector, and in a pasted copy made to name the copy's objects (an id of
/// an object that was not copied goes). The editor cannot rewrite packed ids by itself: this is what the hook is for.
/// </summary>
public sealed class PackedIds : IDataDecoder, ICopyRewriter
{
    /// <summary>The list's version this reads.</summary>
    public const byte Version = 1;

    /// <summary>The ids in a value; null for another version or damaged bytes.</summary>
    public static List<int>? Read(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] != Version || (bytes.Length - 1) % 4 != 0)
        {
            return null;
        }
        var ids = new List<int>();
        for (int i = 1; i < bytes.Length; i += 4)
        {
            ids.Add(BitConverter.ToInt32(bytes, i));
        }
        return ids;
    }

    /// <summary>Ids packed as the mod keeps them.</summary>
    public static byte[] Write(IReadOnlyList<int> ids)
    {
        byte[] bytes = new byte[1 + (ids.Count * 4)];
        bytes[0] = Version;
        for (int k = 0; k < ids.Count; k++)
        {
            Array.Copy(BitConverter.GetBytes(ids[k]), 0, bytes, 1 + (k * 4), 4);
        }
        return bytes;
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => Read(value) switch
    {
        null => null,
        { Count: 0 } => "Linked to nothing.",
        var ids => $"Linked to {ids.Count} object(s): {string.Join(", ", ids)}",
    };

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        if (!copy.TryGetBytes(Keys.Packed, out byte[] bytes))
        {
            return;
        }
        // An unknown list (older, damaged) would tie the copy to the originals: none instead.
        if (Read(bytes) is not { } old)
        {
            copy.SetBytes(Keys.Packed, null);
            return;
        }
        var kept = new List<int>();
        foreach (int id in old)
        {
            if (ids.TryGetNew(id, out int now))
            {
                kept.Add(now);
            }
        }
        copy.SetBytes(Keys.Packed, Write(kept));
    }
}
