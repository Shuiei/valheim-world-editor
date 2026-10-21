using TerrainEditor.Save;
using TerrainEditor.Tunnelheim;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The game's terrain operations read from the "terrain-ops" game data, by the hash of their name.</summary>
public class TerrainOpsTests
{
    [Fact]
    public void TheGamesHoeStrokesAreFoundByTheirNamesHash()
    {
        var ops = TerrainOps.Read(File.ReadAllText(Path.Combine(Fixtures.Root, "terrain-ops.json")));
        var paved = ops[StableHash.Of("paved_road_v2")];
        Assert.Equal((true, 3f, 1f, true, 2, 2.2f), (paved.Smooth, paved.SmoothRadius, paved.SmoothPower, paved.PaintCleared, paved.PaintType, paved.PaintRadius));
        Assert.False(paved.Level);
        Assert.Equal(0.1f, ops[StableHash.Of("raise_v2")].RaisePower);
        Assert.Empty(TerrainOps.Read("{}"));
    }
}
