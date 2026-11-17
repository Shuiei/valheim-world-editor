using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.Terrain;
using Xunit;
using Run = TerrainEditor.Desktop.Tests.SharedPlaceWindow.PanelRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place panel's less common paths on the test world (zone 0, 0): Delete on a built-in preset, declined, or on one
/// gone from the memory; the note for kinds that need cultivated ground; the info line's end-to-end gap and the turned
/// grid; and a panel of its own with no one listening.
/// </summary>
[Collection("World files")]
public class PlacePanelBranchesTests
{
    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DeleteLeavesBuiltInDeclinedAndVanishedPresetsAlone()
    {
        using var r = new Run();
        int entries = r.P.PresetBox.ItemCount;
        // A built-in preset: not yours to delete.
        r.P.PresetBox.SelectedIndex = 1;
        Click(r.P.DeletePresetButton);
        Assert.Equal(entries, r.P.PresetBox.ItemCount);
        // One of yours, the question declined.
        SharedPlaceWindow.ChoosePanelKinds(r, "Beech1");
        r.P.AskName = () => Task.FromResult<string?>("Mine");
        Click(r.P.SavePresetButton);
        Assert.Single(r.P.Memory.Presets);
        r.P.Confirm = _ => Task.FromResult(false);
        Click(r.P.DeletePresetButton);
        Assert.Single(r.P.Memory.Presets);
        // Gone from the memory meanwhile: the entry points past the end, nothing is deleted.
        r.P.Memory.Presets.Clear();
        r.P.Confirm = _ => Task.FromResult(true);
        Click(r.P.DeletePresetButton);
        Assert.Equal(entries + 1, r.P.PresetBox.ItemCount);
    }

    [AvaloniaFact]
    public void TheNoteNamesEveryKindThatNeedsCultivatedGround()
    {
        using var r = new Run();
        SharedPlaceWindow.ChoosePanelKinds(r, "Beech1");
        Assert.Equal("", r.P.Note.Text);
        Assert.False(r.P.Note.IsVisible);
        SharedPlaceWindow.ChoosePanelKinds(r, "sapling_carrot", "sapling_turnip");
        Assert.Contains("sapling_carrot, sapling_turnip only grow on cultivated ground", r.P.Note.Text);
    }

    [AvaloniaFact]
    public void TheInfoTellsTheGapLeftEndToEndAndTheShapesTurn()
    {
        using var r = new Run();
        Click(r.P.ModeButtons[PlaceTool.Modes.Line]);
        // A piece set on the tool itself (the panel's list leaves the hammer's pieces to Build): the list read first, so
        // reading it does not take the piece away again.
        _ = r.P.Creatable();
        SharedPlaceWindow.ChoosePanelKinds(r, "woodwall");
        Assert.True(r.T.EndToEnd);
        r.T.SnapTo = false;
        // 5 m: two 2 m walls, 1 m left.
        r.T.Points.Add(new Vector2(20, 32));
        r.T.Points.Add(new Vector2(25, 32));
        r.T.Notify();
        Assert.Contains("end to end (1.0 m of the line left at the end", r.P.Info.Text);
        Click(r.P.ModeButtons[PlaceTool.Modes.Grid]);
        r.T.GridA = new Vector2(10, 10);
        r.T.GridB = new Vector2(30, 30);
        Assert.True(r.T.TurnShape(90));
        Assert.Contains("turn the grid (now 90°)", r.P.Info.Text);
        Click(r.P.ModeButtons[PlaceTool.Modes.Zone]);
        Assert.Contains("turn the polygon", r.P.Info.Text);
    }

    // The pieces players build with the hammer are the Build tool's: the Place panel's list leaves them out, keeps the
    // cultivator's saplings and crops (fields are planted with its grid) and everything of nature.
    [AvaloniaFact]
    public void TheHammersPiecesAreLeftToTheBuildTool()
    {
        using var r = new Run();
        Assert.True(PlacePanel.BuiltWithBuild("woodwall"));
        Assert.False(PlacePanel.BuiltWithBuild("Beech1"));
        string plant = BuildPanel.Pieces.First(p => p.Category == BuildPanel.Plants).Prefab;
        Assert.False(PlacePanel.BuiltWithBuild(plant));
        var offered = r.P.Creatable();
        Assert.NotEmpty(offered);
        Assert.DoesNotContain(offered, c => PlacePanel.BuiltWithBuild(c.Name));
        Assert.Contains(offered, c => c.Name == "Beech1");
        // Picked in the world, a wall is not taken: the panel says where it is put down.
        r.P.Choose("Beech1", add: false);
        r.P.Choose("woodwall", add: true);
        Assert.Equal(["Beech1"], r.T.Chosen);
        Click(r.P.PickButton);
        r.W.PlaceInput.PickOnce!("woodwall", false);
        Assert.Equal("woodwall is a building piece: put it down with Build (U).", r.Said);
        Assert.Equal(["Beech1"], r.T.Chosen);
    }

    // The brush plants at its own rate, not the sculpt brushes' strength.
    [AvaloniaFact]
    public void TheBrushHasItsOwnRate()
    {
        using var r = new Run();
        Click(r.P.ModeButtons[PlaceTool.Modes.Brush]);
        float strength = r.W.Tools.Brush.Strength;
        r.P.RateSlider.Value = 0.8;
        Assert.Equal(0.8f, r.T.Rate, 3);
        Assert.Equal(0.8f, r.P.Memory.Rate, 3);
        Assert.Equal(strength, r.W.Tools.Brush.Strength);
        // As it was (the tests' place.json is shared).
        r.P.RateSlider.Value = 0.5;
    }

    [AvaloniaFact]
    public void APanelOfItsOwnSaysNothingAndTellsNoOneOfOpenedGroups()
    {
        using var r = new Run();
        var input = new PlaceInput(r.W.View, new PlaceTool { Scene = () => r.Scene });
        var p = new PlacePanel(input, () => r.Scene, PrefabCatalog.NameOf, new PlaceMemory());
        Click(p.PickButton);
        Assert.NotNull(input.PickOnce);
        Click(p.KindsButton);
        var (kind, group) = p.Groups.First(g => !g.Value.IsExpanded);
        group.IsExpanded = true;
        Assert.Contains(kind, p.OpenKinds);
    }
}
