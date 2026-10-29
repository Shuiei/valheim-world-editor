using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The group a build piece is listed under when it comes from a mod: a piece of the game (not from a mod) has none, so
/// it is listed under its station instead.
/// </summary>
public sealed class BuildPanelPieceGroupTests
{
    /// <summary>A piece of the game itself has no mod group.</summary>
    [Fact]
    public void APieceOfTheGameHasNoModGroup() =>
        Assert.Null(BuildPanel.PieceGroup("Wood"));
}
