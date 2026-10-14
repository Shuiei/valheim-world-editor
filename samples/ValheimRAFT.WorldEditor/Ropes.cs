using System;
using System.Collections.Generic;
using System.Linq;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// A rope anchor's ropes ("MBRopeAnchor_Ropes"): a version byte (2), then for each rope the PersistentID of the anchor
/// it goes to (an int, little-endian) and that anchor's attachment point (a byte). Shown in the inspector, and in a
/// pasted copy made to name the copy's anchors (a rope to an anchor that was not copied goes).
/// </summary>
public sealed class Ropes : IDataDecoder, ICopyRewriter
{
    /// <summary>The data key.</summary>
    public const string Key = "MBRopeAnchor_Ropes";

    /// <summary>The list's version that names anchors by PersistentID (older ones name them by ZDOID).</summary>
    public const byte Version = 2;

    /// <summary>The ropes in a value; null for another version or damaged bytes.</summary>
    public static List<(int Anchor, byte Point)>? Read(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] != Version || (bytes.Length - 1) % 5 != 0)
        {
            return null;
        }
        var ropes = new List<(int, byte)>();
        for (int i = 1; i < bytes.Length; i += 5)
        {
            ropes.Add((BitConverter.ToInt32(bytes, i), bytes[i + 4]));
        }
        return ropes;
    }

    /// <summary>Ropes as the mod keeps them.</summary>
    public static byte[] Write(IReadOnlyList<(int Anchor, byte Point)> ropes)
    {
        byte[] bytes = new byte[1 + (ropes.Count * 5)];
        bytes[0] = Version;
        for (int k = 0; k < ropes.Count; k++)
        {
            Array.Copy(BitConverter.GetBytes(ropes[k].Anchor), 0, bytes, 1 + (k * 5), 4);
            bytes[1 + (k * 5) + 4] = ropes[k].Point;
        }
        return bytes;
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value) => Read(value) switch
    {
        null => null,
        { Count: 0 } => "No ropes.",
        var ropes => $"{ropes.Count} rope(s): " + string.Join(", ", ropes.Select(r => $"to anchor {r.Anchor} (point {r.Point})")),
    };

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        if (!copy.TryGetBytes(Key, out byte[] bytes))
        {
            return;
        }
        // Ropes to the copy's anchors; an unknown list (older, damaged) would tie the copy to the originals: none.
        if (Read(bytes) is not { } ropes)
        {
            copy.SetBytes(Key, null);
            return;
        }
        var kept = new List<(int Anchor, byte Point)>();
        foreach (var (anchor, point) in ropes)
        {
            if (ids.TryGetNew(anchor, out int now))
            {
                kept.Add((now, point));
            }
        }
        copy.SetBytes(Key, Write(kept));
    }
}
