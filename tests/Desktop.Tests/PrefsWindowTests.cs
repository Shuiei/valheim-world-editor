using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A window closed and opened again remembers what the editor kept: the View switches, the brush, the Area action, Select's
/// options, the Shape preset and formula, the right-hand panel, the clipboard and the map's place as they were. The tests'
/// windows remember in memory only unless given a file.
/// </summary>
public sealed class PrefsWindowTests : PrefsFolder
{
    /// <summary>
    /// A shown main window remembering in the test's prefs file.
    /// </summary>
    private MainWindow Window()
    {
        var w = new MainWindow(load: false, prefs: Prefs.Load(File1)) { Width = 1200, Height = 900 };
        w.Show();
        return w;
    }

    /// <summary>
    /// A small 2 × 2 clipboard with a missing height, mixed weights and paint (four values a point), to see it
    /// remembered as it was.
    /// </summary>
    private static CopyData Clip() => new()
    {
        W = 2,
        H = 2,
        Rel = [0f, 1f, float.NaN, 2f],
        Wt = [1f, 1f, 0f, 0.5f],
        Pnt = [-1f, -1, -1, -1, 0f, 0.5f, 1f, 0, 0.5f, 0.5f, 0, 0, 1f, 0, 0, 0],
        Objects = [new(Save.StableHash.Of("Beech1"), "Beech1", 1, 1, 0, new Vector3(0, 90, 0), 1, 1234)],
        Poly = [new(0, 0), new(2, 0), new(2, 2)],
        Name = "kept",
    };

