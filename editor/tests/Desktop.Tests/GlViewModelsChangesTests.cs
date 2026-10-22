using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view following its things as they go and come back (GlView.Models.Changes): a selected thing gone leaves the
/// selection, and the window hears of it.
/// </summary>
public class GlViewModelsChangesTests
{
    private static WorldScene.Thing Rock(int id) => new(id, StableHash.Of("Rock_4"), new Vector3(id, 30, 0), Vector3.Zero, 1, false);

    // A selected rock deleted (or the move that made it undone): the selection drops it and says so, so the Select
    // panel's exact place and the status bar's "Selected: …" no longer show an object that is not there.
    [AvaloniaFact]
    public void ASelectedThingGoneChangesTheSelection()
    {
        var area = TestScenes.Flat(1, Rock(1), Rock(2));
        var v = new GlView();
        var w = new Window { Width = 400, Height = 300, Content = v };
        w.Show();
        try
        {
            v.Show(area.Scene, null);
            v.Select([0, 1]);
            Dispatcher.UIThread.RunJobs();
            IReadOnlyList<WorldScene.Thing>? said = null;
            v.SelectionChanged += things => said = things;
            area.Delete([0]);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal([1], v.Selected);
            Assert.NotNull(said);
            Assert.Equal(2, Assert.Single(said).Id);
        }
        finally
        {
            w.Close();
        }
    }
}
