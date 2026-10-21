using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A remembered list choice (Prefs.Bind) in another language: kept in English, so a choice made in one language comes
/// back in another (the made-up one: PseudoLang), and the other way round; a text no catalogue has is kept as it is.
/// </summary>
[Collection("DataDir")]
public sealed class PrefsLangTests
{
    /// <summary>A list of two catalogue texts in the language in use, nothing chosen.</summary>
    private static ComboBox Choices() => new() { ItemsSource = new[] { Lang.T("common.cancel"), Lang.T("common.save") } };

    [AvaloniaFact]
    public void AListsChoiceComesBackInAnotherLanguage()
    {
        var p = Prefs.InMemory();
        var english = Choices();
        p.Bind(english, "k");
        english.SelectedIndex = 1;
        Assert.Equal("Save", p.Get("k", ""));
        using (new PseudoLang())
        {
            var pseudo = Choices();
            p.Bind(pseudo, "k");
            Assert.Equal(1, pseudo.SelectedIndex);
            pseudo.SelectedIndex = 0;
            Assert.Equal("Cancel", p.Get("k", ""));
            // A stamp's name is no catalogue text.
            var stamps = new ComboBox { ItemsSource = new[] { Lang.T("common.save"), "My stamp" } };
            p.Bind(stamps, "stamp");
            stamps.SelectedIndex = 1;
            Assert.Equal("My stamp", p.Get("stamp", ""));
        }
        var back = Choices();
        p.Bind(back, "k");
        Assert.Equal(0, back.SelectedIndex);
        var stampsBack = new ComboBox { ItemsSource = new[] { Lang.T("common.save"), "My stamp" } };
        p.Bind(stampsBack, "stamp");
        Assert.Equal(1, stampsBack.SelectedIndex);
    }
}
