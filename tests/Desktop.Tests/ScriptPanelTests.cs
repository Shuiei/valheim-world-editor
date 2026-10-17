using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's panel (ScriptPanel): examples and saved scripts to pick, saving under a name, Ctrl+Enter to run,
/// and Run and Stop while a script runs. Its scripts folder is in a data folder of its own here, so this runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class ScriptPanelTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-scripts-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _old = AppSettings.DataDirOverride;

    public ScriptPanelTests() => AppSettings.DataDirOverride = _data;

    public void Dispose()
    {
        AppSettings.DataDirOverride = _old;
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, true);
        }
    }

    // A script saved under a name (characters a file cannot have replaced), then listed after the examples and
    // picked: its code and name back. An example picked leaves the name empty; no name is said.
    [AvaloniaFact]
    public void ScriptsAreSavedAndPicked()
    {
        var p = new ScriptPanel();
        var said = new List<string>();
        p.Message += m => said.Add(m.Shown);
        p.Fill();
        Assert.Equal(-1, p.ScriptBox.SelectedIndex);
        int examples = ScriptExamples.All.Length;

        p.NameBox.Text = "  ";
        p.SaveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Type a name to save the script as.", said[^1]);

        p.CodeBox.Text = "Print(1);";
        p.NameBox.Text = "hills/valleys";
        p.SaveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith("Saved the script “hills_valleys”", said[^1]);
        Assert.Equal("Print(1);", File.ReadAllText(Path.Combine(ScriptPanel.Folder, "hills_valleys.csx")));
        // Listed after the examples, and picked.
        Assert.Equal(examples + 1, p.ScriptBox.ItemCount);
        Assert.Equal(examples, p.ScriptBox.SelectedIndex);

        p.ScriptBox.SelectedIndex = 0;
        Assert.Equal(ScriptExamples.All[0].Code, p.CodeBox.Text);
        Assert.Equal("", p.NameBox.Text);
        p.ScriptBox.SelectedIndex = examples;
        Assert.Equal("Print(1);", p.CodeBox.Text);
        Assert.Equal("hills_valleys", p.NameBox.Text);
        p.Fill("no such script");
        Assert.Equal(-1, p.ScriptBox.SelectedIndex);
    }

    // Saving over another saved script asks first (No keeps it); the script picked in the list is saved over without
    // asking. A saved script that cannot be read is left out of the list, not the others with it.
    [AvaloniaFact]
    public async Task AnotherScriptIsReplacedOnlyWhenSaidAndAnUnreadableOneIsLeftOut()
    {
        Directory.CreateDirectory(ScriptPanel.Folder);
        File.WriteAllText(Path.Combine(ScriptPanel.Folder, "mine.csx"), "Print(1);");
        File.CreateSymbolicLink(Path.Combine(ScriptPanel.Folder, "gone.csx"), Path.Combine(_data, "missing.csx"));
        var p = new ScriptPanel();
        var asked = new List<string>();
        p.Confirm = q => { asked.Add(q); return Task.FromResult(false); };
        int examples = ScriptExamples.All.Length;
        Assert.Equal(examples + 1, p.ScriptBox.ItemCount);
        // An example open, saved as "mine": asked, and No keeps the saved one.
        p.ScriptBox.SelectedIndex = 0;
        p.NameBox.Text = "mine";
        await p.Save();
        Assert.Equal("There is a saved script called “mine” already. Replace it?", Assert.Single(asked));
        Assert.Equal("Print(1);", File.ReadAllText(Path.Combine(ScriptPanel.Folder, "mine.csx")));
        // "mine" itself picked and changed: saved without asking.
        p.ScriptBox.SelectedIndex = examples;
        p.CodeBox.Text = "Print(2);";
        await p.Save();
        Assert.Single(asked);
        Assert.Equal("Print(2);", File.ReadAllText(Path.Combine(ScriptPanel.Folder, "mine.csx")));
    }

    // The scripts folder cannot be made (a file is in its place): said, nothing saved.
    [AvaloniaFact]
    public void ASaveThatFailsIsSaid()
    {
        Directory.CreateDirectory(_data);
        File.WriteAllText(ScriptPanel.Folder, "not a folder");
        var p = new ScriptPanel();
        string? said = null;
        p.Message += m => said = m.Shown;
        p.NameBox.Text = "mine";
        p.SaveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith("Could not save the script: ", said);
    }

    // Ctrl+Enter in the code runs it (Enter alone is a new line); Run and Stop raise theirs; while running, Run is off
    // and Stop on.
    [AvaloniaFact]
    public void RunAndStop()
    {
        var p = new ScriptPanel();
        int run = 0, stop = 0;
        p.RunAsked += () => run++;
        p.StopAsked += () => stop++;
        var window = new Window { Content = p.Card, Width = 800, Height = 700 };
        window.Show();
        p.CodeBox.Focus();
        Assert.True(p.CodeBox.IsFocused);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        Assert.Equal(0, run);
        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        Assert.Equal(1, run);
        p.RunButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        p.StopButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((2, 1), (run, stop));
        p.Running(true);
        Assert.False(p.RunButton.IsEnabled);
        Assert.True(p.StopButton.IsEnabled);
        p.Running(false);
        Assert.True(p.RunButton.IsEnabled);
        Assert.False(p.StopButton.IsEnabled);
        window.Close();
    }
}
