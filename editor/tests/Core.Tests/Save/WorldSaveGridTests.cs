using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The zone grid a save's chunk files lie on, as its world file names it (WorldSave.SavedGridWidth), and a live world's
/// keys taken from the game's own world file (WorldSave.TakeLiveKeys).
/// </summary>
public class WorldSaveGridTests
{
    [Fact]
    public void TheSavedGridIsReadFromTheKeysWithinWhatAChunkIdCanName()
    {
        Assert.Equal(512, WorldSave.SavedGridWidth([], "w"));
        Assert.Equal(1024, WorldSave.SavedGridWidth(["ews_gridwidth 1024"], "w"));
        Assert.Equal(2048, WorldSave.SavedGridWidth(["ews_gridwidth 2048"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth 256"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth 4096"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth wide"], "w"));
    }

    /// <summary>A world file's bytes with these starting global keys.</summary>
    private static byte[] Meta(string dir, params string[] keys)
    {
        string path = Path.Combine(dir, "meta.fwl");
        WorldFileHead.Write(path, 32, keys);
        return File.ReadAllBytes(path);
    }

    [Fact]
    public async Task ALiveWorldTakesTheKeysOfTheGamesWorldFile()
    {
        using var w = new TempWorld();
        var save = w.Load();
        using var game = new FakeGame { Plugins = [], WorldMeta = Meta(w.Dir, "ews_world_radius 25000", "ews_gridwidth 1024") };
        using var live = new LiveBridge(game.Url, game.Token);

        await save.TakeLiveKeys(live);

        Assert.Equal(["ews_world_radius 25000", "ews_gridwidth 1024"], save.StartingGlobalKeys);
        Assert.Equal(1024, save.GridWidth);
    }

    [Fact]
    public async Task AWorldFileTheBridgeCannotServeLeavesTheKeysAsLoaded()
    {
        using var w = new TempWorld();
        var save = w.Load();
        save.StartingGlobalKeys.Add("loaded");
        using var game = new FakeGame { Plugins = [] };
        game.Fail["/world/meta"] = 500;
        using var live = new LiveBridge(game.Url, game.Token);

        await save.TakeLiveKeys(live);

        Assert.Equal(["loaded"], save.StartingGlobalKeys);
        // An older bridge (no world file served): the same.
        using var older = new FakeGame();
        using var olderLive = new LiveBridge(older.Url, older.Token);
        await save.TakeLiveKeys(olderLive);
        Assert.Equal(["loaded"], save.StartingGlobalKeys);
    }
}
