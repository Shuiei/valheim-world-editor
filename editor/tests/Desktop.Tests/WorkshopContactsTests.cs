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
        Directory.CreateDirectory(r.Homestead);
        string path = Path.Combine(r.Homestead, Homestead.FileName("Hillside"));
        File.WriteAllText(path, "#Name:Hillside\n#Creator:test\n#HomesteadVersion:1\n"
            // The ground under the high floor, in game; one that is no number and one too short, skipped.
            + "#HomesteadTerrainContact:0;12;0\n#HomesteadTerrainContact:a;b;c\n#HomesteadTerrainContact:1;2\n"
            + "#Pieces\n"
            + "wood_floor;Building;0;0;0;0;0;0;1;\"\";1;1;1\n"
            + "wood_floor;Building;0;12;0;0;0;0;1;\"\";1;1;1\n");
        await r.OpenWorkshop(path);
        Assert.Contains("Opened “Hillside”: 2 piece(s)", w.MessageText.Text);
        Assert.Equal(0, w.LastSupport!.Breaking);
    }

    [Theory]
    [InlineData("x", "2", "3")]
    [InlineData("1", "y", "3")]
    [InlineData("1", "2", "z")]
    [InlineData("1", "2", "")]
    public void AContactWithAFieldThatIsNoNumberIsNoPoint(string x, string y, string z)
    {
        Assert.False(Workshop.TryPoint([x, y, z], out var p));
        Assert.Equal(default, p);
    }

    [Fact]
    public void AContactsPointIsReadWhateverTheCulture()
    {
        // Its fields as the game writes them, with a dot, and a fourth field (the piece) left alone.
        Assert.True(Workshop.TryPoint(["1.5", "-2", "1e1", "wood_floor"], out var p));
        Assert.Equal(new System.Numerics.Vector3(1.5f, -2, 10), p);
    }
}
