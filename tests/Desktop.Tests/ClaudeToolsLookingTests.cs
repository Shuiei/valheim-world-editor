using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's looking tools (ClaudeTools): what each says with nothing open, the worlds on this computer, objects found by
/// name and near a point (with tilts and size when they have them), objects selected for the user, and what can be
/// placed, with the building pieces' sizes.
/// </summary>
[Collection("World files")]
public class ClaudeToolsLookingTests
{
    // On the start page, nothing open: every tool says what to open first, and changes nothing.
    [AvaloniaFact]
    public async Task WithNothingOpenEachToolSaysWhatToOpen()
    {
        using var r = new ClaudeRun();
        var t = r.T;
        Assert.Equal("Open a world first (open_world).", await t.OpenArea(0, 0));
        Assert.StartsWith("No area is open", await t.DescribeArea());
        Assert.Equal("No area is open.", await t.ListObjects());
        Assert.Equal("No area is open.", await t.GetSelection());
        Assert.Equal("No area is open.", await t.SelectObjects([1]));
        Assert.StartsWith("No area is open", await t.RunScript("Ground.Raise(0, 0, 1);"));
        Assert.StartsWith("No area is open", await t.Flatten(0, 0, 4, 4));
        Assert.StartsWith("No area is open", await t.Undo());
        Assert.StartsWith("No area is open", await t.Redo());
        Assert.StartsWith("No area is open", await t.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "woodwall" }]));
        Assert.StartsWith("No area is open", await t.BuildFloor(0, 0, 4, 4));
        Assert.StartsWith("No area is open", await t.RemoveObjects([1]));
        Assert.StartsWith("No area is open", await t.SetContents(1, text: "hello"));
        Assert.StartsWith("No area is open", await t.GenerateDungeon());
        Assert.Contains("open_workshop first", await t.SupportCheck());
        Assert.StartsWith("No area is open", await t.AddBlueprint("Hut"));
        var map = await t.AreaMap();
        Assert.True(map.IsError);
        Assert.Equal("No area is open.", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(map.Content)).Text);
        var shot = await t.Screenshot();
        Assert.True(shot.IsError);
        Assert.StartsWith("Nothing to see", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(shot.Content)).Text);
        var four = await t.Screenshot(views: 4);
        Assert.True(four.IsError);
        Assert.StartsWith("Four views need", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(four.Content)).Text);
        // A folder that is not a world: said, and still nothing open.
        Assert.StartsWith("Could not open it", await t.OpenWorld(Path.Combine(r.Dir, "no such world")));
        Assert.Null(r.W.World);
    }

    // The worlds the start page lists (a recent one here), then objects of an area by name, near a point (nearest
    // first) and by kind; selected for the user with the ids that are not objects said.
    [AvaloniaFact]
    public async Task WorldsAreListedAndObjectsFoundAndSelected()
    {
        using var r = new ClaudeRun();
        r.W.Settings.AddRecent(r.Dir, "CITest");
        var worlds = ClaudeRun.J(await r.T.ListWorlds());
        var world = Assert.Single(worlds.EnumerateArray(), w => w.GetProperty("folder").GetString() == Path.GetFullPath(r.Dir));
        Assert.Equal("recent", world.GetProperty("where").GetString());
        Assert.True(world.GetProperty("usable").GetBoolean());
        await r.OpenArea();

        var all = ClaudeRun.J(await r.T.ListObjects(limit: 2000)).EnumerateArray().ToList();
        Assert.NotEmpty(all);
        string name = all[0].GetProperty("name").GetString()!;
        var named = ClaudeRun.J(await r.T.ListObjects(name: name.ToUpperInvariant(), limit: 2000)).EnumerateArray().ToList();
        Assert.NotEmpty(named);
        Assert.All(named, o => Assert.Contains(name, o.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase));
        Assert.True(named.Count <= all.Count);

        float x = all[0].GetProperty("x").GetSingle(), z = all[0].GetProperty("z").GetSingle();
        var near = ClaudeRun.J(await r.T.ListObjects(x: x, z: z, radius: 15, limit: 2000)).EnumerateArray().ToList();
        var distances = near.Select(o => MathF.Sqrt(MathF.Pow(o.GetProperty("x").GetSingle() - x, 2) + MathF.Pow(o.GetProperty("z").GetSingle() - z, 2))).ToList();
        Assert.Equal(all[0].GetProperty("id").GetInt32(), near[0].GetProperty("id").GetInt32());
        Assert.All(distances, d => Assert.True(d <= 15.01f, $"{d} m away"));
        Assert.True(distances[0] <= distances.Min() + 0.02f);
        Assert.Single(ClaudeRun.J(await r.T.ListObjects(x: x, z: z, radius: 15, limit: 0)).EnumerateArray());

        int id = all[0].GetProperty("id").GetInt32();
        // A repeated id is one id: two of them were not objects.
        Assert.Equal("Selected 1 (2 id(s) were not objects of the area).", await r.T.SelectObjects([id, id, -1, 1_000_000]));
        Assert.Equal(id, Assert.Single(ClaudeRun.J(await r.T.GetSelection()).EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal("Selected 0 (1 id(s) were not objects of the area).", await r.T.SelectObjects([-3]));
        Assert.Empty(ClaudeRun.J(await r.T.GetSelection()).EnumerateArray());
    }

    // What can be placed: the hammer's pieces with their name, size and cost; trees and rocks by kind; items for
    // chests. A piece's size, bounds and snap points; a name that is not a piece is said.
    [AvaloniaFact]
    public async Task PrefabsItemsAndPiecesAreFound()
    {
        var walls = ClaudeRun.J(await ClaudeTools.FindPrefabs("wood wall", piecesOnly: true, limit: 500)).EnumerateArray().ToList();
        var wall = Assert.Single(walls, p => p.GetProperty("prefab").GetString() == "woodwall");
        Assert.Equal("hammer", wall.GetProperty("tool").GetString());
        Assert.InRange(wall.GetProperty("size").GetProperty("x").GetSingle(), 1.8f, 2.3f);
        Assert.InRange(wall.GetProperty("size").GetProperty("y").GetSingle(), 1.8f, 2.3f);
        Assert.NotEmpty(wall.GetProperty("cost").GetString()!);
        Assert.All(walls, p => Assert.True(p.TryGetProperty("tool", out _)));

        var beech = ClaudeRun.J(await ClaudeTools.FindPrefabs("beech1")).EnumerateArray().ToList();
        Assert.Contains(beech, p => p.GetProperty("prefab").GetString() == "Beech1" && p.GetProperty("kind").GetString() == "Trees");
        Assert.Empty(ClaudeRun.J(await ClaudeTools.FindPrefabs("beech1", piecesOnly: true)).EnumerateArray());
        Assert.Single(ClaudeRun.J(await ClaudeTools.FindPrefabs("wood", limit: 1)).EnumerateArray());

        // Spawn points by the creature they spawn, with what they spawn.
        var boar = ClaudeRun.J(await ClaudeTools.FindPrefabs("boar")).EnumerateArray().ToList();
        var spawner = Assert.Single(boar, p => p.GetProperty("prefab").GetString() == "Spawner_Boar");
        Assert.Equal("SpawnPoints", spawner.GetProperty("kind").GetString());
        Assert.Equal("Boar · 0–2 ★ · spawns once", spawner.GetProperty("spawns").GetString());
        var nest = Assert.Single(ClaudeRun.J(await ClaudeTools.FindPrefabs("greydwarf_shaman")).EnumerateArray(), p => p.GetProperty("prefab").GetString() == "Spawner_GreydwarfNest");
        Assert.Equal("Nests", nest.GetProperty("kind").GetString());
        Assert.All(beech, p => Assert.False(p.TryGetProperty("spawns", out _)));

        var coins = ClaudeRun.J(await ClaudeTools.FindPrefabs("coins", items: true)).EnumerateArray().ToList();
        Assert.Contains(coins, i => i.GetProperty("item").GetString() == "Coins");

        var info = ClaudeRun.J(await ClaudeTools.PieceInfo(["woodwall", "no_such_piece"])).EnumerateArray().ToList();
        Assert.Equal(2, info.Count);
        Assert.Equal("hammer", info[0].GetProperty("tool").GetString());
        Assert.True(info[0].GetProperty("snaps").GetArrayLength() >= 4);
        var bounds = info[0].GetProperty("bounds");
        Assert.InRange(bounds.GetProperty("maxY").GetSingle() - bounds.GetProperty("minY").GetSingle(), 1.8f, 2.3f);
        Assert.Contains("not a building piece", info[1].GetProperty("error").GetString());

        string guide = ClaudeTools.BuildingGuide();
        Assert.StartsWith(ClaudeGuide.Building, guide);
        Assert.EndsWith(ClaudeGuide.Shaping, guide);
    }

    // An upright object at its own size is listed without tilts or size; a tilted, larger one with them.
    [AvaloniaFact]
    public async Task ObjectsAreListedWithTiltsAndSizeWhenTheyHaveThem()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 4, Z = 4 }]);
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "rock4_forest", X = -4, Z = 4, Pitch = 10 }]);
        int rock = ClaudeRun.J(await r.T.ListObjects(name: "rock4_forest", x: -4, z: 4, radius: 1))[0].GetProperty("id").GetInt32();
        await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = rock, Scale = 2 }]);

        var tree = ClaudeRun.J(await r.T.ListObjects(name: "Beech1", x: 4, z: 4, radius: 1))[0];
        Assert.False(tree.TryGetProperty("pitch", out _));
        Assert.False(tree.TryGetProperty("scale", out _));
        var tilted = ClaudeRun.J(await r.T.ListObjects(name: "rock4_forest", x: -4, z: 4, radius: 1))[0];
        Assert.Equal(10, tilted.GetProperty("pitch").GetSingle(), 1);
        Assert.Equal(0, tilted.GetProperty("roll").GetSingle(), 1);
        Assert.Equal(2, tilted.GetProperty("scale").GetSingle(), 3);
    }
}
