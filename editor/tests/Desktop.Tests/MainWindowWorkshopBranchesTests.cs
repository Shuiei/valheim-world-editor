using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's rarer ways in the window: blueprints and files dragged over and dropped onto the view (in a world
/// only imported), added where the view's middle points, outside the Workshop not at all, or from a file that cannot
/// be read; a building not saved kept when another is opened and the answer is Stay; Save blueprint with nothing on
/// the plot, with pieces that would fall, cancelled, and with no plot; a file of another mod opened; the Library's
/// Import file…; scaled pieces in the support check, a kind with no recipe in the cost, and a support check queued as
/// the Workshop closes.
/// </summary>
public class MainWindowWorkshopBranchesTests
{
    /// <summary>A Workshop run with the Tower blueprint kept in Homestead and the window's questions answered Yes.</summary>
    private static BlueprintsPanelRun RunWithTowerKept()
    {
        var r = new BlueprintsPanelRun();
        r.W.Ask = (_, _, _, _) => Task.FromResult(true);
        r.Keep("Tower", "woodwall");
        return r;
    }

    /// <summary>A drag over the view: the effect it is given.</summary>
    private static DragDropEffects Over(MainWindow w, IDataTransfer data)
    {
        var e = new DragEventArgs(DragDrop.DragOverEvent, data, w.Surface, new Avalonia.Point(5, 5), KeyModifiers.None);
        w.Surface.RaiseEvent(e);
        return e.DragEffects;
    }

