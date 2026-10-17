using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop in the window: opening one, the support check (on and off), saving it as a Homestead blueprint, leaving
/// (asking when it is not saved), and the Workshop keeping to building until it is left.
/// </summary>
public class WorkshopWindowTests
{
    [AvaloniaFact]
    public async Task TheWorkshopOpensChecksSupportSavesAndAsksBeforeLeaving()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        string path = BlueprintFiles.Hut(r.Homestead);
        var asked = new List<string>();
        bool answer = false;
        w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
        w.Tell = _ => Task.CompletedTask;
        await w.OpenWorkshop(path);
        Assert.True(w.InWorkshop);
        Assert.True(w.SupportBox.IsVisible);
        Assert.Contains("Opened “Hut”: 3 piece(s)", w.MessageText.Text);
        Assert.Contains("no_such_piece_xyz", w.MessageText.Text);
        // The floating floor would fall; the rest stands.
        var support = w.LastSupport!;
        Assert.Equal(1, support.Breaking);
        Assert.NotNull(w.View.Support);
        Assert.Contains("1 would fall", w.PendingText.Text);
        Assert.Contains("saved", w.PendingText.Text);
        w.SupportBox.IsChecked = false;
        Assert.Null(w.LastSupport);
        Assert.Null(w.View.Support);
        w.SupportBox.IsChecked = true;
        Assert.NotNull(w.LastSupport);
        // Something changed: leaving asks, and staying stays.
        w.Session!.Commit("Moved", null, [w.Session.Scene.Things.FindIndex(t => !t.Gone)], []);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("not saved as a blueprint", w.PendingText.Text);
        await w.LeaveWorkshop();
        Assert.Equal("Leave the Workshop", Assert.Single(asked));
        Assert.True(w.InWorkshop);
        // Saving asks first, since a piece would fall; then writes the building only.
        // The details asked start from the blueprint's own, with its cost.
        string? costAsked = null;
        r.B.AskDetails = (d, cost) => { costAsked = cost; return Task.FromResult<Homestead.Details?>(d with { Name = "Hut 2", Tags = ["hut"] }); };
        Assert.StartsWith("Wood ", w.WorkshopCost);
        answer = true;
        await w.SaveWorkshop();
        Assert.Equal("Save blueprint", asked[^1]);
        var saved = Homestead.List(r.Homestead).Single(e => e.Name == "Hut 2");
        Assert.Equal(2, saved.Pieces);
        Assert.Equal(new[] { "hut" }, saved.Tags);
        Assert.StartsWith("Wood ", costAsked);
        Assert.NotNull(saved.Picture);
        Assert.Contains("· saved", w.PendingText.Text);
        // Saved: leaving does not ask.
        answer = false;
        int before = asked.Count;
        await w.LeaveWorkshop();
        Assert.Equal(before, asked.Count);
        Assert.False(w.InWorkshop);
        Assert.False(w.SupportBox.IsVisible);
    }

    // The Workshop keeps to building: Build, Select and View on the rail (other tools and keys do
    // nothing there), the Build panel instead of Place's, no Mask, View panel or zone borders; leaving
    // gives the Place tool and the rest back as they were.
    [AvaloniaFact]
    public async Task TheWorkshopOnlyBuildsAndGivesTheToolsBackWhenLeft()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.PlaceTool.Chosen.Clear();
        w.PlaceTool.Chosen.Add("Beech1");
        w.PlaceTool.RandomYaw = true;
        w.View.SetOverlay(Overlays.Layer.Borders, true);
        await w.OpenWorkshop(null);
        Assert.True(w.Tools.Workshop);
        Assert.Equal(ToolMode.Place, w.Tools.Mode);
        Assert.True(w.BuildPanel.Card.IsVisible);
        Assert.False(w.PlacePanel.Card.IsVisible);
        Assert.False(w.MaskPanel.Card.IsVisible);
        Assert.False(w.ViewButton.IsVisible);
        Assert.False(w.View.IsOverlayShown(Overlays.Layer.Borders));
        // A building piece, one at a time, facing the turn.
        Assert.Equal(new[] { "woodwall" }, w.PlaceTool.Chosen);
        Assert.True(w.PlaceTool.Building && w.PlaceTool.OneAtATime && !w.PlaceTool.RandomYaw);
        // World tools are not there.
        w.Tools.ChooseMode(ToolMode.Mountain);
        w.Tools.Choose(BrushTool.Raise);
        Assert.Equal(ToolMode.Place, w.Tools.Mode);
        w.Tools.ChooseSelect();
        Assert.Equal(ToolMode.Select, w.Tools.Mode);
        Assert.False(w.BuildPanel.Card.IsVisible);
        w.Tools.ChooseMode(ToolMode.Place);
        // Choosing a piece, the grid and the turn step.
        w.BuildPanel.Choose("stone_wall_2x1");
        Assert.Equal(new[] { "stone_wall_2x1" }, w.PlaceTool.Chosen);
        Assert.Contains("Stone 4", w.BuildPanel.Cost.Text);
        w.BuildPanel.GridBox.SelectedIndex = 3;
        Assert.Equal(2, w.PlaceTool.GridStep);
        w.BuildPanel.TurnBox.SelectedIndex = 3;
        Assert.True(w.PlaceInput.Key(Avalonia.Input.Key.OemPeriod, false, false));
        Assert.Equal(45, w.PlaceTool.Rotation);
        // Everything players build: the cultivator's and the serving tray's pieces too.
        Assert.Contains(BuildPanel.Pieces, p => p.Category == BuildPanel.Plants);
        Assert.Contains(BuildPanel.Pieces, p => p.Category == BuildPanel.Feasts);
        w.BuildPanel.SnapBox.IsChecked = false;
        Assert.False(w.PlaceTool.SnapTo);
        await w.LeaveWorkshop();
        Assert.False(w.Tools.Workshop);
        Assert.Equal(new[] { "Beech1" }, w.PlaceTool.Chosen);
        Assert.True(w.PlaceTool.RandomYaw && w.PlaceTool.SnapTo && !w.PlaceTool.Building);
        Assert.Equal(0, w.PlaceTool.GridStep);
        Assert.Null(w.PlaceInput.TurnStep);
        Assert.True(w.ViewButton.IsVisible);
        Assert.True(w.View.IsOverlayShown(Overlays.Layer.Borders));
    }
}
