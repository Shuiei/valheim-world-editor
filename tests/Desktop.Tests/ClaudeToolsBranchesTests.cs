using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using ModelContextProtocol.Protocol;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The less trodden ways through Claude's tools (ClaudeTools): four views of a point given and over the map, roads
/// painted dirt on the ground's own heights, a dungeon over the view's middle, the Workshop refused over an unsaved
/// world, stands given items and armour, a chest given what is no item, a creature's stars, a blueprint's chest
/// filled on the plot, and a piece with no recipe.
/// </summary>
[Collection("World files")]
public class ClaudeToolsBranchesTests
{
    /// <summary>A plain PNG of the given size, written to <paramref name="path"/>.</summary>
    private static Task Png(string path, int w, int h)
    {
        using var bitmap = new SKBitmap(w, h);
        bitmap.Erase(SKColors.SkyBlue);
        using var image = SKImage.FromBitmap(bitmap);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return Task.CompletedTask;
    }

    /// <summary>The new id a set_contents answer gives ("…; its id is now 12.").</summary>
    private static int NewId(string said) => int.Parse(said[(said.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The id of the one piece a place_pieces answer put down.</summary>
    private static int Placed(string json) => ClaudeRun.J(json).GetProperty("placed")[0].GetProperty("id").GetInt32();

    // Four views of a point given (each 640 wide, two by two); with the map shown over an open area, four views are
    // refused and the state says the map.
    [AvaloniaFact]
    public async Task FourViewsOfAPointAndNoneOverTheMap()
    {
        using var r = new ClaudeRun();
        r.T.ViewPicture = path => Png(path, 640, 480);
        r.T.MapPicture = path => Png(path, 320, 240);
        await r.OpenArea();
        var four = await r.T.Screenshot(x: 5, z: -5, distance: 60, height: 40, views: 4);
        Assert.NotEqual(true, four.IsError);
        var block = Assert.Single(four.Content.OfType<ImageContentBlock>());
        using (var sheet = SKBitmap.Decode(block.DecodedData.ToArray()))
        {
            Assert.Equal((1280, 960), (sheet.Width, sheet.Height));
        }

        r.W.ShowMap();
        Assert.True(r.W.MapShown);
        Assert.NotNull(r.W.Session);
        var refused = await r.T.Screenshot(views: 4);
        Assert.True(refused.IsError);
        Assert.StartsWith("Four views need", Assert.IsType<TextContentBlock>(Assert.Single(refused.Content)).Text);
        var state = ClaudeRun.J(await r.T.EditorState());
        Assert.Equal("map", state.GetProperty("page").GetString());
    }

    // A road painted dirt on the ground's heights (none given, or not one per point): one pending step each.
    [AvaloniaFact]
    public async Task RoadsOnTheGroundsOwnHeights()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.StartsWith("road of 1 segment(s)", await r.T.Road([[-20f, 0f], [20f, 0f]], paint: "dirt"));
        Assert.Equal("Claude: road", r.W.Session!.UndoLabel);
        Assert.StartsWith("road of 1 segment(s)", await r.T.Road([[-20f, 10f], [20f, 10f]], heights: [40f]));
        Assert.True(r.W.World!.Pending.Zones > 0);
    }

    // With no point given, the dungeon goes over the view's middle.
    [AvaloniaFact]
    public async Task ADungeonGoesOverTheViewsMiddle()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        string made = await r.T.GenerateDungeon(size: 1, levels: 1, seed: 5);
        Assert.Contains("Now pending:", made);
        Assert.True(r.W.World!.Pending.Added > 0, made);
    }

    // A world with changes not saved: the Workshop is not opened over it.
    [AvaloniaFact]
    public async Task TheWorkshopWaitsForAnUnsavedWorld()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        string refused = await r.T.OpenWorkshop();
        Assert.StartsWith("The open world has changes not saved (", refused);
        Assert.EndsWith("ask the user to save or discard them first.", refused);
        Assert.False(r.W.InWorkshop);
    }

