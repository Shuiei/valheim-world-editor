using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Settings: where they live, that they survive a round trip, that broken settings give defaults, and the recent worlds list.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderSettingsTests : HomeFolderTestBase
{
    private static readonly string[] Expected2 = ["/w"];
    private static readonly string[] Expected1 = ["/b"];
    [Fact]
    public void SettingsLiveInTheDataFolderAndSurviveARoundTrip()
    {
        Assert.Equal(Local(".local", "share", "ValheimWorldEditor"), AppSettings.DataDir);
        Assert.True(Directory.Exists(AppSettings.DataDir));
        var s = new AppSettings { ValheimPath = "/games/valheim", LiveUrl = "http://x:1", LastMode = "server", WorldFolders = { "/w" }, BepInExFolders = { "/b" } };
        s.Save();
        var back = AppSettings.Load();
        Assert.Equal("/games/valheim", back.ValheimPath);
        Assert.Equal("server", back.LastMode);
        Assert.Equal(Expected2, back.WorldFolders);
        Assert.Equal(Expected1, back.BepInExFolders);
    }

    [Fact]
    public void BrokenSettingsGiveDefaults()
    {
        File.WriteAllText(Path.Combine(AppSettings.DataDir, "settings.json"), "{ this is not json");
        var s = AppSettings.Load();
        Assert.Null(s.ValheimPath);
        Assert.Empty(s.Recent);
        // "null" is valid JSON for no settings at all.
        File.WriteAllText(Path.Combine(AppSettings.DataDir, "settings.json"), "null");
        Assert.NotNull(AppSettings.Load());
    }

    [Fact]
    public void RecentWorldsAreNewestFirstOnceEachAndAtMostTwelve()
    {
        var s = new AppSettings();
        for (int i = 0; i < 14; i++)
        {
            s.AddRecent(Local("w" + i), "W" + i);
        }
        Assert.Equal(12, s.Recent.Count);
        Assert.Equal("W13", s.Recent[0].Name);
        // The same folder written another way moves to the top instead of appearing twice.
        s.AddRecent(Local("w5", "..", "w5"), "W5 again");
        Assert.Equal(12, s.Recent.Count);
        Assert.Equal("W5 again", s.Recent[0].Name);
        Assert.Single(s.Recent, r => Path.GetFullPath(r.Path) == Local("w5"));
        Assert.Equal(12, AppSettings.Load().Recent.Count);
    }
}
