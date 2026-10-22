using System.Globalization;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's scripts (ClaudeTools.Scripts): the ground tools written as scripts (flatten) keep within the game's ±8 m, as
/// they say, while a script of Claude's own goes past it, as the Script tool's do.
/// </summary>
[Collection("World files")]
public class ClaudeToolsScriptsTests
{
    /// <summary>The ground's height and original height at a world point of the open area.</summary>
    private static async Task<(float Height, float Original)> At(ClaudeRun r, float x, float z)
    {
        var p = ClaudeRun.J(await r.T.GroundAtPoints([[x, z]]))[0];
        return (p.GetProperty("height").GetSingle(), p.GetProperty("original").GetSingle());
    }

    [AvaloniaFact]
    public async Task TheGroundToolsKeepWithinTheGamesLimitAndScriptsGoPastIt()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var (start, original) = await At(r, 15, 15);
        Assert.Contains("stopped at the game's ±8 m", await r.T.Flatten(10, 10, 20, 20, height: start + 100, edge: 0));
        var (flat, still) = await At(r, 15, 15);
        Assert.Equal(original + 8, flat, 1);
        Assert.Equal(original, still, 1);
        // A script of Claude's own: No limit is on, as in the Script tool.
        string up = (original + 60).ToString(CultureInfo.InvariantCulture);
        await r.T.RunScript($"Ground.Set(15, 15, {up}f);");
        Assert.Equal(original + 60, (await At(r, 15, 15)).Height, 1);
    }
}
