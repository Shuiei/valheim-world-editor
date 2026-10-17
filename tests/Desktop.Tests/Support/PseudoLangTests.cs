using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' made-up language (PseudoLang), tested once here so the windows' language tests can rely on it: every
/// catalogue text wrapped with its values put in, a bare English text in any place a window shows text found, numbers
/// with units and the data a test names let through, and English back once it is disposed.
/// </summary>
[Collection("DataDir")]
public sealed class PseudoLangTests
{
    [AvaloniaFact]
    public void ItWrapsEveryTextAndEnglishComesBack()
    {
        using (new PseudoLang())
        {
            Assert.Equal(PseudoLang.Code, Lang.Current);
            Assert.Equal("⟦Cancel⟧", Lang.T("common.cancel"));
            Assert.Equal("⟦World folder not found: /x⟧", Lang.T("settings.worldNotFound", "/x"));
            // Claude's English is not wrapped.
            Assert.Equal("Cancel", Lang.En("common.cancel"));
        }
        Assert.Equal(Lang.Source, Lang.Current);
        Assert.Equal("Cancel", Lang.T("common.cancel"));
    }

    [AvaloniaFact]
    public void TextsNotFromTheCatalogueAreFound()
    {
        using var pseudo = new PseudoLang();
        var title = new TextBlock();
        title.Inlines ??= [];
        title.Inlines.Add(new Run("Valheim"));
        title.Inlines.Add(new LineBreak());
        title.Inlines.Add(new Bold { Inlines = { new Run(Lang.T("common.save")) } });
        var tipped = new Button { Content = Lang.T("common.ok") };
        ToolTip.SetTip(tipped, "Bare tip");
        var panel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = Lang.T("common.save") },
                new Button { Content = Lang.T("common.cancel") },
                new TextBlock { Text = "Bare English" },
                // Numbers with their units, signs, and a name the test calls data.
                new TextBlock { Text = "12.5 m · 3 × 4 · 60 fps ✕" },
                new TextBlock { Text = "Hut" },
                // A translated text holding another, and two translated texts with English between them.
                new TextBlock { Text = PseudoLang.Wrap($"Placed {Lang.T("common.yes")} here") },
                new TextBlock { Text = $"{Lang.T("common.yes")} and {Lang.T("common.ok")}" },
                new TextBox { Text = "typed by the user", PlaceholderText = "type here" },
                new ComboBox { ItemsSource = new[] { Lang.T("common.yes"), "No way" } },
                new CheckBox { Content = "Bare switch" },
                new Expander { Header = Lang.T("common.copy"), Content = new TextBlock { Text = "Inside" } },
                tipped,
                title,
            },
        };
        var window = new Window { Title = "Bare title", Content = panel };

        Assert.Equal(["Bare title", "Bare English", "⟦Yes⟧ and ⟦OK⟧", "type here", "No way", "Bare switch", "Inside", "Bare tip", "Valheim"], PseudoLang.Untranslated(window, "Hut"));
        Assert.Empty(PseudoLang.Untranslated(new TextBlock { Text = "Valheim" }, "Valheim"));
    }
}
