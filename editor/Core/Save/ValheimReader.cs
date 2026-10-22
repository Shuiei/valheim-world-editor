namespace TerrainEditor.Save;

/// <summary>
/// Mirrors the read side of Valheim's ZPackage (little-endian BinaryReader plus a few custom encodings).
/// </summary>
/// <param name="stream">The data to read; disposing the reader closes it.</param>
public sealed partial class ValheimReader(Stream stream) : IDisposable
{
    private readonly BinaryReader _reader = new(stream);

    /// <summary>The read position in the underlying stream, in bytes.</summary>
    public long Position => _reader.BaseStream.Position;

    /// <inheritdoc/>
    public void Dispose() => _reader.Dispose();
}

/// <summary>
/// Lengths and counts read from a file, checked before they are used (see the other part of this class).
/// </summary>
public static partial class CheckedReads
{
    /// <summary>
    /// A count in the game's compact form (ZPackage.ReadNumItems): one byte below 128, else two bytes, the first with its
    /// high bit set.
    /// </summary>
    public static int ReadNumItems(this BinaryReader r)
    {
        int n = r.ReadByte();
        return (n & 0x80) != 0 ? ((n & 0x7F) << 8) | r.ReadByte() : n;
    }
}

/// <summary>World save format versions at which the encoding changed.</summary>
public static class WorldVersion
{
    /// <summary>From this version counts read by ReadNumItems can take the two-byte form.</summary>
    public const int NumItems = 33;

    /// <summary>
    /// From this version the world is saved in chunk files, and objects no longer carry their sector and store their
    /// rotation in the small form.
    /// </summary>
    public const int ChunkedSave = 40;

    /// <summary>The oldest save format the writer changes (the Deep North save as the game wrote it until 1.0.17).</summary>
    public const int OldestWritable = 41;

    /// <summary>
    /// The save format the game writes since Valheim 1.0.20, and the newest the editor reads and writes. It differs
    /// from 41 only by the number in each file's head: the objects, the zone list and the metadata are laid out the same.
    /// </summary>
    public const int Latest = 42;

    /// <summary>Whether the writer can change a file in this save format.</summary>
    public static bool Writable(int version) => version is >= OldestWritable and <= Latest;
}

/// <summary>The game's string hashing, for prefab names and data keys.</summary>
public static class StableHash
{
    /// <summary>Valheim's StringExtensionMethods.GetStableHashCode, used for prefab and data keys.</summary>
    public static int Of(string str) => ValheimWorldEditor.Sdk.GameHash.Of(str);

    /// <summary>
    /// The prefab an object's name in a copy or blueprint stands for: a kind with no known name (from a mod, or not in
    /// the catalogue) is written as its hash, a number, which stands for itself.
    /// </summary>
    public static int OfName(string name) =>
        int.TryParse(name, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int hash) ? hash : Of(name);
}
