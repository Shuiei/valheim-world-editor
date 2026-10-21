using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' Blueprints panel run opening the Workshop (BlueprintsPanelRun.OpenWorkshop), tested once here so the
/// tests using it can rely on it: the window's own Workshop opens blank, and a blueprint opens over a building not
/// saved without waiting on the question (answered Yes).
/// </summary>
public sealed class BlueprintsPanelRunTests
{
    [AvaloniaFact]
    public async Task OpenWorkshopOpensTheWindowsWorkshopAnsweringYes()
    {
        using var r = new BlueprintsPanelRun();
        var w = await r.OpenWorkshop();
        Assert.Same(r.W, w);
        Assert.True(w.InWorkshop);
        Assert.Equal(0, Workshop.Pieces(w.Session!.Scene));
        await r.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        w.Session!.Commit("Moved", null, [w.Session.Scene.Things.FindIndex(t => !t.Gone)], []);
        Assert.True(w.WorkshopDirty);
        await r.OpenWorkshop(null);
        Assert.False(w.WorkshopDirty);
        Assert.Equal(0, Workshop.Pieces(w.Session!.Scene));
    }
}
