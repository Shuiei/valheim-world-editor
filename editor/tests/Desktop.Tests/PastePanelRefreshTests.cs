using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Paste panel's info line: nothing copied says so, and a copied area is named with its size and turn.
/// </summary>
[Collection("DataDir")]
public sealed class PastePanelRefreshTests
{
    [AvaloniaFact]
    public void WithNothingCopiedTheInfoSaysSo()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.PastePanel.Refresh();
        Assert.Equal(Lang.T("paste.info.none"), w.PastePanel.Info.Text);
    }

    [AvaloniaFact]
    public void ACopiedObjectIsNamedInTheInfo()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(40, 30, 40))));
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.Copy();
        w.StartPaste();
        w.View.Paste.Clip!.Name = "My hut";
        w.PastePanel.Refresh();
        Assert.Contains("My hut", w.PastePanel.Info.Text, StringComparison.Ordinal);
        Assert.NotEqual(Lang.T("paste.info.none"), w.PastePanel.Info.Text);
    }

    // A blueprint of pieces has no ground, so a grid of one point: its size is its outline's (the pieces 10 m apart and
    // a metre round them), not 1 × 1 m.
    [AvaloniaFact]
    public void ABlueprintWithoutGroundGivesItsOutlinesSize()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        var bp = new BlueprintFormats.Parsed("Hut", null, null,
            [new("woodwall", new Vector3(0, 0, 0), Vector3.Zero, 1), new("woodwall", new Vector3(10, 0, 4), Vector3.Zero, 1)], [], 0);
        w.View.Paste.Clip = CopyFormat.FromJson(BlueprintFormats.ToClip(bp, _ => true, out _), "Hut");
        w.PastePanel.Refresh();
        Assert.StartsWith("Hut: 12 × 6 m, 2 object(s)", w.PastePanel.Info.Text, StringComparison.Ordinal);
    }
}
