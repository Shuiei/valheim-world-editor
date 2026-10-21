using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's terrain operations read from its bundles (CatalogueGame): each TerrainOp's settings by the name of the
/// object it is on (a prefab's root or a child of it), every setting kept.
/// </summary>
public class GameCataloguesTerrainOpScanTests
{
    [Fact]
    public void EachTerrainOpsSettingsAreReadByItsObjectsName()
    {
        using var game = CatalogueGame.Make();
        game.Save();
        var json = JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["terrain-ops"])["terrain-ops"])!.AsObject();
        Assert.Equal(["paved_test", "raise_op"], json.Select(kv => kv.Key).Order());
        var paved = json["paved_test"]!;
        Assert.Equal(14, paved.AsObject().Count);
        Assert.Equal((1, 3.0, 2, 2.2f), (paved["m_smooth"]!.GetValue<int>(), paved["m_smoothRadius"]!.GetValue<double>(), paved["m_paintType"]!.GetValue<int>(), (float)paved["m_paintRadius"]!.GetValue<double>()));
        var raise = json["raise_op"]!;
        Assert.Equal((1, 0.5, 0.1f, 0), (raise["m_raise"]!.GetValue<int>(), raise["m_levelOffset"]!.GetValue<double>(), (float)raise["m_raisePower"]!.GetValue<double>(), raise["m_level"]!.GetValue<int>()));
    }
}
