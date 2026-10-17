using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Regrowing in another language (the made-up one: PseudoLang): why it cannot be done is shown in the language in use,
/// and Claude's regrow_nature is told it in English.
/// </summary>
[Collection("DataDir")]
public sealed class GrowthLangTests
{
    [AvaloniaFact]
    public async Task ClaudeIsToldWhyNothingRegrowsInEnglish()
    {
        using var pseudo = new PseudoLang();
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        // No world generator in a made-up area.
        var s = TestScenes.Flat(1);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        Said? said = null;
        Assert.Null(await Growth.Spots(s, -30, -30, 30, 30, _ => true, _ => null, m => said = m));
        Assert.Equal(PseudoLang.Wrap("Regrow needs the world's generator (not available here)."), said!.Shown);
        Assert.Equal("Regrow needs the world's generator (not available here).", said.English);
        Assert.Equal("Regrow needs the world's generator (not available here).", await new ClaudeTools(w).RegrowNature(-10, -10, 10, 10));
    }
}
