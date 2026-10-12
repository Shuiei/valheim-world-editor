using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A blueprint with Homestead's terrain contacts (where its pieces touched the ground in game): the support check
/// takes the ground from them, so a floor high up that stood on a hillside holds; contacts that do not read are
/// skipped.
/// </summary>
public class WorkshopContactsTests
{
    [AvaloniaFact]
    public async Task TerrainContactsHoldWhatStoodOnTheGroundInGame()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.Tell = _ => Task.CompletedTask;
        Directory.CreateDirectory(r.Homestead);
        string path = Path.Combine(r.Homestead, Homestead.FileName("Hillside"));
        File.WriteAllText(path, "#Name:Hillside\n#Creator:test\n#HomesteadVersion:1\n"
            // The ground under the high floor, in game; one that is no number and one too short, skipped.
            + "#HomesteadTerrainContact:0;12;0\n#HomesteadTerrainContact:a;b;c\n#HomesteadTerrainContact:1;2\n"
            + "#Pieces\n"
            + "wood_floor;Building;0;0;0;0;0;0;1;\"\";1;1;1\n"
            + "wood_floor;Building;0;12;0;0;0;0;1;\"\";1;1;1\n");
        await w.OpenWorkshop(path);
        Assert.Contains("Opened “Hillside”: 2 piece(s)", w.MessageText.Text);
        Assert.Equal(0, w.LastSupport!.Breaking);
    }
}
