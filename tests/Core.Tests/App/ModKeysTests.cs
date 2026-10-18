using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The data names the player's mods keep on objects (ModKeys): found among the string literals of the DLLs in the
/// settings' plugins folders, given to ZdoKeys (the game's own names still first, a ZDOID's "_u" and "_i" too), kept in the
/// data folder until a DLL changes.
/// </summary>
[Collection("DataDir")]
public sealed class ModKeysTests : PretendHomeTestBase
{
    /// <summary>
    /// Forgets the mods' names given to ZdoKeys, before the environment is put back.
    /// </summary>
    protected override void ForgetMods() => ZdoKeys.UseMods(new Dictionary<int, string>());

    /// <summary>The kept file.</summary>
    private static string Kept => Path.Combine(AppSettings.UserDataDir, "mod-zdo-keys.json");

    [Fact]
    public void TheModsDataNamesNameTheirKeys()
    {
        string plugins = Plugins("profile");
        Directory.CreateDirectory(Path.Combine(plugins, "zolantris-ValheimRAFT"));
        TestDll.Write(Path.Combine(plugins, "zolantris-ValheimRAFT", "ValheimVehicles.dll"), strings: ["MBParentId", "MBPosition", "not a key!", "creator"]);
        TestDll.Write(Path.Combine(plugins, "OtherMod.dll"), strings: ["OM_Level"]);

        int names = ModKeys.Refresh(Settings("profile"));

        Assert.Equal(12, names);
        Assert.Equal("MBParentId", ZdoKeys.NameOf(StableHash.Of("MBParentId")));
        Assert.Equal("MBParentId_u", ZdoKeys.NameOf(StableHash.Of("MBParentId_u")));
        Assert.Equal("MBPosition_i", ZdoKeys.NameOf(StableHash.Of("MBPosition_i")));
        Assert.Equal("OM_Level", ZdoKeys.NameOf(StableHash.Of("OM_Level")));
        Assert.Null(ZdoKeys.NameOf(StableHash.Of("not a key!")));
    }

    [Fact]
    public void TheGamesOwnNameComesFirst()
    {
        int creator = StableHash.Of("creator");
        string? game = ZdoKeys.NameOf(creator);
        Assert.NotNull(game);

        ZdoKeys.UseMods(new Dictionary<int, string> { [creator] = "a mod's name" });

        Assert.Equal(game, ZdoKeys.NameOf(creator));
    }

    [Fact]
    public void FilesThatAreNotDllsOfCodeGiveNoNames()
    {
        string plugins = Plugins("profile");
        File.WriteAllText(Path.Combine(plugins, "Broken.dll"), "MBParentId");
        File.WriteAllText(Path.Combine(plugins, "readme.txt"), "MBParentId");

        Assert.Equal(0, ModKeys.Refresh(Settings("profile")));
        Assert.Equal(0, ModKeys.Refresh(Settings("missing")));
        Assert.Null(ZdoKeys.NameOf(StableHash.Of("MBParentId")));
    }

    [Fact]
    public void TheNamesAreKeptWhileTheDllsAreUnchanged()
    {
        string plugins = Plugins("profile");
        string dll = Path.Combine(plugins, "Mod.dll");
        TestDll.Write(dll, strings: ["MBParentId"]);
        ModKeys.Refresh(Settings("profile"));
        // What is kept is used: a name changed in the file shows.
        var kept = JsonNode.Parse(File.ReadAllText(Kept))!;
        kept["Names"]![StableHash.Of("MBParentId").ToString(System.Globalization.CultureInfo.InvariantCulture)] = "from the kept file";
        File.WriteAllText(Kept, kept.ToJsonString());

        ModKeys.Refresh(Settings("profile"));
        Assert.Equal("from the kept file", ZdoKeys.NameOf(StableHash.Of("MBParentId")));

        // A DLL changed: read again.
        File.SetLastWriteTimeUtc(dll, DateTime.UtcNow.AddMinutes(1));
        ModKeys.Refresh(Settings("profile"));
        Assert.Equal("MBParentId", ZdoKeys.NameOf(StableHash.Of("MBParentId")));

        // A broken kept file: read again.
        File.WriteAllText(Kept, "{ broken");
        Assert.Equal(3, ModKeys.Refresh(Settings("profile")));
    }

    [Fact]
    public void AKeptFileThatCannotBeWrittenGivesNoNames()
    {
        TestDll.Write(Path.Combine(Plugins("profile"), "Mod.dll"), strings: ["MBParentId"]);
        // A folder where the kept file goes: nothing can be written there, so no names are in use.
        Directory.CreateDirectory(Kept);
        Assert.Equal(0, ModKeys.Refresh(Settings("profile")));
    }

    [Fact]
    public void ABrokenLinkInAPluginsFolderIsPassedOver()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a symbolic link needs rights on Windows");
        string plugins = Plugins("profile");
        // A link to a file that is not there: its size cannot be read, so the folder's DLLs are not read.
        File.CreateSymbolicLink(Path.Combine(plugins, "Broken.dll"), Path.Combine(plugins, "gone.dll"));
        Assert.Equal(0, ModKeys.Refresh(Settings("profile")));
    }
}
