using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The generator's terrain steps called on their own (WorldGenerator.Terrain): a height inside a river is cut down to the
/// bank and the bed, a height outside any river is left alone, and far past the world's edge the Ashlands are sea floor
/// with no lava.
/// </summary>
public class WorldGeneratorTerrainTests
{
    /// <summary>
    /// A generator for a seed of the current generator version.
    /// </summary>
    private static WorldGenerator Generator() => WorldGenerator.Create(new World { Seed = 12345, WorldGenVersion = 2 });

    /// <summary>
    /// A generator whose only river is one point at the origin, of this width, as the generator lays them.
    /// </summary>
    private static WorldGenerator WithRiverAtTheOrigin(float width)
    {
        WorldGenerator generator = Generator();
        var cells = (Dictionary<Vector2i, WorldGenerator.RiverPoint[]>)PrivateMember.Get(generator, "_riverCells")!;
        cells.Clear();
        cells[WorldGenerator.GetRiverGrid(0f, 0f)] = [new WorldGenerator.RiverPoint(new Vector2(0f, 0f), width)];
        return generator;
    }

    [Fact]
    public void AHeightOutsideAnyRiverIsLeftAlone()
    {
        WorldGenerator generator = Generator();
        ((Dictionary<Vector2i, WorldGenerator.RiverPoint[]>)PrivateMember.Get(generator, "_riverCells")!).Clear();

        Assert.Equal(1f, PrivateCall.Instance(generator, "CutRivers", 0f, 0f, 1f));
    }

    [Fact]
    public void AHeightAboveTheBankInARiverIsCutDownToTheBed()
    {
        float cut = (float)PrivateCall.Instance(WithRiverAtTheOrigin(40f), "CutRivers", 0f, 0f, 1f)!;

        Assert.InRange(cut, 0.128f, 0.14f);
    }

    [Fact]
    public void AHeightBelowTheBankInARiverIsLeftAlone()
    {
        Assert.Equal(0.05f, PrivateCall.Instance(WithRiverAtTheOrigin(40f), "CutRivers", 0f, 0f, 0.05f));
    }

    [Fact]
    public void FarPastTheWorldsEdgeTheAshlandsAreSeaFloorWithNoLava()
    {
        float height = Generator().GetAshlandsHeight(0f, 12000f, out Color mask);

        Assert.Equal(0f, mask.A);
        Assert.True(height <= 0f, $"height {height} should be at or below sea level");
    }
}
