using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Data kept out of a bundle's objects, in its resource file.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Data kept out of the object, in a resource file of the same bundle (.resS): meshes, textures. Empty when there is
    /// no such file or the range named does not lie inside it.
    /// </summary>
    /// <param name="bundle">The bundle the object is in.</param>
    /// <param name="streamData">The object's m_StreamData field (offset, size, path).</param>
    /// <param name="skip">Bytes to skip after the data's offset.</param>
    /// <param name="take">How many bytes to read; -1: the rest of the data.</param>
    private byte[] StreamBytes(string bundle, AssetTypeValueField streamData, long skip = 0, long take = -1)
    {
        long offset = streamData["offset"].AsLong + skip;
        long size = take >= 0 ? take : streamData["size"].AsUInt - skip;
        var (b, _) = Open(bundle);
        int idx = b.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
        if (idx < 0)
        {
            return [];
        }
        b.file.GetFileRange(idx, out long start, out long length);
        if (!InResource(length, offset, size))
        {
            Console.WriteLine($"game files: {bundle} names {size} bytes at {offset} of a {length}-byte resource file; not read.");
            return [];
        }
        lock (b.file.DataReader)
        {
            b.file.DataReader.Position = start + offset;
            return b.file.DataReader.ReadBytes((int)size);
        }
    }

    /// <summary>
    /// Whether <paramref name="size"/> bytes at <paramref name="offset"/> lie inside a resource file of
    /// <paramref name="length"/> bytes, and fit in one array: sizes from a broken file can point anywhere, even into the
    /// next file.
    /// </summary>
    internal static bool InResource(long length, long offset, long size) =>
        offset >= 0 && size >= 0 && size <= Array.MaxLength && offset <= length - size;
}
