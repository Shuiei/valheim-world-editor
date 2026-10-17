using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's and the mod managers' BepInEx folders and their plugin, and whether the editor knows a mod manager installed it.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderBepInExTests : HomeFolderTestBase
{
    private static readonly string[] Expected1 = ["flat", "game", "mine", "r2", "ts"];
    [Fact]
    public void TheGameAndModManagerProfilesAreSearched()
    {
        LocalGame.SearchDefaultPlaces = true;
        // A Valheim folder (it must look like the game's), and profiles of r2modman (native and Flatpak)
        // and the Thunderstore Mod Manager.
        string valheim = FakeValheim();
        Plugin(Path.Combine(valheim, "BepInEx"), 5301, "game");
        Plugin(Local(".config", "r2modmanPlus-local", "Valheim", "profiles", "Default", "BepInEx"), 5302, "r2");
        Plugin(Local(".var", "app", "com.github.ebkr.r2modman", "config", "r2modmanPlus-local", "Valheim", "profiles", "Flat", "BepInEx"), 5303, "flat");
        Plugin(Local(".config", "Thunderstore Mod Manager", "DataFolder", "Valheim", "profiles", "Tsmm", "BepInEx"), 5304, "ts");
        // A BepInEx folder given in Settings, named BepInEx itself.
        Plugin(Local("custom", "BepInEx"), 5305, "mine");
        var settings = new AppSettings { ValheimPath = valheim, BepInExFolders = { Local("custom", "BepInEx") } };
        var folders = LocalGame.BepInExFolders(settings).ToList();
        Assert.Contains(folders, f => f.Where == "Valheim folder");
        Assert.Contains(folders, f => f.Where == "your folder");
        // r2modman's folder is reached twice on Linux (application data is ~/.config): listed once.
        Assert.Single(folders, f => f.Where == "r2modman profile \"Default\"");
        Assert.Contains(folders, f => f.Where == "r2modman profile \"Flat\"");
        Assert.Contains(folders, f => f.Where == "Thunderstore Mod Manager profile \"Tsmm\"");
        var bridges = LocalGame.FindBridges(settings, out bool bepInEx, out bool plugin);
        Assert.True(bepInEx && plugin);
        Assert.Equal(Expected1, bridges.Select(b => b.Token).Order().ToArray());
    }

    // A mod's folder in plugins that cannot be read is passed over in the search for the plugin (the whole check threw).
    [Fact]
    public void AnUnreadableFolderInPluginsIsPassedOver()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string bep = Local("profile", "BepInEx");
        string locked = Path.Combine(bep, "plugins", "Locked");
        Directory.CreateDirectory(locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.Empty(LocalGame.FindBridges(new AppSettings { BepInExFolders = { bep } }, out bool bepInEx, out bool plugin));
            Assert.True(bepInEx);
            Assert.False(plugin);
        }
        finally
        {
            RestoreAccess(locked);
        }
    }

    [Fact]
    public void AFolderListedThatDoesNotExistIsPassedOver()
    {
        LocalGame.SearchDefaultPlaces = true;
        var settings = new AppSettings { ValheimPath = FakeValheim(), BepInExFolders = { Local("nowhere") } };
        Assert.Empty(LocalGame.FindBridges(settings, out bool bepInEx, out bool plugin));
        Assert.False(bepInEx || plugin);
    }
    [Fact]
    public void TheEditorKnowsWhenAModManagerInstalledIt()
    {
        Assert.Equal(Local("p", "BepInEx"), LocalGame.InstalledIn(Local("p", "BepInEx", "plugins", "Tie-ValheimWorldEditor")));
        Assert.Null(LocalGame.InstalledIn(Local("plugins", "x")));
    }
}
