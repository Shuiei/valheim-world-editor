using System.Text;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game as extensions see it (LiveGame): the bridge's plugins and BepInEx/config files given in the SDK's types,
/// nothing from an older bridge, and a wait given up when asked.
/// </summary>
public class LiveGameTests
{
    [Fact]
    public async Task ThePluginsAndConfigFilesComeInTheSdksTypes()
    {
        using var game = new FakeGame { Plugins = [("expand_world_size", "Expand World Size", "1.45.0")] };
        game.AddConfig("expand_world_size.cfg", "World radius = 20000\n");
        game.AddConfig("expand_world/expand_biomes.yaml", "- biome: meadows\n");
        using var live = new LiveBridge(game.Url, game.Token);
        var mod = new LiveGame(live, ["Sized"]);

        var plugin = Assert.Single((await mod.Plugins(TestContext.Current.CancellationToken))!);
        Assert.Equal(("expand_world_size", "Expand World Size", "1.45.0"), (plugin.Id, plugin.Name, plugin.Version));
        Assert.Equal("World radius = 20000\n", Encoding.UTF8.GetString((await mod.ConfigFile("expand_world_size.cfg", TestContext.Current.CancellationToken))!));
        var entries = (await mod.ConfigFolder("", TestContext.Current.CancellationToken))!.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(["expand_world", "expand_world_size.cfg"], entries.Select(e => e.Name));
        Assert.True(entries[0].Folder);
        Assert.Equal(21, entries[1].Size);
        Assert.Equal(["Sized"], mod.Mods);
    }

    [Fact]
    public async Task AnOlderBridgeGivesNothingAndAWaitCanBeGivenUp()
    {
        using var game = new FakeGame();
        using var live = new LiveBridge(game.Url, game.Token);
        var mod = new LiveGame(live, []);

        Assert.Null(await mod.Plugins(TestContext.Current.CancellationToken));
        Assert.Null(await mod.ConfigFile("expand_world_size.cfg", TestContext.Current.CancellationToken));
        Assert.Null(await mod.ConfigFolder("", TestContext.Current.CancellationToken));
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mod.Plugins(cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mod.ConfigFile("x.cfg", cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mod.ConfigFolder("", cancel.Token));
    }
}
