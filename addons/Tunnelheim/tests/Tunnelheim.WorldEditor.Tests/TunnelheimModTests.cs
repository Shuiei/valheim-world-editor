using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// Tunnelheim as the add-on knows it: its data objects told apart by the game's hashes of their names, and its settings as
/// the world's game has them (the mod's defaults without, or when one cannot be read).
/// </summary>
public class TunnelheimModTests
{
    [Fact]
    public void ItsDataObjectsAreToldApart()
    {
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.RecordPrefab));
        Assert.True(TunnelheimMod.IsData(TunnelheimMod.CheckpointPrefab));
        Assert.Equal(1906006406, TunnelheimMod.RecordPrefab);
        Assert.Equal(-1717259926, TunnelheimMod.CheckpointPrefab);
        Assert.False(TunnelheimMod.IsData(0));
    }

    [Fact]
    public void ItsSettingsAreTheGamesOrTheModsDefaults()
    {
        var none = new FakeHost();
        Assert.Equal(64f, TunnelheimMod.MaxBuildHeight(none));
        Assert.True(TunnelheimMod.AshlandsLava(none));
        var set = new FakeHost { Settings = { [("Building", "MaxHeightAboveGround")] = "20.5", [("Biomes", "AshlandsLavaOnRock")] = "false" } };
        Assert.Equal(20.5f, TunnelheimMod.MaxBuildHeight(set));
        Assert.False(TunnelheimMod.AshlandsLava(set));
        var bad = new FakeHost { Settings = { [("Building", "MaxHeightAboveGround")] = "high", [("Biomes", "AshlandsLavaOnRock")] = "maybe" } };
        Assert.Equal(64f, TunnelheimMod.MaxBuildHeight(bad));
        Assert.True(TunnelheimMod.AshlandsLava(bad));
        Assert.Equal(64f, TunnelheimMod.MaxBuildHeight(new FakeHost { Settings = { [("Building", "MaxHeightAboveGround")] = "NaN" } }));
        Assert.Equal(64f, TunnelheimMod.MaxBuildHeight(new FakeHost { Settings = { [("Building", "MaxHeightAboveGround")] = "Infinity" } }));
    }
}
