using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's get_object on an object of the world whose saved bytes cannot be read any more (its chunk file is gone, as
/// when the game removes an older save): the answer says its values cannot be read, and nothing is thrown.
/// </summary>
[Collection("World files")]
public class ClaudeToolsObjectValuesTests
{
    [AvaloniaFact]
    public async Task AnObjectWhoseFileIsGoneHasNoValuesToGive()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int id = ClaudeRun.J(await r.T.ListObjects())[0].GetProperty("id").GetInt32();
        var thing = r.W.Session!.Scene.Things[id];
        var world = r.W.World!.World;
        File.Delete(Path.Combine(world.Directory, world.ObjectRefs[thing.Id].File.FileName));

        var got = ClaudeRun.J(await r.T.GetObject(id));

        Assert.Equal("Its values cannot be read.", got.GetProperty("note").GetString());
        Assert.False(got.TryGetProperty("values", out _));
    }
}