    /// <summary>A drop onto the view.</summary>
    private static void Dropped(MainWindow w, IDataTransfer data) =>
        w.Surface.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, w.Surface, new Avalonia.Point(5, 5), KeyModifiers.None));

    [AvaloniaFact]
    public async Task BlueprintsAndFilesAreDraggedOverAndDroppedOntoTheView()
    {
        using var r = RunWithTowerKept();
        var w = r.W;
        using var library = new DataTransfer();
        library.Add(DataTransferItem.Create(BuildPanel.BlueprintFormat, Path.Combine(r.Homestead, "Tower.blueprint")));
        // Files from the computer (only their kind is looked at while dragging).
        using var files = new DataTransfer();
        var file = new DataTransferItem();
        file.Set(DataFormat.File, () => null!);
        files.Add(file);
        // In a world: a Library blueprint is not taken, files are (only imported).
        Assert.Equal(DragDropEffects.None, Over(w, library));
        Assert.Equal(DragDropEffects.Copy, Over(w, files));
        int things = w.Session!.Scene.Things.Count;
        w.Drop(null, [BlueprintFiles.Vbuild(r.Dir, "Wall", "woodwall")], new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
        Assert.Contains(r.Listed, e => e.Name == "Wall");
        Assert.Equal(things, w.Session.Scene.Things.Count);
        // In the Workshop: the Library's blueprint is put on the plot.
        await w.OpenWorkshop(null);
        Assert.Equal(DragDropEffects.Copy, Over(w, library));
        Dropped(w, library);
        Assert.Equal(1, Workshop.Pieces(w.Session!.Scene));
        Assert.StartsWith("Added “Tower”: 1 piece(s).", w.StatusMessage);
    }

    [AvaloniaFact]
    public async Task BlueprintsGoWhereTheViewPointsAndOnlyOntoThePlot()
    {
        using var r = RunWithTowerKept();
        var w = r.W;
        string tower = Path.Combine(r.Homestead, "Tower.blueprint");
        // Not in the Workshop: nothing is added to the world's area.
        int things = w.Session!.Scene.Things.Count;
        w.AddToWorkshop(tower, null);
        Assert.Equal(things, w.Session.Scene.Things.Count);
        await w.OpenWorkshop(null);
        var s = w.Session!;
        // Dropped in the view's middle, which looks 10 m east of the plot's middle.
        w.View.SetCamera(new Vector3(10, 200, 1), new Vector3(10, 0, 0), 1);
        w.Drop(tower, [], new Avalonia.Point(500, 500), new Avalonia.Size(1000, 1000));
        var placed = s.Scene.Things.Single(t => !t.Gone);
        Assert.InRange(placed.Position.X, Workshop.Anchor(s.Scene).X + 9, Workshop.Anchor(s.Scene).X + 11);
        // Added twice more in the middle: the ground each stood on is kept (the lowest).
        w.AddToWorkshop(tower, null);
        w.AddToWorkshop(tower, null);
        Assert.Equal(3, Workshop.Pieces(s.Scene));
        // Kinds the game does not know are left out, and said.
        w.AddToWorkshop(BlueprintFiles.Hut(r.Homestead), null);
        Assert.Contains("1 kind(s) the game does not know were left out (mods?): no_such_piece_xyz.", w.StatusMessage);
        // A file that is not there.
        w.AddToWorkshop(Path.Combine(r.Dir, "missing.blueprint"), null);
        Assert.StartsWith("Could not add that blueprint: ", w.StatusMessage);
    }

    [AvaloniaFact]
    public async Task SaveBlueprintNeedsPiecesAsksWhenSomeWouldFallAndCanBeCancelled()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        var asked = new List<string>();
        bool answer = false;
        w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
        var told = new List<string>();
        w.Tell = t => { told.Add(t); return Task.CompletedTask; };
        // A blank plot: nothing to save.
        await w.OpenWorkshop(null);
        await w.SaveWorkshop();
        Assert.StartsWith("There is no building piece on the plot yet", Assert.Single(told));
        // A floor would fall: asked, and Keep building keeps it as it is.
        await w.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        Assert.Contains("no_such_piece_xyz", w.StatusMessage);
        await w.SaveWorkshop();
        Assert.Equal("Save blueprint", Assert.Single(asked));
        Assert.Single(r.Listed);
        // Saved anyway, but the details are cancelled: still nothing written.
        answer = true;
        r.B.AskDetails = (_, _) => Task.FromResult<Homestead.Details?>(null);
        await w.SaveWorkshop();
        Assert.Single(r.Listed);
        // Without a plot (back on the start page): nothing.
        await w.LeaveWorkshop();
        Assert.Null(w.Session);
        await w.SaveWorkshop();
        Assert.Single(r.Listed);
    }

    [AvaloniaFact]
    public async Task ABuildingNotSavedIsKeptWhenTheAnswerIsStay()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        var asked = r.AnswerNo();
        await w.OpenWorkshop(null);
        var s = w.Session!;
        s.Commit("wall", null, [], [(new NewObject(0, StableHash.Of("woodwall"), Workshop.Anchor(s.Scene) + Vector3.UnitY, Vector3.Zero, 0), true)]);
        Assert.True(w.WorkshopDirty);
        // Another blueprint: asked, and the building stays.
        await w.OpenWorkshop(null);
        Assert.Same(s, w.Session);
        Assert.Equal("Workshop", Assert.Single(asked));
        Assert.True(w.InWorkshop);
    }

    [AvaloniaFact]
    public async Task AnotherModsFileOpensWithItsOwnNameAndTheLibraryImportsFiles()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        // A .vbuild from elsewhere (not in Homestead's folder): every kind known.
        string wall = BlueprintFiles.Vbuild(r.Dir, "Wall", "woodwall", "wood_floor");
        await w.OpenWorkshop(wall);
        Assert.Equal("Opened “Wall”: 2 piece(s). Change it, then Save blueprint.", w.StatusMessage);
        // Saved: its own name suggested, asked before replacing nothing (the file is not Homestead's).
        string? suggested = null;
        r.B.AskDetails = (d, _) => { suggested = d.Name; return Task.FromResult<Homestead.Details?>(d); };
        await w.SaveWorkshop();
        Assert.Equal("Wall", suggested);
        Assert.Contains(r.Listed, e => e.Name == "Wall");
        // The Library's Import file…: the file picked is imported and listed; none picked, nothing.
        string tower = BlueprintFiles.Vbuild(r.Dir, "Tower", "woodwall");
        r.B.PickFile = () => Task.FromResult<string?>(tower);
        w.BuildPanel.ImportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Contains(r.Listed, e => e.Name == "Tower");
        r.B.PickFile = () => Task.FromResult<string?>(null);
        w.BuildPanel.ImportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, r.Listed.Count);
    }

    [AvaloniaFact]
    public async Task ScaledPiecesAreCheckedKindsWithoutARecipeAreCostedAndALateCheckIsHarmless()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        await w.OpenWorkshop(null);
        Dispatcher.UIThread.RunJobs();
        var s = w.Session!;
        var at = Workshop.Anchor(s.Scene) + Vector3.UnitY;
        s.Commit("pieces", null, [], [
            (new NewObject(0, StableHash.Of("woodwall"), at, Vector3.Zero, 1.5f), true),
            (new NewObject(0, StableHash.Of("TreasureChest_morkhalla"), at + new Vector3(6, -1, 0), Vector3.Zero, 0), true),
        ]);
        Dispatcher.UIThread.RunJobs();
        w.RefreshSupport();
        int wall = s.Scene.Things.FindIndex(t => !t.Gone && t.Prefab == StableHash.Of("woodwall"));
        Assert.Equal(1.5f, s.Scene.Things[wall].Scale);
        Assert.Contains(wall, w.LastSupportThings);
        Assert.StartsWith("Wood ", w.WorkshopCost);
        // A check queued as the Workshop closes runs on the next area harmlessly.
        w.AfterClaudeEdit(Said.Raw("Claude did something."));
        await w.LeaveWorkshop();
        Assert.False(w.InWorkshop);
        w.Edit(TestScenes.Flat(2));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(w.LastSupport);
    }

    [AvaloniaFact]
    public async Task ASecondOpenWhileOneIsUnderWayIsRefused()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        var first = w.OpenWorkshop(null);
        var second = w.OpenWorkshop(null);
        Assert.True(second.IsCompleted);
        Assert.Equal("The Workshop is already opening.", w.StatusMessage);
        await first;
        Assert.True(w.InWorkshop);
        // Once done, another open goes ahead.
        var again = w.OpenWorkshop(null);
        Assert.NotEqual("The Workshop is already opening.", w.StatusMessage);
        await again;
    }

    [AvaloniaFact]
    public async Task ABlueprintOpenedThroughALinkToTheFolderIsSavedBackWithoutAsking()
    {
        using var r = RunWithTowerKept();
        var w = r.W;
        string link = Path.Combine(r.Dir, "linked");
        Directory.CreateSymbolicLink(link, r.Homestead);
        await w.OpenWorkshop(Path.Combine(link, Homestead.FileName("Tower")));
        Assert.Null(w.SaveWorkshopAs(new Homestead.Details("Tower", "", []), overwrite: false, evenIfFalling: true));
    }
}

