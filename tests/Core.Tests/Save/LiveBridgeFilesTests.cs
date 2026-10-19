using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The bridge client's file calls (LiveBridge.Files) against the stand-in game: the plugins the game loaded, the files
/// and folders under its BepInEx/config, its world file, and the 404s of a plugin older than 1.54.0.
/// </summary>
public class LiveBridgeFilesTests
{
    [Fact]
    public async Task AnOlderPluginServesNoFiles()
    {
        using var game = new FakeGame();
        using var live = new LiveBridge(game.Url, game.Token);
        Assert.Null(await live.Plugins());
        Assert.Null(await live.ConfigFile("expand_world_size.cfg"));
        Assert.Null(await live.ConfigFolder(""));
        Assert.Null(await live.WorldMeta());
    }

    [Fact]
    public async Task ThePluginsTheGameLoadedAreListed()
    {
        using var game = new FakeGame { Plugins = [("expand_world_size", "Expand World Size", "1.45.0"), ("Tie.WorldEditorBridge", "WorldEditorBridge", "1.54.0")] };
        using var live = new LiveBridge(game.Url, game.Token);
        var plugins = await live.Plugins();
        Assert.NotNull(plugins);
        Assert.Equal(["expand_world_size", "Tie.WorldEditorBridge"], plugins.Select(p => p.Id));
        Assert.Equal("Expand World Size", plugins[0].Name);
        Assert.Equal("1.45.0", plugins[0].Version);
    }

    [Fact]
    public async Task FilesAndFoldersUnderConfigAreRead()
    {
        using var game = new FakeGame { Plugins = [] };
        game.AddConfig("expand_world_size.cfg", "[1. General]\nWorld radius = 20000\n");
        game.AddConfig("expand_world/expand_biomes.yaml", "- biome: meadows\n");
        game.AddConfig("expand_world/more/expand_world_extra.yaml", "- biome: swamp\n");
        using var live = new LiveBridge(game.Url, game.Token);
        Assert.Equal("[1. General]\nWorld radius = 20000\n", Encoding.UTF8.GetString((await live.ConfigFile("expand_world_size.cfg"))!));
        Assert.Null(await live.ConfigFile("nowhere.cfg"));
        // A folder is not a file, nor a file a folder.
        Assert.Null(await live.ConfigFile("expand_world"));
        Assert.Null(await live.ConfigFolder("expand_world_size.cfg"));
        var root = await live.ConfigFolder("");
        Assert.NotNull(root);
        Assert.Equal(["expand_world", "expand_world_size.cfg"], root.Select(e => e.Name).Order());
        var data = await live.ConfigFolder("expand_world");
        Assert.NotNull(data);
        var biomes = data.Single(e => e.Name == "expand_biomes.yaml");
        Assert.False(biomes.Folder);
        Assert.Equal(17, biomes.Size);
        Assert.Equal(new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc), biomes.Modified);
        Assert.True(data.Single(e => e.Name == "more").Folder);
        Assert.Null(await live.ConfigFolder("elsewhere"));
        // A path out of the folder is refused, not missing.
        await Assert.ThrowsAsync<InvalidOperationException>(() => live.ConfigFile("../BepInEx.cfg"));
    }

    [Fact]
    public async Task TheWorldFileIsServedAsItLies()
    {
        using var game = new FakeGame { Plugins = [], WorldMeta = [1, 2, 3] };
        using var live = new LiveBridge(game.Url, game.Token);
        Assert.Equal([1, 2, 3], await live.WorldMeta());
        game.WorldMeta = null;
        Assert.Null(await live.WorldMeta());
    }

    [Fact]
    public async Task AnErrorFromTheBridgeIsThrown()
    {
        using var game = new FakeGame { Plugins = [] };
        game.Fail["/plugins"] = 500;
        using var live = new LiveBridge(game.Url, game.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(live.Plugins);
    }
}
