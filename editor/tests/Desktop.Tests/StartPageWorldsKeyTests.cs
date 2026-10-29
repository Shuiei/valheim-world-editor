using System.Reflection;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The key that tells whether the start page's list of worlds has changed: one line per world, and a change in any of a
/// world's details (here its save number) gives a different key.
/// </summary>
public class StartPageWorldsKeyTests
{
    /// <summary>The key of a list of worlds.</summary>
    private static string WorldsKey(List<Worlds.Info> worlds) => (string)typeof(StartPage).GetMethod("WorldsKey", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [worlds])!;

    [Fact]
    public void TheSameWorldsGiveTheSameKeyOneLinePerWorld()
    {
        var saved = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var worlds = new List<Worlds.Info>
        {
            new("A", "/w/a", saved, 3, "yours", true, null),
            new("B", "/w/b", saved, 1, "local", false, "broken"),
        };
        string key = WorldsKey(worlds);
        Assert.Equal(2, key.Split('\n').Length);
        Assert.Equal(key, WorldsKey([.. worlds]));
    }

    [Fact]
    public void AChangedSaveNumberGivesADifferentKey()
    {
        var saved = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var before = new List<Worlds.Info> { new("A", "/w/a", saved, 3, "yours", true, null) };
        var after = new List<Worlds.Info> { new("A", "/w/a", saved, 4, "yours", true, null) };
        Assert.NotEqual(WorldsKey(before), WorldsKey(after));
    }
}
