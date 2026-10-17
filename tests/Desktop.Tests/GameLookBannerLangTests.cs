using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page's game-look card in another language (the made-up one: PseudoLang): the game not found (with the
/// steps to find its folder), the copy failed, the copy running and done show only catalogue texts, and Use without a
/// folder says why in the language. The copy itself never runs: the card is given the states.
/// </summary>
[Collection("DataDir")]
public sealed class GameLookBannerLangTests
{
    [AvaloniaFact]
    public void GameLookBannerShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var now = new GameLook.Snapshot("missing", null, null, null, null);
        var b = new GameLookBanner(new AppSettings()) { Read = () => now, StartExport = (_, _) => null };
        var w = new Window { Content = b.View };
        w.Show();
        try
        {
            Assert.True(b.Steps.IsVisible);
            Assert.Empty(PseudoLang.Untranslated(b.View));

            b.Start("");
            Assert.Equal(PseudoLang.Wrap(Lang.English["look.error.noFolder"]), b.Error.Text);

            foreach (var state in new GameLook.Snapshot[]
            {
                new("failed", null, "/games/valheim", null, null),
                new("running", null, "/games/valheim", null, 0.1),
                new("ready", null, "/games/valheim", null, 1),
            })
            {
                now = state;
                b.Refresh();
                Assert.Empty(PseudoLang.Untranslated(b.View));
            }
        }
        finally
        {
            w.Close();
        }
    }
}
