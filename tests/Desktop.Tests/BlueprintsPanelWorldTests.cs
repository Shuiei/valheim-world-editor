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
}
