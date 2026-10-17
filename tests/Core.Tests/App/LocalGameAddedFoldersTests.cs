using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The BepInEx folders behind the folders the player added in Settings (LocalGame.AddedBepInExFolders): a folder that
/// is BepInEx itself, a folder holding one, or a folder of profiles that each hold one; anything else, or a folder that
/// does not exist, gives nothing.
/// </summary>
public class LocalGameAddedFoldersTests : PretendHomeTestBase
{
    private static List<(string BepInEx, string Where)> Added(params string[] folders) =>
        [.. (IEnumerable<(string BepInEx, string Where)>)PrivateCall.Static(typeof(LocalGame), "AddedBepInExFolders", (IEnumerable<string>)folders)!];

    [Fact]
    public void AMissingFolderGivesNothing()
    {
        Assert.Empty(Added(Path.Combine(Home, "not-there")));
    }

    [Fact]
    public void AFolderNamedBepInExIsItselfTheBepInExFolder()
    {
        string bepInEx = Path.Combine(Home, "custom", "BepInEx");
        Directory.CreateDirectory(bepInEx);
        Assert.Equal([(bepInEx, "your folder")], Added(bepInEx));
    }

    [Fact]
    public void AFolderHoldingABepInExFolderIsNamedInQuotes()
    {
        string folder = Path.Combine(Home, "server");
        Directory.CreateDirectory(Path.Combine(folder, "BepInEx"));
        Assert.Equal([(Path.Combine(folder, "BepInEx"), "\"server\"")], Added(folder));
    }

    [Fact]
    public void AFolderOfProfilesGivesEachProfileThatHoldsABepInExFolder()
    {
        string profiles = Path.Combine(Home, "profiles");
        string withBepInEx = Path.Combine(profiles, "Default");
        Directory.CreateDirectory(Path.Combine(withBepInEx, "BepInEx"));
        Directory.CreateDirectory(Path.Combine(profiles, "Empty"));
        Assert.Equal([(Path.Combine(withBepInEx, "BepInEx"), "profile \"Default\"")], Added(profiles));
    }

    [Fact]
    public void AFolderWithNoBepInExInItOrItsProfilesGivesNothing()
    {
        string folder = Path.Combine(Home, "plain");
        Directory.CreateDirectory(Path.Combine(folder, "Sub"));
        Assert.Empty(Added(folder));
    }
}
