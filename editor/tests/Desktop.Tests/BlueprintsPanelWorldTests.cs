using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A blueprint pasted into a world that cannot make some of its kinds (opens the test world).
/// </summary>
[Collection("World files")]
public class BlueprintsPanelWorldTests
{
    [AvaloniaFact]
    public void KindsTheWorldCannotMakeAreLeftOutWhenPasting()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            using var r = new BlueprintsPanelRun(scene.Session);
            r.B.Store.Save("Odd", null, null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Odd", "Beech1", "NoSuchPiece_xyz")));
            r.B.Paste(r.B.Store.List()[0].Id);
            Assert.Contains("1 kind(s) are unknown here and are left out: NoSuchPiece_xyz", r.Said);
            Assert.DoesNotContain(r.W.View.Paste.Clip!.Objects, o => o.Name == "NoSuchPiece_xyz");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ABlueprintTaggedADungeonIsPastedAsOne()
    {
        using var r = new BlueprintsPanelRun();
        Directory.CreateDirectory(r.Homestead);
        var clip = BlueprintsPanelRun.Clip("Crypt", "stone_wall_2x1", "portal_wood");
        File.WriteAllText(Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Crypt")),
            TerrainEditor.App.Homestead.Write(CopyFormat.ToJson(clip), "Crypt", "test", null, DateTime.Now, "A crypt", ["Dungeon", "stone"]));
        r.Keep("Hut", "stone_wall_2x1");
        r.B.Paste("hs:" + TerrainEditor.App.Homestead.FileName("Crypt"));
        Assert.True(r.W.View.Paste.Clip!.Dungeon);
        Assert.Contains("click where its entrance goes", r.Said);
        r.B.Paste("hs:" + TerrainEditor.App.Homestead.FileName("Hut"));
        Assert.False(r.W.View.Paste.Clip!.Dungeon);
        Assert.Contains("click to place it", r.Said);
    }
}
