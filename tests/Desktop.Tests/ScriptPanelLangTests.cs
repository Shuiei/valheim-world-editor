using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script panel in another language (the made-up one: PseudoLang): with the examples and a saved script listed, it
/// shows only catalogue texts but for the saved script's name; what it says in the status bar is in the language, its
/// English kept for Claude. Its scripts folder is in a data folder of its own here.
/// </summary>
[Collection("DataDir")]
public sealed class ScriptPanelLangTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-scripts-lang-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _old = AppSettings.DataDirOverride;

    public ScriptPanelLangTests() => AppSettings.DataDirOverride = _data;

    public void Dispose()
    {
        AppSettings.DataDirOverride = _old;
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, true);
        }
    }

    [AvaloniaFact]
    public async Task ScriptPanelShowsOnlyCatalogueTexts()
    {
        Directory.CreateDirectory(ScriptPanel.Folder);
        File.WriteAllText(Path.Combine(ScriptPanel.Folder, "mine.csx"), "Print(1);");
        using var pseudo = new PseudoLang();
        var p = new ScriptPanel();
        Assert.Equal(ScriptExamples.All.Length + 1, p.ScriptBox.ItemCount);
        Assert.Empty(PseudoLang.Untranslated(p.Card, "mine"));

        // An example picked: its own name, not the shown "Example: …", names the run.
        p.ScriptBox.SelectedIndex = 1;
        Assert.Equal(ScriptExamples.All[1].Name, p.ChosenName);

        Said? said = null;
        p.Message += m => said = m;
        p.NameBox.Text = " ";
        await p.Save();
        Assert.Equal(PseudoLang.Wrap("Type a name to save the script as."), said!.Shown);
        Assert.Equal("Type a name to save the script as.", said.English);
    }
}
