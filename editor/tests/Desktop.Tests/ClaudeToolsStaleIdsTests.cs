using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object ids the Claude tools hand out: they fit the area and reading they were given for, and are refused for another
/// area or once the world has been read again.
/// </summary>
[Collection("World files")]
public class ClaudeToolsStaleIdsTests
{
    private static string? StaleIds(ClaudeTools tools, WorldScene? scene) =>
        PrivateInvoke.Call<string?>(typeof(ClaudeTools), "StaleIds", tools, scene);

    private static void GaveIds(ClaudeTools tools, WorldScene scene, int reads) =>
        PrivateInvoke.Call<object?>(typeof(ClaudeTools), "GaveIds", tools, scene, reads);

    [AvaloniaFact]
    public async Task NoIdsGivenYetAreNeverStale()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();

        Assert.Null(StaleIds(r.T, r.W.Session!.Scene));
    }

    [AvaloniaFact]
    public async Task IdsGivenForTheSceneAtItsCurrentReadingFit()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        GaveIds(r.T, scene, scene.Reads);

        Assert.Null(StaleIds(r.T, scene));
    }

    [AvaloniaFact]
    public async Task IdsGivenBeforeTheWorldWasReadAgainAreStale()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        GaveIds(r.T, scene, scene.Reads - 1);

        Assert.Equal(
            "The ids are from before the world was read again: list_objects (or get_selection) again for this one.",
            StaleIds(r.T, scene));
    }

    [AvaloniaFact]
    public async Task IdsGivenForAnotherAreaAreStale()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        GaveIds(r.T, scene, scene.Reads);

        Assert.Equal(
            "The ids are from another area: list_objects (or get_selection) again for this one.",
            StaleIds(r.T, null));
    }
}
