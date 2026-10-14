using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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
public static class ModBundles
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
                Console.WriteLine($"mod bundles: could not be copied ({e.Message}).");
            }
            _made = (stamp, reader);
            return reader;
        }
    }

    /// <summary>
    /// The mods' bundles in every plugins folder; when the same one is in several (the game and mod manager profiles),
    /// the newest.
    /// </summary>
    internal static List<Source> Find(AppSettings settings)
    {
        var found = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dir, _) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            if (!Directory.Exists(plugins))
            {
                continue;
            }
            IEnumerable<string> files;
            try
            {
                files = [.. Directory.EnumerateFiles(plugins, "*", SearchOption.AllDirectories)];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (string file in files)
            {
                foreach (var s in In(file))
                {
                    if (!found.TryGetValue(s.Name, out var had) || had.Time < s.Time)
                    {
                        found[s.Name] = s;
                    }
                }
            }
        }
        return [.. found.Values.OrderBy(s => s.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The bundles a file is or holds: a bundle file itself, or a DLL's embedded ones; none for anything else or a file
    /// that cannot be read.
    /// </summary>
    internal static IEnumerable<Source> In(string file)
    {
        var list = new List<Source>();
        try
        {
            var fi = new FileInfo(file);
            if (fi.Length < Magic.Length || fi.Length > MaxFile)
            {
                return list;
            }
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string dll = Path.GetFileNameWithoutExtension(file);
                foreach (string resource in EmbeddedBundles(file))
                {
                    list.Add(new Source(Safe($"{dll}.{resource}"), file, resource, fi.Length, fi.LastWriteTimeUtc.Ticks));
                }
            }
            else if (StartsWithMagic(file))
            {
                list.Add(new Source(Safe(Path.GetFileName(file)), file, null, fi.Length, fi.LastWriteTimeUtc.Ticks));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            // Not a .NET DLL, or not readable: nothing in it.
        }
        return list;
    }

    /// <summary>Whether a file starts as a bundle does.</summary>
    private static bool StartsWithMagic(string file)
    {
        using var fs = File.OpenRead(file);
        Span<byte> head = stackalloc byte[7];
        return fs.ReadAtLeast(head, head.Length, false) == head.Length && head.SequenceEqual(Magic);
    }

    /// <summary>A file name made of the name's letters, digits, dots, dashes and underscores (the rest as _).</summary>
    private static string Safe(string name) => new([.. name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')]);

    /// <summary>The names of a DLL's manifest resources that are bundles.</summary>
    private static List<string> EmbeddedBundles(string dll)
    {
        var names = new List<string>();
        using var pe = new PEReader(File.OpenRead(dll));
        if (!pe.HasMetadata)
        {
            return names;
        }
        var md = pe.GetMetadataReader();
        foreach (var h in md.ManifestResources)
        {
            var r = md.GetManifestResource(h);
            if (r.Implementation.IsNil && Resource(pe, r, Magic.Length) is { } head && head.AsSpan().SequenceEqual(Magic))
            {
                names.Add(md.GetString(r.Name));
            }
        }
        return names;
    }

    /// <summary>
    /// An embedded resource's bytes (the first <paramref name="take"/> of them; -1: all), or null when they are not in
    /// the file.
    /// </summary>
    private static byte[]? Resource(PEReader pe, ManifestResource r, int take = -1)
    {
        var dir = pe.PEHeaders.CorHeader?.ResourcesDirectory;
        if (dir is not { Size: > 0 } d)
        {
            return null;
        }
        var block = pe.GetSectionData(d.RelativeVirtualAddress);
        if (block.Length == 0 || r.Offset < 0 || r.Offset + 4 > d.Size)
        {
            return null;
        }
        var reader = block.GetReader();
        reader.Offset = (int)r.Offset;
        int length = reader.ReadInt32();
        if (length < 0 || r.Offset + 4 + length > d.Size)
        {
            return null;
        }
        return reader.ReadBytes(take < 0 ? length : Math.Min(take, length));
    }

    /// <summary>
    /// Copies the bundles into <see cref="Folder"/> (emptied first), unless the stamp says they are there already.
    /// </summary>
    private static void Copy(List<Source> sources, string stamp)
    {
        bool same = File.Exists(StampFile) && File.ReadAllText(StampFile) == stamp && Directory.Exists(Folder);
        // The same sources as last time: only those that could not be copied then are tried again (a passing
        // error), the rest stays as copied.
        var retry = same && File.Exists(FailedFile) ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FailedFile)) ?? []).ToHashSet(StringComparer.Ordinal) : [];
        if (same && retry.Count == 0)
        {
            return;
        }
        if (!same)
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
            Directory.CreateDirectory(Folder);
        }
        var failed = new List<string>();
        foreach (var s in sources)
        {
            if (same && !retry.Contains(s.Name))
            {
                continue;
            }
            try
            {
                byte[] bytes = s.Resource == null ? File.ReadAllBytes(s.Path) : FromDll(s.Path, s.Resource) ?? [];
                if (bytes.Length > 0)
                {
                    File.WriteAllBytes(Path.Combine(Folder, s.Name), Lz4(bytes));
                }
            }
            // Whatever a damaged or unusual bundle makes the reader throw: that bundle alone is left out (the others
            // still go), else no mod's model would be drawn; it is tried again next time.
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Console.WriteLine($"mod bundles: {s.Name} could not be copied ({e.Message}).");
                failed.Add(s.Name);
            }
        }
        SafeFile.WriteAllText(StampFile, stamp);
        if (failed.Count > 0)
        {
            SafeFile.WriteAllText(FailedFile, JsonSerializer.Serialize(failed));
        }
        else if (File.Exists(FailedFile))
        {
            File.Delete(FailedFile);
        }
        Console.WriteLine($"mod bundles: {sources.Count - failed.Count} copied into {Folder}{(failed.Count > 0 ? $", {failed.Count} not (tried again next time)" : "")}.");
    }

    /// <summary>An embedded resource of a DLL, by name.</summary>
    private static byte[]? FromDll(string dll, string resource)
    {
        using var pe = new PEReader(File.OpenRead(dll));
        var md = pe.GetMetadataReader();
        foreach (var h in md.ManifestResources)
        {
            var r = md.GetManifestResource(h);
            if (r.Implementation.IsNil && md.GetString(r.Name) == resource)
            {
                return Resource(pe, r);
            }
        }
        return null;
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
