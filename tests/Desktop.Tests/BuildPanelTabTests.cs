using TerrainEditor.App;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Where a piece is listed: the cultivator's and the serving tray's pieces go to their own tabs, the rest to the hammer tab of
/// their category; a piece with no station is headed by the "no station" text.
/// </summary>
public class BuildPanelTabTests
{
    /// <summary>
    /// A piece of the game's (not a mod's) prefab, of a hammer category, with a build tool or none.
    /// </summary>
    private static PieceCatalog.Info Piece(int category, string? tool = null) =>
        new("Test piece", 0, category, 0, 0, 0, 0, 0, 0) { Tool = tool };

    [Fact]
    public void AHammerPieceIsListedUnderItsCategory()
    {
        Assert.Equal(3, BuildPanel.TabOf("vwe_test_tab_hammer", Piece(3, "hammer")));
    }

    [Fact]
    public void ACultivatorPieceIsListedOnThePlantsTab()
    {
        Assert.Equal(BuildPanel.Plants, BuildPanel.TabOf("vwe_test_tab_plant", Piece(1, "cultivator")));
    }

    [Fact]
    public void AServingTrayPieceIsListedOnTheFeastsTab()
    {
        Assert.Equal(BuildPanel.Feasts, BuildPanel.TabOf("vwe_test_tab_feast", Piece(1, "feaster")));
    }

    [Fact]
    public void APieceWithNoStationIsHeadedByTheNoStationText()
    {
        Assert.Equal(Lang.T("build.noStation"), BuildPanel.StationHeading("vwe_test_tab_nostation"));
    }
}
