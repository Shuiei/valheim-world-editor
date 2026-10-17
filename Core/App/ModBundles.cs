using System.Text.Json;
using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// The asset bundles of the player's mods, where the mods' own meshes, materials and textures are: found in the
/// BepInEx plugins folders the editor knows (the game's, the mod managers' profiles: LocalGame.BepInExFolders), as
/// files of their own or, as most mods ship them, embedded in their DLLs (manifest resources starting "UnityFS").
/// They are copied into the data folder (mod-bundles), once until the mods change, an LZMA bundle unpacked and packed
/// again as LZ4 so it reads in place like the game's; GameBundles reads them there. Nothing of a mod is run: its DLL is
/// only read as bytes.
/// </summary>
public static partial class ModBundles
{
    /// <summary>Where the bundles are copied.</summary>
    public static string Folder => Path.Combine(AppSettings.UserDataDir, "mod-bundles");

    /// <summary>What the copies were made from (their sources' paths, sizes and times): copied again when it changes.</summary>
    private static string StampFile => Path.Combine(AppSettings.UserDataDir, "mod-bundles.json");

    /// <summary>The names of the sources that could not be copied last time (JSON), to try again next time.</summary>
    private static string FailedFile => Path.Combine(AppSettings.UserDataDir, "mod-bundles-failed.json");

    /// <summary>The bundles' index, kept as the game's is (GameBundles).</summary>
    private static string IndexFile => Path.Combine(AppSettings.UserDataDir, "mod-bundles-index.json");

    /// <summary>A bundle's first bytes.</summary>
    private static readonly byte[] Magic = "UnityFS"u8.ToArray();

    /// <summary>A file larger than this is not looked into (no mod's DLL is near it).</summary>
    private const long MaxFile = 512L << 20;

    /// <summary>Serialises finding and copying.</summary>
    private static readonly Lock Lock = new();

    /// <summary>The reader made last, and the stamp of what it reads.</summary>
    private static (string Stamp, GameBundles? Reader) _made = ("", null);

    /// <summary>
    /// One bundle of a mod.
    /// </summary>
    /// <param name="Name">The copy's file name: the DLL's name and the resource's, or the bundle file's own.</param>
    /// <param name="Path">The DLL or bundle file it comes from.</param>
    /// <param name="Resource">The manifest resource it is in the DLL; null for a bundle file.</param>
    /// <param name="Length">The source file's size.</param>
    /// <param name="Time">The source file's last write (UTC ticks).</param>
    internal sealed record Source(string Name, string Path, string? Resource, long Length, long Time);

    /// <summary>
    /// A reader of the mods' bundles, copied first when the mods changed since; null when the player has no mod with
    /// bundles (or none of them can be read).
    /// </summary>
    public static GameBundles? Reader(AppSettings settings)
    {
        lock (Lock)
        {
            var sources = Find(settings);
            string stamp = JsonSerializer.Serialize(sources);
            if (_made.Stamp == stamp)
            {
                return _made.Reader;
            }
            // The mods changed: the reader of the last copies lets go of them first, else deleting the folder is
            // refused on Windows (its bundle files are open) and the mods' models stay missing for the session.
            _made.Reader?.Close();
            _made = ("", null);
            GameBundles? reader = null;
            try
            {
                if (sources.Count > 0)
                {
                    Copy(sources, stamp);
                    // Mods are built with different Unity versions (2019 to 6000 among those tried).
                    reader = new GameBundles(Folder, IndexFile, mixedVersions: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidDataException or NotSupportedException or NotImplementedException)
            {
                Log.Caught(e, "copying the mods' bundles (no mod's model is drawn)");
            }
            _made = (stamp, reader);
            return reader;
        }
    }

    /// <summary>
    /// A bundle as GameBundles reads it in place: an LZMA one (read only whole) unpacked and packed again with LZ4
    /// blocks; any other as it is.
    /// </summary>
    internal static byte[] Lz4(byte[] bundle)
    {
        var file = new AssetBundleFile();
        using var reader = new AssetsFileReader(new MemoryStream(bundle));
        file.Read(reader);
        if (file.GetCompressionType() != AssetBundleCompressionType.LZMA)
        {
            file.Close();
            return bundle;
        }
        using var unpacked = new MemoryStream();
        file.Unpack(new AssetsFileWriter(unpacked));
        file.Close();
        unpacked.Position = 0;
        var plain = new AssetBundleFile();
        plain.Read(new AssetsFileReader(unpacked));
        using var packed = new MemoryStream();
        plain.Pack(new AssetsFileWriter(packed), AssetBundleCompressionType.LZ4);
        plain.Close();
        return packed.ToArray();
    }
}
