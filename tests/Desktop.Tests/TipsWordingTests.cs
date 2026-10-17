using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the tips say: the keys named as the help names them, the area actions explained on the list and the button,
/// and a row's label telling what its control does.
/// </summary>
[Collection("World files")]
public class TipsWordingTests
{
    [AvaloniaFact]
    public void TheTipsNameTheKeysAsTheHelpDoes()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        static string Tip(Control c) => ToolTip.GetTip(c) as string ?? "";
        Assert.Contains("[ and ]", Tip(w.Tools.SizeSlider));
        Assert.Contains("[ and ]", Tip(w.PlacePanel.SizeSlider));
        Assert.Contains(", and .", Tip(w.PlacePanel.RotationSlider));
        Assert.Contains("(R)", Tip(w.PastePanel.TurnButton));
        Assert.Contains("(F)", Tip(w.PastePanel.MirrorButton));
        Assert.Contains("(Esc)", Tip(w.PastePanel.DoneButton));
        Assert.Contains("(L)", Tip(w.HistoryButton));
        Assert.Contains("(V)", Tip(w.ViewButton));
        Assert.Contains("(?)", Tip(w.HelpButton));
        Assert.Contains("(Ctrl+Z)", Tip(w.UndoButton));
        Assert.Contains("(Ctrl+S)", Tip(w.SaveButton));
        Assert.Contains("(Del)", Tip(w.SelectPanel.DeleteButton));
        Assert.Contains("(I)", Tip(w.SelectPanel.InspectButton));
        Assert.Contains("(Enter)", Tip(w.PathPanel.ApplyButton));
        Assert.Contains("(Esc)", Tip(w.PathPanel.ClearButton));
        Assert.Contains("(Ctrl+C)", Tip(w.AreaPanel.CopyButton));
        Assert.Contains("(Ctrl+V)", Tip(w.AreaPanel.PasteButton));
        Assert.Contains("(Backspace)", Tip(w.PlacePanel.UndoPointButton));
        Assert.Contains("(R)", Tip(w.PlacePanel.NewLayoutButton));
        Assert.Contains("PgUp / PgDn", Tip(w.PlacePanel.ElevationBox));
        Assert.Contains("Alt + click", Tip(w.Tools.TargetBox));
        Assert.Contains("Alt + Shift + click", Tip(w.MaskPanel.HeightMin));
        // The rail's buttons name their key.
        Assert.StartsWith("Raise (1)", Tip(w.Tools.ButtonOf(BrushTool.Raise)));
        Assert.StartsWith("Select (E)", Tip(w.Tools.SelectButton));
    }

    [AvaloniaFact]
    public void TheAreaActionsExplainThemselvesOnTheListAndTheButton()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var a = w.AreaPanel;
        var seen = new HashSet<string>();
        for (int i = 0; i < a.ActionBox.ItemCount; i++)
        {
            a.ActionBox.SelectedIndex = i;
            string button = (string)ToolTip.GetTip(a.ApplyButton)!;
            string list = (string)ToolTip.GetTip(a.ActionBox)!;
            Assert.EndsWith("(Enter). Ctrl+Z undoes it.", button);
            Assert.StartsWith(Tips.Of("area.action"), list);
            // The list says what the button does.
            Assert.Contains(button[..^" (Enter). Ctrl+Z undoes it.".Length], list);
            seen.Add(button);
        }
        // Each action its own words.
        Assert.Equal(a.ActionBox.ItemCount, seen.Count);
    }

    [AvaloniaFact]
    public void ARowsLabelTellsWhatItsControlDoes()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        foreach (var input in new Control[] { w.Tools.SizeSlider, w.PathPanel.WidthSlider, w.AreaPanel.SoftSlider, w.ShapePanel.RadiusSlider, w.PastePanel.GapBox, w.PlacePanel.DensitySlider, w.SelectPanel.XBox })
        {
            var row = (Panel)input.Parent!;
            var label = row.Children.OfType<TextBlock>().First();
            Assert.Equal(ToolTip.GetTip(input), ToolTip.GetTip(label));
        }
    }
}
