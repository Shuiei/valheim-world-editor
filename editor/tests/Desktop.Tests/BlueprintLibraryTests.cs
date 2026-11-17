using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The one blueprint library behind a world's Blueprints panel and the Workshop's Library tab: the same rows and
/// buttons in both (Paste in a world, Add in the Workshop), the Workshop's Details, .vbuild, Delete and To Homestead
/// done as the panel does them (the plot following its own blueprint), New blueprint from the dock, and the clipboard
/// pasted and saved as a blueprint from the Blueprints panel.
/// </summary>
public sealed class BlueprintLibraryTests
{
    /// <summary>Each button under a control: its label and its tip key.</summary>
    private static List<string> Buttons(Control list) => [.. list.GetLogicalDescendants().OfType<Button>().Select(b => $"{b.Content} {Tips.KeyOf(b)}")];

    /// <summary>The first button with this label under a control.</summary>
    private static Button Labelled(Control list, string label) => list.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == label);

    /// <summary>Lets what a click started finish (the Workshop opening), a few seconds at most.</summary>
    private static async Task Until(Func<bool> done)
    {
        for (int i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }

    [AvaloniaFact]
    public async Task BothPlacesShowTheSameRowsAndButtons()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        r.B.Store.Save("Old", "OtherWorld", null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Old", "woodwall")));
        r.B.Toggle(true);
        Assert.Equal(
        [
            "Paste blueprints.paste", "Open blueprints.open", "Details blueprints.details", ".vbuild blueprints.vbuild", "Delete blueprints.delete",
            "Paste blueprints.paste", "To Homestead blueprints.toHomestead", ".vbuild blueprints.vbuildOld", "Delete blueprints.deleteOld",
        ], Buttons(r.B.List));
        string world = SharedInspectorRun.Texts(r.B.List);

        var bp = (await r.OpenWorkshop()).BuildPanel;
        bp.ShowLibrary(true);
        // The plot takes a blueprint with Add (or dragged) where a world pastes it; an older one goes to Homestead first.
        Assert.Equal(
        [
            "Add build.add", "Open build.open", "Details blueprints.details", ".vbuild blueprints.vbuild", "Delete blueprints.delete",
            "To Homestead blueprints.toHomestead", ".vbuild blueprints.vbuildOld", "Delete blueprints.deleteOld",
        ], Buttons(bp.LibraryList));
        // The same name, pieces, cost and heading on each row.
        Assert.Equal(world, SharedInspectorRun.Texts(bp.LibraryList));
        Assert.Contains("Older editor blueprints", world);
        Assert.Contains("Cost: ", world);
        // The rows are the list's children (with the older ones' heading), a Homestead one known by its file.
        Assert.Equal(3, bp.LibraryList.Children.Count);
        Assert.Equal(Path.Combine(r.Homestead, "Gate.blueprint"), bp.LibraryList.Children.OfType<Border>().First().Tag);
        Assert.Same(bp.Library.ImportButton, bp.ImportButton);
        Assert.Equal(BlueprintLibrary.Places.Workshop, bp.Library.Place);
        Assert.Equal(BlueprintLibrary.Places.World, r.B.Library.Place);
    }

    [AvaloniaFact]
    public async Task TheWorkshopsLibraryChangesDetailsExportsMovesAndDeletes()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        r.Keep("Tower", "woodwall");
        string old = r.B.Store.Save("Old", null, null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Old", "woodwall")));
        var w = await r.OpenWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"));
        var bp = w.BuildPanel;
        bp.ShowLibrary(true);
        Border Row(string name) => bp.LibraryList.Children.OfType<Border>().Single(b => SharedInspectorRun.Texts(b).StartsWith(name + " |", StringComparison.Ordinal));

        // Details of another blueprint: its file follows its name; the plot keeps its own.
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Keep" });
        SharedButtons.Click(Labelled(Row("Tower"), "Details"));
        Assert.Contains(r.Listed, e => e.Name == "Keep");
        Assert.DoesNotContain("Keep", w.TitleText.Text);
        // Details of the blueprint on the plot: the Workshop takes its new name, and saves to its new file.
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "North gate" });
        SharedButtons.Click(Labelled(Row("Gate"), "Details"));
        Assert.Contains("North gate", w.TitleText.Text);
        Assert.Equal("North gate", w.WorkshopDetails!.Name);
        Assert.False(w.WorkshopDirty);
        // Cancelled: nothing changes.
        r.B.AskDetails = (_, _) => Task.FromResult<Homestead.Details?>(null);
        SharedButtons.Click(Labelled(Row("Keep"), "Details"));
        Assert.Equal(2, r.Listed.Count);

        // .vbuild: written into the export folder.
        SharedButtons.Click(Labelled(Row("Keep"), ".vbuild"));
        Assert.True(File.Exists(Path.Combine(r.B.Store.Directory, "export", "Keep.vbuild")));
        // To Homestead: the older one is a Homestead blueprint too, then added like the others.
        SharedButtons.Click(Labelled(bp.LibraryList, "To Homestead"));
        Assert.Contains(r.Listed, e => e.Name == "Old");
        // (The older one is kept as it was.)
        Assert.Single(bp.LibraryList.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "To Homestead");
        Assert.NotNull(r.B.Store.Read(old));

        // Delete, answered No: kept. Answered Yes: gone from the folder and the list.
        r.B.Confirm = _ => Task.FromResult(false);
        SharedButtons.Click(Labelled(Row("Keep"), "Delete"));
        Assert.Contains(r.Listed, e => e.Name == "Keep");
        r.B.Confirm = _ => Task.FromResult(true);
        SharedButtons.Click(Labelled(Row("Keep"), "Delete"));
        Assert.DoesNotContain(r.Listed, e => e.Name == "Keep");
        Assert.DoesNotContain("Keep |", SharedInspectorRun.Texts(bp.LibraryList));
        Assert.False(w.WorkshopDirty);
        // The plot's own blueprint deleted: the building there is no longer saved.
        SharedButtons.Click(Labelled(Row("North gate"), "Delete"));
        Assert.DoesNotContain(r.Listed, e => e.Name == "North gate");
        Assert.True(w.WorkshopDirty);
    }

    [AvaloniaFact]
    public async Task NewBlueprintInTheDockStartsABlankPlot()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        var w = await r.OpenWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"));
        Assert.Equal(2, Workshop.Pieces(w.Session!.Scene));
        var before = w.Session;
        w.BuildPanel.ShowLibrary(true);
        Assert.Equal("New blueprint blueprints.new", $"{w.BuildPanel.NewButton.Content} {Tips.KeyOf(w.BuildPanel.NewButton)}");
        Assert.True(((Control)w.BuildPanel.NewButton.Parent!).IsVisible);
        SharedButtons.Click(w.BuildPanel.NewButton);
        await Until(() => w.InWorkshop && w.Session != before);
        Assert.Equal(0, Workshop.Pieces(w.Session!.Scene));
        Assert.Null(w.WorkshopDetails);
    }

    [AvaloniaFact]
    public void TheClipboardIsPastedAndSavedFromTheBlueprintsPanel()
    {
        using var r = new BlueprintsPanelRun();
        r.B.Toggle(true);
        // Nothing copied yet: said so, both buttons off.
        Assert.StartsWith("Nothing copied yet.", r.B.ClipInfo.Text);
        Assert.False(r.B.PasteClipboardButton.IsEnabled);
        Assert.False(r.B.SaveClipboardButton.IsEnabled);
        Assert.Equal("blueprints.saveClipboard", Tips.KeyOf(r.B.SaveClipboardButton));

        // A copy (the Area tool's, or Ctrl+C in Select): what it holds, both buttons on.
        var clip = BlueprintsPanelRun.Clip("Gate", "woodwall");
        clip.Name = null;
        r.W.View.Paste.Clip = clip;
        r.W.View.Paste.Notify();
        Assert.Equal("3 × 2 m, 1 object(s).", r.B.ClipInfo.Text);
        Assert.True(r.B.PasteClipboardButton.IsEnabled);
        Assert.True(r.B.SaveClipboardButton.IsEnabled);

        // Save as blueprint…: asked its name, kept in Homestead's folder and listed; the clipboard takes the name.
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
        SharedButtons.Click(r.B.SaveClipboardButton);
        Assert.Equal("Gate", Assert.Single(r.Listed).Name);
        Assert.Single(r.RowButtons("Paste"));
        Assert.Equal("“Gate”: 3 × 2 m, 1 object(s).", r.B.ClipInfo.Text);

        // Paste: the panel closes and the window pastes the clipboard.
        SharedButtons.Click(r.B.PasteClipboardButton);
        Assert.False(r.B.Card.IsVisible);
        Assert.Equal(ToolMode.Paste, r.W.Tools.Mode);

        // Emptied: off again.
        r.W.View.Paste.Clip = null;
        r.W.View.Paste.Notify();
        Assert.False(r.B.SaveClipboardButton.IsEnabled);
    }
}
