using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The worlds folder the installed game saves to (Places.GameWorldRoot), and the folders offered for a new world: a
/// Windows build run through Proton saves inside its Steam library's prefix, the native build under ~/.config.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderGameWorldRootTests : HomeFolderTestBase
{
    /// <summary>A Valheim folder in a Steam library under the pretend home, as a Windows build (valheim.exe) or a Linux one.</summary>
    private string Valheim(bool windows, bool linux = false)
    {
        string valheim = Local(".local", "share", "Steam", "steamapps", "common", "Valheim");
        Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
        if (windows)
        {
            File.WriteAllText(Path.Combine(valheim, "valheim.exe"), "");
        }
        if (linux)
        {
            File.WriteAllText(Path.Combine(valheim, "valheim.x86_64"), "");
        }
        return valheim;
    }

    private string Prefix => Local(".local", "share", "Steam", "steamapps", "compatdata", "892970", "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");

    private string Native => Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local");

    [Fact]
    public void AWindowsBuildThroughProtonSavesInsideItsPrefix()
    {
        if (!LinuxLayout)
        {
            return;
        }
        Assert.Equal(Prefix, Places.GameWorldRoot(Valheim(windows: true)));
        // The folder need not exist yet: the game makes it on its first save.
        Assert.False(Directory.Exists(Prefix));
    }

    [Fact]
    public void TheNativeBuildAndNoGameSaveUnderTheHome()
    {
        if (!LinuxLayout)
        {
            return;
        }
        Assert.Equal(Native, Places.GameWorldRoot(Valheim(windows: false, linux: true)));
        Assert.Equal(Native, Places.GameWorldRoot((string?)null));
        // Both builds in the folder: the native one runs.
        Assert.Equal(Native, Places.GameWorldRoot(Valheim(windows: true, linux: true)));
    }

    [Fact]
    public void AGameReachedThroughALinkOrKeptOutsideALibraryStillFindsItsPrefix()
    {
        if (!LinuxLayout)
        {
            return;
        }
        // The folder in Settings is a link to the library's Valheim: the library's prefix.
        string valheim = Valheim(windows: true);
        Directory.CreateDirectory(Local("Games"));
        Directory.CreateSymbolicLink(Local("Games", "Valheim"), valheim);
        Assert.Equal(Prefix, Places.GameWorldRoot(Local("Games", "Valheim")));
        // A Windows build kept outside any library: the game's Proton prefix in a Steam folder on this computer when
        // there is one (its worlds folder need not exist yet), else the home's folder.
        string elsewhere = Local("Elsewhere", "Valheim");
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "valheim.exe"), "");
        Assert.Equal(Native, Places.GameWorldRoot(elsewhere));
        Directory.CreateDirectory(Local(".local", "share", "Steam", "steamapps", "compatdata", "892970", "pfx"));
        Assert.Equal(Prefix, Places.GameWorldRoot(elsewhere));
        Assert.False(Directory.Exists(Prefix));
    }

    [Fact]
    public void AFolderReachedTwoWaysIsOfferedOnce()
    {
        if (!LinuxLayout)
        {
            return;
        }
        // ~/.steam/steam is a link to ~/.local/share/Steam: the prefix reached both ways is one folder.
        Directory.CreateDirectory(Prefix);
        Directory.CreateDirectory(Local(".steam"));
        Directory.CreateSymbolicLink(Local(".steam", "steam"), Local(".local", "share", "Steam"));
        var roots = Places.WorldRootsForNew(new AppSettings { ValheimPath = Valheim(windows: true) });
        Assert.Equal(Prefix, roots[0]);
        Assert.Single(roots, r => r.Contains("compatdata"));
    }

    [Fact]
    public void TheInstalledGamesFolderIsOfferedFirstThenTheOthersThatExist()
    {
        if (!LinuxLayout)
        {
            return;
        }
        string valheim = Valheim(windows: true);
        Directory.CreateDirectory(Native);
        var settings = new AppSettings { ValheimPath = valheim };
        Assert.Equal([Prefix, Native], Places.WorldRootsForNew(settings));
        // The prefix once it exists: still first, once.
        Directory.CreateDirectory(Prefix);
        Assert.Equal([Prefix, Native], Places.WorldRootsForNew(settings));
        // The native game: its folder first, the prefix after.
        File.WriteAllText(Path.Combine(valheim, "valheim.x86_64"), "");
        Assert.Equal([Native, Prefix], Places.WorldRootsForNew(settings));
    }
}
