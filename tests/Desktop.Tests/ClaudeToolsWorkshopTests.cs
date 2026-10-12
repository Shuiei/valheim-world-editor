using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude in the Workshop (ClaudeTools): the blueprint library listed and searched, a blueprint opened on the plot or
/// added to it, what a piece on the plot holds (kept with its values, the plot having no world files), a dungeon on
/// the plot, and pieces that cannot go down at all.
/// </summary>
[Collection("World files")]
public class ClaudeToolsWorkshopTests
{
    // The library searched by name, description and tags; a blueprint opened on the plot, then added to it again
    // beside; names not in the library said.
    [AvaloniaFact]
    public async Task BlueprintsAreListedOpenedAndAdded()
    {
        using var r = new ClaudeRun();
        r.Hut("Hut", "A small wooden hut", "house", "wood");
        r.Hut("Tower", "Tall and stone", "defence");
        Assert.Equal(2, ClaudeRun.J(await r.T.ListBlueprints()).GetArrayLength());
        var huts = ClaudeRun.J(await r.T.ListBlueprints("WOODEN")).EnumerateArray().ToList();
        var hut = Assert.Single(huts);
        Assert.Equal("Hut", hut.GetProperty("name").GetString());
        Assert.Equal(2, hut.GetProperty("pieces").GetInt32());
        Assert.Equal("house", hut.GetProperty("tags")[0].GetString());
        Assert.Equal("Tower", ClaudeRun.J(await r.T.ListBlueprints("defence"))[0].GetProperty("name").GetString());
        Assert.Empty(ClaudeRun.J(await r.T.ListBlueprints("castle")).EnumerateArray());

        Assert.Equal("No blueprint \"Castle\" in the library (list_blueprints).", await r.T.OpenWorkshop("Castle"));
        var state = ClaudeRun.J(await r.T.OpenWorkshop("Hut"));
        Assert.Equal("workshop", state.GetProperty("page").GetString());
        Assert.Equal(2, state.GetProperty("area").GetProperty("objects").GetInt32());
        Assert.Equal("No blueprint \"Castle\" in the library (list_blueprints).", await r.T.AddBlueprint("Castle"));
        string added = await r.T.AddBlueprint("Tower", 20, 0);
        Assert.EndsWith("Now pending: 4 building piece(s) on the plot, not saved as a blueprint.", added);
        Assert.Equal("Claude: added Tower", r.W.Session!.UndoLabel);
        Assert.Equal(4, ClaudeRun.J(await r.T.EditorState()).GetProperty("area").GetProperty("objects").GetInt32());
    }

    // On the plot: an empty one stands; a chest filled and a sign written keep their values on the piece; pieces
    // that cannot go down at all are named and nothing goes in.
    [AvaloniaFact]
    public async Task PiecesOnThePlotHoldTheirValues()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        var empty = ClaudeRun.J(await r.T.SupportCheck());
        Assert.Equal((0, 0), (empty.GetProperty("pieces").GetInt32(), empty.GetProperty("wouldFall").GetInt32()));

        string none = await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "no_such_piece" }, new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 900 }]);
        Assert.StartsWith("Nothing put down: #0 no_such_piece: not an object the game has (find_prefabs); #1 woodwall: (0, 900) is outside the area", none);

        var placed = ClaudeRun.J(await r.T.PlacePieces(
        [
            new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = 0, Z = 0 },
            new ClaudeTools.PieceSpec { Prefab = "sign", X = 4, Z = 0 },
        ], "furniture")).GetProperty("placed");
        Assert.Equal("Claude: furniture", r.W.Session!.UndoLabel);
        int chest = placed[0].GetProperty("id").GetInt32(), sign = placed[1].GetProperty("id").GetInt32();
        Assert.Contains("piece(s) on the plot", ClaudeRun.J(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 10, Z = 10 }])).GetProperty("pending").GetString());

        string filled = await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 30 }, new ClaudeTools.ItemSpec { Item = "Amber", Stack = 2 }]);
        Assert.StartsWith("Changed piece_chest_wood; its id is now ", filled);
        string written = await r.T.SetContents(sign, text: "Hut for sale");
        Assert.StartsWith("Changed sign", written);
        var s = r.W.Session;
        static int NewId(string said) => int.Parse(said[(said.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);
        var signData = s.Edits.FindAdded(s.Scene.Things[NewId(written)].Id)!.Data!;
        Assert.Contains(signData, f => f.Key == StableHash.Of("text") && f.Value == "Hut for sale");
        var chestData = s.Edits.FindAdded(s.Scene.Things[NewId(filled)].Id)!.Data!;
        Assert.Contains(chestData, f => f.Key == StableHash.Of("items"));
    }

    // A dungeon on the plot, with what is there: one step, its notes said.
    [AvaloniaFact]
    public async Task ADungeonGoesOnThePlot()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        string made = await r.T.GenerateDungeon(size: 1, levels: 1, seed: 7);
        Assert.Contains("building piece(s) on the plot", made);
        Assert.True(ClaudeRun.J(await r.T.EditorState()).GetProperty("area").GetProperty("objects").GetInt32() > 0, made);
    }

    // save_blueprint: outside the Workshop, with no name or no piece: said. Saved, the Workshop holds nothing unsaved
    // and another blueprint opens; a name taken is refused unless overwrite; pieces that would fall are refused
    // unless evenIfFalling, whether the Support check is on or off.
    [AvaloniaFact]
    public async Task TheBuildingIsSavedAsABlueprintAndAnotherOpened()
    {
        using var r = new ClaudeRun();
        Assert.StartsWith("Blueprints are saved from the Workshop", await r.T.SaveBlueprint("Hut"));
        await r.T.OpenWorkshop();
        Assert.Equal("Give the blueprint a name.", await r.T.SaveBlueprint(" "));
        Assert.Equal("There is no building piece on the plot to save.", await r.T.SaveBlueprint("Hut"));
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_floor", X = 0, Z = 0 }]);
        Assert.StartsWith("Saved the blueprint “Hut”", await r.T.SaveBlueprint("Hut", "A floor", ["test"]));
        Assert.False(r.W.WorkshopDirty);
        Assert.True(File.Exists(Path.Combine(r.Homestead, "Hut.blueprint")));
        Assert.Equal("Hut", ClaudeRun.J(await r.T.ListBlueprints("Hut"))[0].GetProperty("name").GetString());

        // A second building, another name taken: refused, then replaced on purpose.
        r.Hut("Tower", "Tall");
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_floor", X = 6, Z = 0 }]);
        Assert.StartsWith("There is a blueprint called Tower already", await r.T.SaveBlueprint("Tower"));
        Assert.StartsWith("Saved the blueprint “Tower”", await r.T.SaveBlueprint("Tower", overwrite: true));

        // A wall in the air would fall, even with the Support check's colours off.
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 20, Z = 20, Y = 60, Snap = false }]);
        r.W.SupportBox.IsChecked = false;
        Assert.Contains("would fall in game", await r.T.SaveBlueprint("Floating"));
        Assert.StartsWith("Saved the blueprint “Floating”", await r.T.SaveBlueprint("Floating", evenIfFalling: true));
        Assert.Equal("workshop", ClaudeRun.J(await r.T.OpenWorkshop("Hut")).GetProperty("page").GetString());
    }
}
