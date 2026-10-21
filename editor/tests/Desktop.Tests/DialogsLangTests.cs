using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The small modal windows (Dialogs) in another language: a question with its default second button, a message, a line
/// of text and a blueprint's details show only catalogue texts (the made-up language: PseudoLang). The title and the
/// question come from the caller; here they are catalogue texts too.
/// </summary>
[Collection("DataDir")]
public sealed class DialogsLangTests
{
    [AvaloniaFact]
    public async Task DialogsShowOnlyCatalogueTexts()
    {
        var owner = new Window { Width = 800, Height = 600 };
        owner.Show();
        using var pseudo = new PseudoLang();

        var asked = Dialogs.Ask(owner, Lang.T("common.discard"), Lang.T("common.keepThem"), Lang.T("common.discard"));
        var d = TestWindows.Shown(owner);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.False(await asked);

        var told = Dialogs.Tell(owner, Lang.T("common.save"), Lang.T("common.keepThem"));
        d = TestWindows.Shown(owner);
        Assert.Empty(PseudoLang.Untranslated(d));
        Assert.Equal(Lang.T("common.ok"), Assert.Single(d.GetLogicalDescendants().OfType<Button>()).Content);
        DialogsTests.Press(d, Lang.T("common.ok"));
        await told;

        var typed = Dialogs.AskText(owner, Lang.T("common.save"), Lang.T("common.keepThem"), "My house");
        d = TestWindows.Shown(owner);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.Null(await typed);

        var details = Dialogs.AskBlueprint(owner, new("Hut", "A small hut", ["house"]), "20 Wood");
        d = TestWindows.Shown(owner);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.Null(await details);
    }
}
