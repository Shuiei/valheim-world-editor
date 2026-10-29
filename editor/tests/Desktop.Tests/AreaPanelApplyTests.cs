using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's Apply while it runs: a Cancel reset with nothing marked, a second Apply while one waits for an answer, and an area opened while it waits.
/// </summary>
[Collection("World files")]
public class AreaPanelApplyTests
{
    [AvaloniaFact]
    public async Task CancelResetWithNothingMarkedSaysSoAndResetAsksFirst()
    {
        using var r = new SharedAreaPanelRun();
        Click(r.P.UnresetButton);
        Assert.Equal("No zone marked for reset under the selection.", r.Said);
        r.P.Confirm = _ => Task.FromResult(false);
        r.P.Choose(AreaPanel.Act.Reset);
        await r.P.Apply();
        Assert.Empty(r.S.Resets);
        r.P.Confirm = _ => Task.FromResult(true);
        r.P.ResetGroundBox.IsChecked = false;
        await r.P.Apply();
        Assert.Single(r.S.Resets);
        Click(r.P.UnresetButton);
        Assert.Empty(r.S.Resets);
        Assert.StartsWith("Cancelled reset of 1 zone(s)", r.S.UndoLabel);
    }

    [AvaloniaFact]
    public async Task AnApplyWhileOneRunsDoesNothing()
    {
        using var r = new SharedAreaPanelRun();
        var answer = new TaskCompletionSource<bool>();
        int asked = 0;
        r.P.Confirm = _ =>
        {
            asked++;
            return answer.Task;
        };
        r.P.Choose(AreaPanel.Act.Reset);
        r.P.ResetGroundBox.IsChecked = false;
        var first = r.P.Apply();
        Assert.False(r.P.ApplyButton.IsEnabled);
        // Enter again while the question is open: not asked twice, not marked twice.
        await r.P.Apply();
        Assert.Equal(1, asked);
        answer.SetResult(true);
        await first;
        Assert.Single(r.S.Resets);
        Assert.True(r.P.ApplyButton.IsEnabled);
    }

    [AvaloniaFact]
    public async Task AnAreaOpenedWhileApplyWaitsGetsNothing()
    {
        using var r = new SharedAreaPanelRun();
        EditSession? other = null;
        r.P.Confirm = _ =>
        {
            // Another area opened while the question is open.
            var scene = WorldScene.Load(r.Dir, 0, 0, 1);
            r.W.View.Show(scene, null);
            r.W.Edit(scene.Session!);
            other = scene.Session;
            return Task.FromResult(true);
        };
        r.P.Choose(AreaPanel.Act.Reset);
        r.P.ResetGroundBox.IsChecked = false;
        await r.P.Apply();
        Assert.Empty(r.S.Resets);
        Assert.Empty(other!.Resets);
        Assert.Equal("The area changed while this was running: nothing was applied.", r.Said);
    }
}
