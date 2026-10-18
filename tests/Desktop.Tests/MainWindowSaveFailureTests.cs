using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Save in the 3D editor when the world's folder is gone by then (a sync tool, a drive unplugged): what failed is said in
/// the status bar, nothing is thrown, and the changes stay pending.
/// </summary>
[Collection("World files")]
public sealed class MainWindowSaveFailureTests
{
    [AvaloniaFact]
    public async Task ASaveThatFailsIsSaidAndTheChangesStayPending()
    {
        using var tw = new TempWorld();
        var w = await TestWindows.EditingWorld(tw.Dir);
        w.Session!.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        w.ConfirmSave = _ => Task.FromResult(true);
        w.Tell = _ => Task.CompletedTask;
        string away = tw.Dir + "-away";
        Directory.Move(tw.Dir, away);
        try
        {
            await w.Save();
        }
        finally
        {
            Directory.Move(away, tw.Dir);
        }

        Assert.StartsWith("Could not save:", w.MessageText.Text);
        Assert.Equal(1, w.World!.Pending.Zones);
        Assert.True(w.SaveButton.IsEnabled);
    }
}
