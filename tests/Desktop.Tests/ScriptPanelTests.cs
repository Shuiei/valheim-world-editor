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
        try
        {
            Directory.Delete(_data, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }

    // A script saved under a name (characters a file cannot have replaced), then listed after the examples and
    // picked: its code and name back. An example picked leaves the name empty; no name is said.
    [AvaloniaFact]
    public void ScriptsAreSavedAndPicked()
    {
        var p = new ScriptPanel();
        var said = new List<string>();
        p.Message += said.Add;
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

    // The scripts folder cannot be made (a file is in its place): said, nothing saved.
    [AvaloniaFact]
    public void ASaveThatFailsIsSaid()
    {
        Directory.CreateDirectory(_data);
        File.WriteAllText(ScriptPanel.Folder, "not a folder");
        var p = new ScriptPanel();
        string? said = null;
        p.Message += m => said = m;
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
