using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Valheim's world folders, the worlds found in them and in the recent and listed folders, and what a world folder is.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderWorldsTests : HomeFolderTestBase
{
    [Fact]
    public void ValheimsWorldFoldersAreUnderTheHome()
    {
        if (!LinuxLayout)
        {
            return;
        }
        var roots = Places.WorldRoots().ToList();
        Assert.Contains(Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local"), roots);
        Assert.Contains(Local(".config", "unity3d", "IronGate", "Valheim", "worlds"), roots);
        Assert.All(roots, r => Assert.StartsWith(Home, r));
        // Valheim through Proton keeps its folder inside Steam's prefix (Flatpak's Steam too).
        Assert.Contains(roots, r => r.Contains(Path.Combine("compatdata", "892970")));
        Assert.Contains(roots, r => r.Contains(Path.Combine("com.valvesoftware.Steam", ".local", "share", "Steam")));
    }

    // ~/.steam/steam is a link to ~/.local/share/Steam: a Proton world reached both ways is listed once (twice before),
    // and so is one added with a separator at the end.
    [Fact]
    public void AWorldReachedThroughALinkIsListedOnce()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        string steam = Local(".local", "share", "Steam");
        string prefix = Path.Combine(steam, "steamapps", "compatdata", "892970", "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
        CopyWorld(Path.Combine(prefix, "Proton"));
        Directory.CreateDirectory(Local(".steam"));
        Directory.CreateSymbolicLink(Local(".steam", "steam"), steam);
        var s = new AppSettings { WorldFolders = { Path.Combine(prefix, "Proton") + Path.DirectorySeparatorChar } };

        Assert.Single(Worlds.Find(s), w => w.Name == "Proton");
    }

    [Fact]
    public void TheGamesWorldsAreFoundWithOldSingleFileWorlds()
    {
        if (!LinuxLayout)
        {
            return;
        }
        string local = Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local");
        CopyWorld(Path.Combine(local, "Alpha"));
        Directory.CreateDirectory(Path.Combine(local, "Empty"));
        File.WriteAllText(Path.Combine(local, "Ancient.db"), "old");
        // An old world that has been converted already: its folder is listed, not the .db.
        CopyWorld(Path.Combine(local, "Converted"));
        File.WriteAllText(Path.Combine(local, "Converted.db"), "old");
        var found = Worlds.Find(new AppSettings());
        Assert.Contains(found, w => w.Name == "Alpha" && w.Where == "local" && w.Usable && w.SaveNumber == 2);
        Assert.DoesNotContain(found, w => w.Name == "Empty");
        var ancient = Assert.Single(found, w => w.Name == "Ancient");
        Assert.False(ancient.Usable);
        Assert.StartsWith("Old save format", ancient.Problem);
        Assert.DoesNotContain(found, w => w.Path.EndsWith("Converted.db", StringComparison.Ordinal));
        Assert.Single(found, w => w.Name == "Converted");
    }

    // A save number too large for one (a stray file) is passed over: the world is still listed (it vanished, with
    // nothing said).
    [Fact]
    public void AWorldWithAStraySaveNumberIsStillListed()
    {
        if (!LinuxLayout)
        {
            return;
        }
        string local = Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local");
        CopyWorld(Path.Combine(local, "Odd"));
        File.WriteAllText(Path.Combine(local, "Odd", "_main.99999999999.chunks"), "");

        Assert.Equal(2, Assert.Single(Worlds.Find(new AppSettings()), w => w.Name == "Odd").SaveNumber);
    }

    [Fact]
    public void RecentWorldsComeFirstAndMissingOnesAreLeftOut()
    {
        string mine = Local("saves", "Mine");
        CopyWorld(mine);
        string direct = Local("server", "Direct");
        CopyWorld(direct);
        var s = new AppSettings { WorldFolders = { direct, Local("no such folder") } };
        s.Recent.Add(new AppSettings.RecentWorld(Local("gone"), "Gone", DateTime.Now));
        s.Recent.Add(new AppSettings.RecentWorld(mine, "Mine", DateTime.Now));
        // Listed once, as recent, though it is also in a world folder.
        s.WorldFolders.Add(Local("saves"));
        var found = Worlds.Find(s);
        Assert.Equal("Mine", found[0].Name);
        Assert.Equal("recent", found[0].Where);
        Assert.Single(found, w => w.Path == mine);
        Assert.Contains(found, w => w.Path == direct && w.Where == "yours");
        Assert.DoesNotContain(found, w => w.Name == "Gone");
    }

    [Fact]
    public void WorldFoldersAreCheckedForWhatTheyAre()
    {
        Assert.Equal("Enter the world's folder.", Worlds.Check(" "));
        File.WriteAllText(Local("Old.db"), "x");
        Assert.StartsWith("That is an old single-file world", Worlds.Check(Local("Old.db")));
        Assert.Equal("Alpha", Worlds.WorldName(Local("x", "Alpha") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void AFolderThatCannotBeReadIsSaidSo()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string locked = Local("locked");
        Directory.CreateDirectory(Path.Combine(locked, "inside"));
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.StartsWith("That folder cannot be read", Worlds.Check(locked));
            // A readable folder holding an unreadable one: that one simply holds no world.
            Assert.StartsWith("No Valheim world", Worlds.Check(Home));
            // A recent world now in a folder that cannot be read: left out, the rest still listed.
            var s = new AppSettings();
            s.Recent.Add(new AppSettings.RecentWorld(locked, "Locked", DateTime.Now));
            Assert.DoesNotContain(Worlds.Find(s), w => w.Path == locked);
            // A folder of worlds added in Settings that holds one that cannot be read (a drive's root):
            // the list is still made (it threw, and the start page listed no world at all).
            s.WorldFolders.Add(Home);
            Assert.DoesNotContain(Worlds.Find(s), w => w.Path == locked);
        }
        finally
        {
            RestoreAccess(locked);
        }
    }
}