    // An item stand takes one item it can hold (or none: emptied); an armour stand what it can wear, with a pose; what
    // they cannot take is said. A chest given what is no item of the game is not changed.
    [AvaloniaFact]
    public async Task StandsTakeWhatTheyCanHold()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int stand = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "itemstand", X = -12, Z = -10 }]));
        int armour = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "ArmorStand", X = -14, Z = -10 }]));
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -16, Z = -10 }]));

        Assert.Contains("holds one item, not 2", await r.T.SetContents(stand, [new ClaudeTools.ItemSpec { Item = "SwordIron" }, new ClaudeTools.ItemSpec { Item = "Wood" }]));
        Assert.DoesNotContain("Changed", await r.T.SetContents(stand, [new ClaudeTools.ItemSpec { Item = "Wood" }]));
        string sword = await r.T.SetContents(stand, [new ClaudeTools.ItemSpec { Item = "SwordIron", Quality = 3 }], orientation: 1);
        Assert.StartsWith("Changed", sword);
        Assert.StartsWith("Changed", await r.T.SetContents(NewId(sword), []));

        Assert.DoesNotContain("Changed", await r.T.SetContents(armour, [new ClaudeTools.ItemSpec { Item = "CookedMeat" }]));
        Assert.StartsWith("Changed", await r.T.SetContents(armour, [new ClaudeTools.ItemSpec { Item = "HelmetBronze" }], pose: 2));

        Assert.StartsWith("Could not change it: ", await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "NoSuchItem" }]));
    }

    // On the plot: a creature's stars (clamped to 2: level 3), stars refused for a chest, a blueprint's chest filled,
    // the blueprint added again at the plot's middle; a piece with no recipe has no station.
    [AvaloniaFact]
    public async Task StarsAndABlueprintsChestOnThePlot()
    {
        using var r = new ClaudeRun();
        Directory.CreateDirectory(r.Homestead);
        var piece = new JsonObject { ["name"] = "piece_chest_wood", ["dx"] = 0, ["dy"] = 0, ["dz"] = 0, ["rx"] = 0, ["ry"] = 0, ["rz"] = 0, ["scale"] = 0 };
        var clip = new JsonObject { ["objects"] = new JsonArray(piece) };
        File.WriteAllText(Path.Combine(r.Homestead, Homestead.FileName("Store")), Homestead.Write(clip, "Store", "Tester", null, DateTime.Now, "A chest", ["chest"]));
        Assert.Equal("workshop", ClaudeRun.J(await r.T.OpenWorkshop("Store")).GetProperty("page").GetString());

        int chest = ClaudeRun.J(await r.T.ListObjects(name: "piece_chest_wood"))[0].GetProperty("id").GetInt32();
        Assert.StartsWith("Changed piece_chest_wood", await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 5 }]));
        chest = ClaudeRun.J(await r.T.ListObjects(name: "piece_chest_wood"))[0].GetProperty("id").GetInt32();
        Assert.Contains("is not a creature: only creatures have stars", await r.T.SetContents(chest, stars: 1));

        var s = r.W.Session!;
        int boar = s.Commit("a boar", null, [], [(new Editing.NewObject(0, StableHash.Of("Boar"), new Vector3(10, 34, 10), Vector3.Zero, 0), false)])[0];
        string starred = await r.T.SetContents(boar, stars: 5);
        Assert.StartsWith("Changed", starred);
        var data = s.Edits.FindAdded(s.Scene.Things[NewId(starred)].Id)!.Data!;
        Assert.Contains(data, f => f.Key == StableHash.Of("level") && f.Value == "3");

        Assert.Contains("Now pending:", await r.T.AddBlueprint("Store"));

        // A piece the game asks no station for (the wooden gate, once the example, now has its real cost in the fixtures).
        var gate = Assert.Single(ClaudeRun.J(await ClaudeTools.PieceInfo(["Cart"])).EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, gate.GetProperty("station").ValueKind);
    }
}