    [AvaloniaFact]
    public void TheEditorsChoicesComeBackInTheNextWindow()
    {
        var w = Window();
        var kind = w.KindBoxes.Keys.First();
        w.KindBoxes[kind].IsChecked = !w.KindBoxes[kind].IsChecked;
        bool kindOn = w.KindBoxes[kind].IsChecked == true;
        w.WaterBox.IsChecked = false;
        var layer = w.OverlayBoxes.Keys.Last();
        w.OverlayBoxes[layer].IsChecked = !w.OverlayBoxes[layer].IsChecked;
        bool layerOn = w.OverlayBoxes[layer].IsChecked == true;
        w.SlopeBox.IsChecked = true;
        w.ContourBox.IsChecked = true;
        w.ContourStepBox.SelectedIndex = 3;
        w.Tools.ShapeBox.SelectedIndex = 2;
        w.Tools.FalloffBox.SelectedIndex = 4;
        w.AreaPanel.Choose(AreaPanel.Act.Regrow);
        w.SelectPanel.GroundBox.IsChecked = false;
        w.SelectPanel.SnapBox.IsChecked = false;
        w.SelectPanel.ZoneBox.SelectedIndex = 1;
        w.ShapePanel.PresetBox.SelectedIndex = 1;
        w.ShowRight(w.HelpCard);
        w.Prefs.Flush();

        var v = Window();
        Assert.Equal(kindOn, v.KindBoxes[kind].IsChecked);
        Assert.Equal(kindOn, v.View.IsShown(kind));
        Assert.False(v.WaterBox.IsChecked);
        Assert.Equal(layerOn, v.OverlayBoxes[layer].IsChecked);
        Assert.Equal(layerOn, v.View.IsOverlayShown(layer));
        Assert.True(v.SlopeBox.IsChecked);
        Assert.True(v.View.SlopeColours);
        Assert.True(v.ContourBox.IsChecked);
        Assert.Equal(3, v.ContourStepBox.SelectedIndex);
        Assert.Equal(10, v.View.ContourStep);
        Assert.Equal(2, v.Tools.ShapeBox.SelectedIndex);
        Assert.Equal(4, v.Tools.FalloffBox.SelectedIndex);
        Assert.Equal(AreaPanel.Act.Regrow, v.AreaPanel.Current);
        Assert.False(v.SelectPanel.GroundBox.IsChecked);
        Assert.False(v.View.SelectTool.OnGround);
        Assert.False(v.View.SelectTool.SnapToPieces);
        Assert.True(v.View.SelectTool.ZoneOnGround);
        Assert.Equal(1, v.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal(ShapePanel.Presets[1].Formula, v.ShapePanel.FormulaBox.Text);
        Assert.True(v.HelpCard.IsVisible);
    }

    [AvaloniaFact]
    public void AFormulaOfOnesOwnComesBack()
    {
        var w = Window();
        w.ShapePanel.FormulaBox.Text = "3 * (1 - d / r)";
        Assert.Equal(ShapePanel.Presets.Length, w.ShapePanel.PresetBox.SelectedIndex);
        w.Prefs.Flush();
        var v = Window();
        Assert.Equal(ShapePanel.Presets.Length, v.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal("3 * (1 - d / r)", v.ShapePanel.FormulaBox.Text);
        // Back to a preset: its own formula, the one of one's own still kept for later.
        v.ShapePanel.PresetBox.SelectedIndex = 0;
        v.Prefs.Flush();
        var u = Window();
        Assert.Equal(0, u.ShapePanel.PresetBox.SelectedIndex);
        Assert.Equal(ShapePanel.Presets[0].Formula, u.ShapePanel.FormulaBox.Text);
        Assert.Equal("3 * (1 - d / r)", u.Prefs.Get("shape.formula", ""));
    }

    [AvaloniaTheory]
    [InlineData("history")]
    [InlineData("none")]
    [InlineData("view")]
    [InlineData("something else")]
    public void TheRightHandPanelOpenComesBack(string which)
    {
        var p = Prefs.Load(File1);
        p.Set("panel.right", which);
        p.Flush();
        var v = Window();
        Assert.Equal(which == "history", v.History.Card.IsVisible);
        Assert.Equal(which is "view" or "something else", v.ViewButton.Classes.Contains("on"));
        Assert.False(v.HelpCard.IsVisible);
        // Read back as it was shown (an unknown one: the View panel); the Inspector and Blueprints are
        // not remembered: what was before stays.
        v.ShowRight(v.Inspector.Card);
        Assert.Equal(which == "something else" ? "view" : which, v.Prefs.Get("panel.right", ""));
        v.ShowRight(null);
        Assert.Equal("none", v.Prefs.Get("panel.right", ""));
    }

    [AvaloniaFact]
    public void TheClipboardComesBackWithoutItsSourceIds()
    {
        var w = Window();
        var c = Clip();
        w.View.Paste.Clip = c;
        w.View.Paste.Notify();
        // Notified again with the same copy (an offset changed): written once.
        w.View.Paste.Notify();
        w.Prefs.Flush();
        var v = Window();
        var back = v.View.Paste.Clip;
        Assert.NotNull(back);
        Assert.Equal(2, back!.W);
        Assert.True(float.IsNaN(back.Rel[2]));
        Assert.Equal("Beech1", Assert.Single(back.Objects).Name);
        Assert.Null(back.Objects[0].SourceId);
        Assert.Equal(90, back.Objects[0].Rotation.Y, 3);
        Assert.Equal(3, back.Poly.Count);
        Assert.Equal("kept", back.Name);
        // Pasting works from it at once (Ctrl+V needs no copy first).
        Assert.NotNull(v.View.Paste.Clip);
    }

    [AvaloniaFact]
    public void ABrokenClipboardIsForgotten()
    {
        var p = Prefs.Load(File1);
        p.Set("clipboard", new JsonObject { ["w"] = 2, ["h"] = 2 });
        p.Flush();
        var v = Window();
        Assert.Null(v.View.Paste.Clip);
        Assert.False(v.Prefs.Has("clipboard"));
        // Not even an object: ignored.
        p = Prefs.Load(File1);
        p.Set("clipboard", "text");
        p.Flush();
        Assert.Null(Window().View.Paste.Clip);
    }

    [AvaloniaFact]
    public void AnEntryNoLongerOfferedLeavesTheDefault()
    {
        var p = Prefs.Load(File1);
        p.Set("brush.shape", "Stamp: gone");
        p.Set("area.action", "An action that went away");
        p.Set("view.contourStep", "7");
        p.Set("view.slope", "not a bool");
        p.Flush();
        var v = Window();
        Assert.Equal(0, v.Tools.ShapeBox.SelectedIndex);
        Assert.Equal(AreaPanel.Act.Flatten, v.AreaPanel.Current);
        Assert.Equal(1, v.ContourStepBox.SelectedIndex);
        Assert.False(v.SlopeBox.IsChecked);
    }

    [AvaloniaFact]
    public void TestWindowsRememberNothingOnDisk()
    {
        var w = new MainWindow(load: false);
        w.SlopeBox.IsChecked = true;
        w.Prefs.Flush();
        Assert.False(new MainWindow(load: false).SlopeBox.IsChecked);
    }
}
