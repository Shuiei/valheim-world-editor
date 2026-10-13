using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window in the Workshop: Save with nothing on the plot, the top bar's Start button, and quitting, which asks while
/// the building is not saved as a blueprint (staying, or quitting anyway) and does not ask once nothing waits.
/// </summary>
[Collection("World files")]
public class MainWindowClosingTests
{
    /// <summary>
    /// Raises the button's click.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// A wall put on the plot: the building is not saved any more.
    /// </summary>
    private static void AddWall(MainWindow w) =>
        w.Session!.Commit("Wall", null, [], [(new NewObject(0, StableHash.Of("woodwall"), new Vector3(0, 31, 0), Vector3.Zero, 0), true)]);

    [AvaloniaFact]
    public async Task AnEmptyPlotHasNothingToSaveAndStartLeavesAtOnce()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        var told = new List<string>();
        w.Tell = text => { told.Add(text); return Task.CompletedTask; };
        w.Ask = (_, _, _, _) => throw new InvalidOperationException("nothing to ask");
        await w.OpenWorkshop(null);
        Assert.True(w.InWorkshop);
        await w.Save();
        Assert.Equal("There is no building piece on the plot yet: place some first (Place tool).", Assert.Single(told));
        // Start (the Map button in the Workshop): nothing waits, so back to the start page at once.
        Click(w.MapButton);
        Dispatcher.UIThread.RunJobs();
        Assert.False(w.InWorkshop);
        Assert.NotNull(w.StartPage);
        Assert.Null(w.Session);
    }

    [AvaloniaFact]
    public async Task QuittingTheWorkshopAsksWhileTheBuildingIsNotSaved()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        var asked = new List<string>();
        w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(false); };
        await w.OpenWorkshop(null);
        AddWall(w);
        Assert.True(w.WorkshopDirty);
        // No: the window stays.
        w.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Unsaved building", Assert.Single(asked));
        Assert.True(w.IsVisible);
        // Yes (answered later, as a dialog does): it closes.
        w.Ask = async (title, _, _, _) =>
        {
            asked.Add(title);
            await Task.Yield();
            return true;
        };
        w.Close();
        await TestWindows.Until(() => !w.IsVisible);
        Assert.Equal(2, asked.Count);
    }

    [AvaloniaFact]
    public async Task QuittingASavedWorkshopDoesNotAsk()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => throw new InvalidOperationException("nothing to ask");
        await w.OpenWorkshop(null);
        Assert.False(w.WorkshopDirty);
        w.Close();
        await TestWindows.Until(() => !w.IsVisible);
    }
}
