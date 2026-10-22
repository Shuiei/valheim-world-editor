using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window following the edit session of the area it shows (MainWindow.Edits): the line under the world's name.
/// </summary>
public class MainWindowEditsTests
{
    private static WorldScene.Thing Rock(int id) => new(id, StableHash.Of("Rock_4"), new Vector3(30 + id, 30, 30), Vector3.Zero, 1, false);

    // The count of objects under the world's name follows the edits: it was the count when the area opened, so a
    // deletion, or the undo of a placement, still showed the objects that were gone.
    [AvaloniaFact]
    public void TheAreasObjectCountFollowsTheEdits()
    {
        var (w, s) = TestWindows.EditingFlat(Rock(1), Rock(2), Rock(3));
        try
        {
            s.Delete([0]);
            Dispatcher.UIThread.RunJobs();
            Assert.EndsWith("· 2 objects", w.SubtitleText.Text);
            s.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.EndsWith("· 3 objects", w.SubtitleText.Text);
        }
        finally
        {
            w.Close();
        }
    }
}
