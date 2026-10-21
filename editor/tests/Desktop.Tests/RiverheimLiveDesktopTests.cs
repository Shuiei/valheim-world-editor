using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opening a live world made with Riverheim (WorldSession.OpenLive): its world file, served by the game's bridge, says
/// so; it is drawn with the player's Riverheim when the game runs the same release, and refused with why otherwise.
/// </summary>
[Collection("DataDir")]
public sealed class RiverheimLiveDesktopTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-liverh-" + Guid.NewGuid().ToString("N")[..8]);

    public RiverheimLiveDesktopTests()
    {
        AppSettings.DataDirOverride = _dir;
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>Puts the data folder and the tests' copies back, and deletes the folder.</summary>
    public void Dispose()
    {
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>A game running this Riverheim, its world made with it; the player's Riverheim on, in their BepInEx folder.</summary>
    private FakeGame Game(string version)
    {
        string bep = Path.Combine(_dir, "bep", "BepInEx");
        TestMods.Install(bep, TestMods.Riverheim);
        TestMods.InstallCore(bep);
        var settings = AppSettings.Load();
        settings.Riverheim = true;
        settings.ExpandWorldFolder = bep;
        settings.ValheimPath = UnityStandIn.Game(Path.Combine(_dir, "Valheim"));
        Assert.Null(settings.Save());
        string head = Path.Combine(_dir, "head.fwl2");
        WorldFileHead.Write(head, 42, [], WorldFileHead.RiverheimPreset);
        return new FakeGame { Plugins = [(Riverheim.PluginId, "Riverheim", version)], WorldMeta = File.ReadAllBytes(head) };
    }

    [Fact]
    public async Task ItIsDrawnWithThePlayersRiverheimOfTheSameRelease()
    {
        using var game = Game("1.2.0");
        using var world = await OpenLive(game);
        Assert.True(world.IsLive);
        Assert.Equal(("Riverheim", "1.2.1", "builtin.amur v2"), (world.World.Rules.Generator!.Name, world.World.Rules.Generator.Version, world.World.Rules.Generator.Settings));
    }

    [Fact]
    public async Task AGameOnAnotherReleaseIsRefused()
    {
        using var game = Game("1.1.1");
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => OpenLive(game));
        Assert.Equal(Lang.T("riverheim.otherVersion", "1.1.1", "1.2.1"), e.Message);
    }
}
