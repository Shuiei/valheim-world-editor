using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// Data kept out of a bundle's objects, in its resource file.
/// </summary>
public static partial class GameCatalogues
{
    internal sealed partial class Bundle
    {
        /// <summary>
        /// Data kept out of the object, in a resource file of the bundle (.resS): meshes. Empty when the bundle has no
        /// such resource file, or the range named does not lie inside it.
        /// </summary>
        public byte[] StreamBytes(AssetTypeValueField streamData)
        {
            int idx = _bundle.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
            if (idx < 0)
            {
                return [];
            }
            _bundle.file.GetFileRange(idx, out long start, out long length);
            long offset = streamData["offset"].AsLong, size = streamData["size"].AsUInt;
            if (!GameBundles.InResource(length, offset, size))
            {
                Console.WriteLine($"game files: a mesh names {size} bytes at {offset} of a {length}-byte resource file; not read.");
                return [];
            }
            _bundle.file.DataReader.Position = start + offset;
            return _bundle.file.DataReader.ReadBytes((int)size);
        }
    }
}
