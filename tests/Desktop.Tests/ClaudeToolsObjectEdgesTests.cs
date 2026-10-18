using System.Numerics;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's get_object and add_blueprint at their edges: no area open, a piece on the Workshop's plot (its values are
/// those it was added with), a runestone (no values of its own), and one coordinate alone.
/// </summary>
[Collection("World files")]
public class ClaudeToolsObjectEdgesTests
{
    [AvaloniaFact]
    public async Task WithoutAnAreaThereIsNoObjectToGive()
    {
        using var r = new ClaudeRun();
        Assert.Equal("No area is open.", await r.T.GetObject(0));
    }

    [AvaloniaFact]
    public async Task APieceOnThePlotGivesTheValuesItWasAddedWith()
    {
        using var r = new ClaudeRun();
        r.Hut("Hut", "A small wooden hut", "house");
        await r.T.OpenWorkshop("Hut");
        int id = ClaudeRun.J(await r.T.ListObjects())[0].GetProperty("id").GetInt32();

        var got = ClaudeRun.J(await r.T.GetObject(id));

        Assert.Equal(JsonValueKind.Array, got.GetProperty("values").ValueKind);
        Assert.False(got.TryGetProperty("note", out _));
    }

    [AvaloniaFact]
    public async Task ARunestoneHoldsNoValuesOfItsOwn()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        // A runestone is a location, not a piece: put in the area as the game would have it.
        var s = r.W.Session!;
        s.Commit("Place", null, [], [(new NewObject(0, StableHash.Of("Runestone_Meadows"), new Vector3(3, 30, 3), Vector3.Zero, 0f), false)]);
        int id = ClaudeRun.J(await r.T.ListObjects(name: "Runestone_Meadows"))[0].GetProperty("id").GetInt32();

        var got = ClaudeRun.J(await r.T.GetObject(id));

        Assert.StartsWith("A runestone holds no values of its own", got.GetProperty("note").GetString());
        Assert.False(got.TryGetProperty("values", out _));
    }

    [AvaloniaFact]
    public async Task ABlueprintNeedsBothCoordinatesOrNeither()
    {
        using var r = new ClaudeRun();
        r.Hut("Hut", "A small wooden hut", "house");
        await r.OpenArea();

        Assert.Equal("Give x and z together, or neither: only x was given.", await r.T.AddBlueprint("Hut", x: 5));
        Assert.Equal("Give x and z together, or neither: only z was given.", await r.T.AddBlueprint("Hut", z: 5));
    }
}
