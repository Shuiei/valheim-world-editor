using Xunit;
using static TerrainEditor.Desktop.Tests.LiveFollowSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The live follow setup helpers, tested once here so the tests using them can rely on them.
/// </summary>
public sealed class LiveFollowSetupTests
{
    [Fact]
    public async Task OpenWithTreeGivesTheFirstSavedTreeAndItsObjectId()
    {
        var (game, world, s, tree, id) = await OpenWithTree();
        using var _ = game;
        Assert.False(s.Scene.Things[tree].Piece);
        Assert.Equal(s.Scene.Things[tree].Id, id);
        Assert.Equal((0, 0), world.World.ObjectRefs[id].Zone);
    }
}
