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
}
