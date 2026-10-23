using System.Globalization;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>One rope of an anchor: the id of what it is tied to, and which of that one's tie points.</summary>
/// <param name="Target">The id (<see cref="Keys.Id"/>) of what the rope is tied to.</param>
/// <param name="Point">Which of its tie points (0 for most: a vanilla ship has several).</param>
internal readonly record struct Rope(int Target, byte Point);

/// <summary>
/// A rope anchor's ropes (<see cref="Keys.Ropes"/>), as the mod packs them: a version byte, then each rope. Version 2
/// (today's) names each target by its id, four bytes little-endian, then its tie point, a byte. Versions 0 and 1
/// (older worlds) named it by its object id (eight bytes of user, four of id) instead, which the mod turns into an id
/// when it loads the anchor. Shown in the inspector, and in a pasted copy made to name the copy's objects.
/// </summary>
internal sealed class RopeList : IDataDecoder, ICopyRewriter
{
    /// <summary>The version the mod writes today.</summary>
    public const byte Version = 2;

    /// <summary>The bytes of one rope in today's version, and in the older ones.</summary>
    private const int Size = 5, OldSize = 13;

    /// <summary>
    /// The ropes in a value of today's version; null for an older one, another, or damaged bytes.
    /// </summary>
    public static List<Rope>? Read(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] != Version || (bytes.Length - 1) % Size != 0)
        {
            return null;
        }
        var ropes = new List<Rope>();
        for (int i = 1; i < bytes.Length; i += Size)
        {
            ropes.Add(new Rope(BitConverter.ToInt32(bytes, i), bytes[i + 4]));
        }
        return ropes;
    }

    /// <summary>How many ropes a value of an older version holds; null when it is not one, or damaged.</summary>
    public static int? OldCount(byte[] bytes) =>
        bytes.Length > 0 && bytes[0] <= 1 && (bytes.Length - 1) % OldSize == 0 ? (bytes.Length - 1) / OldSize : null;

    /// <summary>Ropes packed as the mod keeps them today.</summary>
    public static byte[] Write(IReadOnlyList<Rope> ropes)
    {
        byte[] bytes = new byte[1 + (ropes.Count * Size)];
        bytes[0] = Version;
        for (int k = 0; k < ropes.Count; k++)
        {
            Array.Copy(BitConverter.GetBytes(ropes[k].Target), 0, bytes, 1 + (k * Size), 4);
            bytes[1 + (k * Size) + 4] = ropes[k].Point;
        }
        return bytes;
    }

    /// <inheritdoc/>
    public string? Describe(IObjectView obj, byte[] value)
    {
        if (Read(value) is { } ropes)
        {
            return ropes.Count == 0 ? "No rope." : $"{Ropes(ropes.Count)} to: " + string.Join(", ", ropes.Select(Name));
        }
        return OldCount(value) is int old
            ? $"{Ropes(old)}, kept the old way: the game names what they are tied to again when it loads them."
            : null;
    }

    /// <summary>"1 rope", "2 ropes".</summary>
    private static string Ropes(int count) => count == 1 ? "1 rope" : $"{count.ToString(CultureInfo.InvariantCulture)} ropes";

    /// <summary>A rope's target as the inspector names it: its id, and its tie point when not the first.</summary>
    private static string Name(Rope rope) => rope.Point == 0
        ? rope.Target.ToString(CultureInfo.InvariantCulture)
        : string.Format(CultureInfo.InvariantCulture, "{0} (tie point {1})", rope.Target, rope.Point + 1);

    /// <inheritdoc/>
    public void Rewrite(ICopy copy, IIdMap ids)
    {
        if (!copy.TryGetBytes(Keys.Ropes, out byte[] bytes))
        {
            return;
        }
        // Ropes kept the old way name the originals by their object ids, which a copy cannot follow: none instead.
        if (Read(bytes) is not { } ropes)
        {
            copy.SetBytes(Keys.Ropes, null);
            return;
        }
        var kept = new List<Rope>();
        foreach (var rope in ropes)
        {
            // A rope to something not copied goes: the copy's anchor would tie it to the original.
            if (ids.TryGetNew(rope.Target, out int now))
            {
                kept.Add(new Rope(now, rope.Point));
            }
        }
        copy.SetBytes(Keys.Ropes, Write(kept));
    }
}
