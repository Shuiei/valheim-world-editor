using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' asset bundles (ModBundles) found in the BepInEx plugins folders of the settings, as files of their own or
/// embedded in a mod's DLL, copied into the data folder (the newest of the same one in several folders) and read from
/// there; copied again only when the mods change; an LZMA bundle packed again as LZ4. Runs alone, without the player's own
/// Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ModBundlesTests : PretendHomeTestBase
{
    /// <summary>
    /// A bundle (LZ4 unless stored) with one mesh of this name.
    /// </summary>
    private static byte[] Bundle(string name, string mesh, bool lz4 = true)
    {
        var b = new TestBundle(name);
        new TestMesh { Name = mesh, Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]] }.Add(b);
        return b.ToBytes(lz4);
    }

    /// <summary>
    /// A bundle's compression, as AssetsTools reads it.
    /// </summary>
    private static AssetBundleCompressionType CompressionOf(byte[] bundle)
    {
        var file = new AssetBundleFile();
        file.Read(new AssetsFileReader(new MemoryStream(bundle)));
        var type = file.GetCompressionType();
        file.Close();
        return type;
    }

    [Fact]
    public void BundleFilesAndBundlesInDllsAreCopiedAndRead()
    {
        string plugins = Plugins("Default");
        byte[] own = Bundle("finebundle", "fine_plank");
        byte[] loose = Bundle("loose", "loose_beam");
        TestDll.Write(Path.Combine(plugins, "FineMod.dll"), [("FineMod.Assets.finebundle", own), ("FineMod.Assets.icons.txt", "not a bundle"u8.ToArray())]);
        Directory.CreateDirectory(Path.Combine(plugins, "Other", "assets"));
        File.WriteAllBytes(Path.Combine(plugins, "Other", "assets", "loose bundle"), loose);

        var reader = ModBundles.Reader(Settings("Default"))!;

        Assert.Equal(ModBundles.Folder, reader.Folder);
        Assert.StartsWith(Home, ModBundles.Folder, StringComparison.Ordinal);
        Assert.Equal(["FineMod.FineMod.Assets.finebundle", "loose_bundle"], Directory.GetFiles(ModBundles.Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(own, File.ReadAllBytes(Path.Combine(ModBundles.Folder, "FineMod.FineMod.Assets.finebundle")));
        Assert.Equal(loose, File.ReadAllBytes(Path.Combine(ModBundles.Folder, "loose_bundle")));
        Assert.Single(reader.Named(AssetClassID.Mesh, "fine_plank"));
        Assert.Single(reader.Named(AssetClassID.Mesh, "loose_beam"));
    }

    [Fact]
    public void WithoutBundlesThereIsNoReader()
    {
        string plugins = Plugins("Plain");
        // A mod with no bundle in it, a DLL that is not .NET, files that are not bundles.
        TestDll.Write(Path.Combine(plugins, "PlainMod.dll"), [("PlainMod.readme.txt", "UnityFX and more"u8.ToArray())]);
        File.WriteAllBytes(Path.Combine(plugins, "native.dll"), [.. "UnityFS"u8, .. new byte[64]]);
        File.WriteAllText(Path.Combine(plugins, "notes.txt"), "UnityF");
        File.WriteAllText(Path.Combine(plugins, "config.json"), "{\"bundle\": \"UnityFS\"}");

        Assert.Empty(ModBundles.Find(Settings("Plain")));
        Assert.Null(ModBundles.Reader(Settings("Plain")));
        // No plugins folder at all.
        Directory.CreateDirectory(Path.Combine(Home, "Empty", "BepInEx"));
        Assert.Null(ModBundles.Reader(Settings("Empty")));
    }

    [Fact]
    public void TheNewestOfTheSameModInSeveralFoldersIsCopied()
    {
        string older = Path.Combine(Plugins("Old"), "FineMod.dll");
        string newer = Path.Combine(Plugins("New"), "FineMod.dll");
        byte[] before = Bundle("finebundle", "old_plank");
        byte[] after = Bundle("finebundle", "new_plank");
        TestDll.Write(older, [("FineMod.finebundle", before)]);
        TestDll.Write(newer, [("FineMod.finebundle", after)]);
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-10));

        // Whichever folder comes first.
        foreach (var settings in (AppSettings[])[Settings("Old", "New"), Settings("New", "Old")])
        {
            var source = Assert.Single(ModBundles.Find(settings));
            Assert.Equal(newer, source.Path);
        }
        var reader = ModBundles.Reader(Settings("Old", "New"))!;
        Assert.Equal(after, File.ReadAllBytes(Path.Combine(ModBundles.Folder, "FineMod.FineMod.finebundle")));
        Assert.Empty(reader.Named(AssetClassID.Mesh, "old_plank"));
    }

    // Different bundles whose copies would have had the same name (punctuation made "_", letter case ignored) are both
    // copied and read (the older was dropped, its models never drawn).
    [Fact]
    public void BundlesWhoseNamesWouldMeetAreEachCopied()
    {
        string plugins = Plugins("Default");
        Directory.CreateDirectory(Path.Combine(plugins, "One"));
        Directory.CreateDirectory(Path.Combine(plugins, "Two"));
        Directory.CreateDirectory(Path.Combine(plugins, "Three"));
        File.WriteAllBytes(Path.Combine(plugins, "One", "Mod A.bundle"), Bundle("a", "space_plank"));
        File.WriteAllBytes(Path.Combine(plugins, "Two", "Mod_A.bundle"), Bundle("b", "underscore_plank"));
        File.WriteAllBytes(Path.Combine(plugins, "Three", "mod_a.bundle"), Bundle("c", "lower_plank"));

        var sources = ModBundles.Find(Settings("Default"));
        var reader = ModBundles.Reader(Settings("Default"))!;

        Assert.Equal(3, sources.Select(s => s.Name.ToLowerInvariant()).Distinct().Count());
        Assert.All(sources, s => Assert.StartsWith("Mod_A.bundle-", s.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Single(reader.Named(AssetClassID.Mesh, "space_plank"));
        Assert.Single(reader.Named(AssetClassID.Mesh, "underscore_plank"));
        Assert.Single(reader.Named(AssetClassID.Mesh, "lower_plank"));
    }

    [Fact]
    public void TheCopiesAreKeptWhileTheModsAreUnchanged()
    {
        string dll = Path.Combine(Plugins("Default"), "FineMod.dll");
        TestDll.Write(dll, [("FineMod.finebundle", Bundle("finebundle", "fine_plank"))]);
        var settings = Settings("Default");
        var first = ModBundles.Reader(settings)!;
        string marker = Path.Combine(ModBundles.Folder, "marker");
        File.WriteAllText(marker, "left by the test");
        Assert.Equal(3, first.VertexCountOf(Assert.Single(first.Named(AssetClassID.Mesh, "fine_plank"))));
        Assert.Equal(1, first.OpenBundles);

        // Asked again: the same reader.
        Assert.Same(first, ModBundles.Reader(settings));
        // After another set of mods (none here, so nothing copied), the kept copies are read again, not copied; the
        // reader of before let go of its files first (Windows refuses to delete a folder with an open file).
        Assert.Null(ModBundles.Reader(Settings()));
        Assert.Equal(0, first.OpenBundles);
        var again = ModBundles.Reader(settings)!;
        Assert.NotSame(first, again);
        Assert.True(File.Exists(marker));
        Assert.True(File.Exists(Path.Combine(AppSettings.UserDataDir, "mod-bundles.json")));
        // A mod changed while a reader holds its copy open: copied again, the folder emptied first, and the new
        // copy's models read.
        Assert.Equal(3, again.VertexCountOf(Assert.Single(again.Named(AssetClassID.Mesh, "fine_plank"))));
        TestDll.Write(dll, [("FineMod.finebundle", Bundle("finebundle", "new_plank"))]);
        File.SetLastWriteTimeUtc(dll, DateTime.UtcNow.AddDays(-1));
        var changed = ModBundles.Reader(settings)!;
        Assert.False(File.Exists(marker));
        Assert.True(File.Exists(Path.Combine(ModBundles.Folder, "FineMod.FineMod.finebundle")));
        Assert.Equal(3, changed.VertexCountOf(Assert.Single(changed.Named(AssetClassID.Mesh, "new_plank"))));
        Assert.Empty(changed.Named(AssetClassID.Mesh, "fine_plank"));
    }

    [Fact]
    public void AnLzmaBundleIsPackedAgainAsLz4()
    {
        byte[] stored = Bundle("lzma", "packed_plank", lz4: false);
        var file = new AssetBundleFile();
        file.Read(new AssetsFileReader(new MemoryStream(stored)));
        using var packed = new MemoryStream();
        file.Pack(new AssetsFileWriter(packed), AssetBundleCompressionType.LZMA);
        file.Close();
        byte[] lzma = packed.ToArray();
        Assert.Equal(AssetBundleCompressionType.LZMA, CompressionOf(lzma));

        byte[] lz4 = ModBundles.Lz4(lzma);

        Assert.Equal(AssetBundleCompressionType.LZ4, CompressionOf(lz4));
        // It reads as the same bundle.
        string dir = Path.Combine(Home, "bundles");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "lzma"), lz4);
        var bundles = new GameBundles(dir, null);
        var mesh = Assert.Single(bundles.Named(AssetClassID.Mesh, "packed_plank"));
        Assert.Equal(3, bundles.VertexCountOf(mesh));
    }

    [Fact]
    public void AnLz4OrStoredBundleIsLeftAsItIs()
    {
        byte[] lz4 = Bundle("lz4", "plank");
        byte[] stored = Bundle("stored", "plank", lz4: false);

        Assert.Same(lz4, ModBundles.Lz4(lz4));
        Assert.Same(stored, ModBundles.Lz4(stored));
    }
}
