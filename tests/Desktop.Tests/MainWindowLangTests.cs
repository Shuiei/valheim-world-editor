using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The status bar in another language (the made-up one: PseudoLang): a message is shown in it while its English is
/// kept for Claude, who reads it back in editor_state; a message added to keeps each side's language; a text put
/// there directly is the same for both.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowLangTests
{
    [AvaloniaFact]
    public async Task TheStatusBarShowsTheLanguageAndClaudeReadsEnglish()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        // What the window posts when it opens says nothing over the messages below.
        Dispatcher.UIThread.RunJobs();
        var tools = new ClaudeTools(w);
        using var pseudo = new PseudoLang();

        w.Msg("settings.worldNotFound", "/x");
        Assert.Equal("⟦World folder not found: /x⟧", w.MessageText.Text);
        Assert.Equal("World folder not found: /x", w.LastMessageEn);
        Assert.Equal("World folder not found: /x", ClaudeRun.J(await tools.EditorState()).GetProperty("message").GetString());

        w.Msg(w.LastSaid + Said.Of("common.save"));
        Assert.Equal("⟦World folder not found: /x⟧ ⟦Save⟧", w.MessageText.Text);
        Assert.Equal("World folder not found: /x Save", w.LastMessageEn);

        // An exception's message, put there as it is.
        w.MessageText.Text = "disk full";
        Assert.Equal("disk full", w.LastMessageEn);
        Assert.Equal(new Said("disk full", "disk full"), w.LastSaid);
    }
}
