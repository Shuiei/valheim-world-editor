using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's Library: blueprints searched, opened alone, added and dropped onto the plot; and the Generate dungeon
/// making a dungeon of building pieces onto the plot.
/// </summary>
public class WorkshopLibraryTests
{
    // The Workshop's Library: Homestead's blueprints, searched, opened alone on the plot, added to it
    // (one undo step each), and dropped onto the view (from the Library, or files from the computer:
    // imported first; other files said so). The unsaved marks are off in the Workshop.
    [AvaloniaFact]
    public async Task TheLibraryOpensAddsAndTakesDrops()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        r.Keep("Gate", "woodwall", "wood_floor");
        r.Keep("Tower", "woodwall");
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        await w.OpenWorkshop(null);
        Assert.False(w.View.ShowNewMarkers);
        var bp = w.BuildPanel;
        bp.ShowLibrary(true);
        Assert.True(bp.LibraryShown);
        Assert.Equal(2, bp.LibraryList.Children.Count);
        bp.LibrarySearch.Text = "tow";
        Assert.Single(bp.LibraryList.Children);
        bp.LibrarySearch.Text = "";
        // Add: into the plot, with what is there.
        w.AddToWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"), null);
        Assert.Equal(2, Workshop.Pieces(w.Session!.Scene));
        w.AddToWorkshop(Path.Combine(r.Homestead, "Tower.blueprint"), new Vector2(5, 5));
        Assert.Equal(3, Workshop.Pieces(w.Session!.Scene));
        Assert.Contains(w.Session.Scene.Things, t => !t.Gone && MathF.Abs(t.Position.X - 5) < 0.01f && MathF.Abs(t.Position.Z - 5) < 0.01f);
        Assert.StartsWith("Added “Tower”: 1 piece(s).", w.MessageText.Text);
        w.Session.Undo();
        Assert.Equal(2, Workshop.Pieces(w.Session.Scene));
        // Dropped: from the Library, and a file from elsewhere (imported, then added).
        w.Drop(Path.Combine(r.Homestead, "Tower.blueprint"), [], new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
        Assert.Equal(3, Workshop.Pieces(w.Session.Scene));
        string other = Path.Combine(r.Dir, "Wall.vbuild");
        File.WriteAllText(other, BlueprintFormats.Write(System.Text.Json.Nodes.JsonNode.Parse(CopyFormat.ToJson(BlueprintsPanelRun.Clip("Wall", "woodwall", "woodwall")).ToJsonString())!.AsObject(), "vbuild", "Wall", _ => 0));
        w.Drop(null, [other, Path.Combine(r.Dir, "notes.txt")], new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
        Assert.Equal(5, Workshop.Pieces(w.Session.Scene));
        Assert.Contains(r.Listed, e => e.Name == "Wall");
        Assert.Equal(3, bp.LibraryList.Children.Count);
        w.Drop(null, [Path.Combine(r.Dir, "notes.txt")], new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
        Assert.Equal("Drop .blueprint (Homestead, PlanBuild) or .vbuild files.", w.MessageText.Text);
        // Open: that one alone (the plot was not saved: asked first).
        await w.OpenWorkshop(Path.Combine(r.Homestead, "Gate.blueprint"));
        Assert.Equal(2, Workshop.Pieces(w.Session.Scene));
        await w.LeaveWorkshop();
        Assert.Equal(w.UnsavedBox.IsChecked == true, w.View.ShowNewMarkers);
    }

    // The Library's Generate dungeon: the Dungeon panel's generator in the Build panel, made of building
    // pieces only (no "Made of", no Place in the world); Add to the plot puts it there with what is there
    // (one undo step), Open on the plot alone (asked first: the plot is not saved); its blueprint is kept
    // in the Library; back to the Library after.
    [AvaloniaFact]
    public async Task TheLibraryGeneratesADungeonOntoThePlot()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        r.Keep("Tower", "woodwall");
        int asked = 0;
        w.Ask = (_, _, _, _) => { asked++; return Task.FromResult(true); };
        await w.OpenWorkshop(null);
        var bp = w.BuildPanel;
        bp.ShowLibrary(true);
        bp.GenerateButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.True(bp.DungeonShown);
        Assert.False(bp.LibraryShown);
        Assert.Contains("on", bp.LibraryTab.Classes);
        var g = bp.Dungeon;
        Assert.False(((Avalonia.Controls.Control)g.Made.Parent!).IsVisible);
        Assert.Null(g.Place.Parent);
        Assert.True(g.OpenOnPlot.IsVisible && g.AddToPlot.IsVisible);
        // Small and quick to make.
        g.Size.SelectedIndex = 0;
        g.Levels.SelectedIndex = 0;
        await Until(g, () => g.AddToPlot.IsEnabled);
        g.AddToPlot.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Until(g, () => Workshop.Pieces(w.Session!.Scene) > 1);
        int pieces = Workshop.Pieces(w.Session!.Scene);
        Assert.StartsWith("Added “", w.MessageText.Text);
        // Its blueprint: every object (chests, creatures, torches too), the plot counts building pieces.
        Assert.Contains(r.Listed, e => e.Tags.Contains("dungeon") && e.Pieces >= pieces);
        Assert.Equal(0, asked);
        w.Session.Undo();
        Assert.Equal(0, Workshop.Pieces(w.Session.Scene));
        w.Session.Redo();
        // Open on the plot: the plot changed and is not saved, so asked; the dungeon alone on a new plot
        // (its blueprint numbered: the first one is kept).
        var before = w.Session;
        g.OpenOnPlot.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Until(g, () => asked == 1 && w.InWorkshop && w.Session != before);
        Assert.Equal(pieces, Workshop.Pieces(w.Session.Scene));
        Assert.Equal(2, r.Listed.Count(e => e.Tags.Contains("dungeon")));
        Assert.True(bp.DungeonShown);
        bp.BackToLibrary.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.True(bp.LibraryShown);
        Assert.False(bp.DungeonShown);
        Assert.Equal(3, bp.LibraryList.Children.Count);
    }

    /// <summary>
    /// Waits (a few seconds at most) for what the window does after a click, the generator's dungeon too.
    /// </summary>
    private static async Task Until(DungeonGenPanel g, Func<bool> done)
    {
        for (int i = 0; i < 300 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await g.Pending;
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }
}
