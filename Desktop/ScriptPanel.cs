using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Script tool's panel: a C# script (with the editor's API: Area, Ground, Objects, Noise, Rnd, Print), examples to
/// start from, the scripts saved in the data folder (scripts/*.csx), Run and Stop, and what the script printed or got
/// wrong. The window runs it (it has the edit session).
/// </summary>
public sealed class ScriptPanel
{
    /// <summary>The panel itself, shown over the view while the Script tool is on.</summary>
    public Control Card { get; }
    /// <summary>Run (or Ctrl+Enter in the code) was asked for; the window runs it.</summary>
    public event Action? RunAsked;
    /// <summary>Stop was asked for.</summary>
    public event Action? StopAsked;

    /// <summary>The examples and the saved scripts; choosing one puts its code in the editor.</summary>
    internal ComboBox ScriptBox { get; }
    /// <summary>The script's code.</summary>
    internal TextBox CodeBox { get; } = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("monospace"),
        FontSize = 12,
        Height = 330,
        Width = 520,
    };
    /// <summary>The name to save the script as (filled with a saved script's name, empty for an example).</summary>
    internal TextBox NameBox { get; } = new() { PlaceholderText = Lang.T("script.hint.name"), FontSize = 12, Width = 180 };
    /// <summary>Saves the code as name.csx in <see cref="Folder"/>.</summary>
    internal Button SaveButton { get; } = new() { Content = Lang.T("common.save"), FontSize = 12 };
    /// <summary>Runs the script.</summary>
    internal Button RunButton { get; } = new Button { Content = Lang.T("script.label.run"), FontSize = 12 }.Classed("primary");
    /// <summary>Stops the running script.</summary>
    internal Button StopButton { get; } = new() { Content = Lang.T("script.label.stop"), FontSize = 12, IsEnabled = false };
    /// <summary>What the script printed or got wrong.</summary>
    internal SelectableTextBlock Output { get; } = new() { FontFamily = new FontFamily("monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The list's entries: the examples (shown as "Example: " and their name), then the saved scripts; each with its own
    /// name (an example's without "Example: ") and whether it is an example.
    /// </summary>
    private List<(string Name, string Code, bool Example)> _entries = [];
    /// <summary>Set while the list is filled, so choosing the selection does not replace the code.</summary>
    private bool _filling;
    /// <summary>Asks a yes-or-no question (replacing another saved script); yes when nothing is set.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

    /// <summary>The folder saved scripts go in.</summary>
    public static string Folder => Path.Combine(AppSettings.DataDir, "scripts");

    /// <summary>Builds the panel, starting with the first example chosen.</summary>
    public ScriptPanel()
    {
        ScriptBox = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 420 };
        Fill();
        ScriptBox.SelectionChanged += (_, _) =>
        {
            if (_filling || ScriptBox.SelectedIndex < 0 || ScriptBox.SelectedIndex >= _entries.Count)
            {
                return;
            }
            var (name, code, example) = _entries[ScriptBox.SelectedIndex];
            CodeBox.Text = code;
            NameBox.Text = example ? "" : name;
        };
        RunButton.Click += (_, _) => RunAsked?.Invoke();
        StopButton.Click += (_, _) => StopAsked?.Invoke();
        SaveButton.Click += async (_, _) => await Save();
        // On the way down (tunnel), before the text box takes Enter for a new line.
        CodeBox.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) || e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta)))
            {
                e.Handled = true;
                RunAsked?.Invoke();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ScriptBox.Tip("script.pick");
        CodeBox.Tip("script.code");
        NameBox.Tip("script.name");
        SaveButton.Tip("script.save");
        RunButton.Tip("script.run");
        StopButton.Tip("script.stop");
        // The first example chosen, so its code is shown and a Run is named after it.
        ScriptBox.SelectedIndex = 0;
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 520,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("rail.label.script"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    ScriptBox,
                    CodeBox,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { RunButton, StopButton, NameBox, SaveButton } },
                    new ScrollViewer { MaxHeight = 150, Content = Output },
                    new TextBlock
                    {
                        Text = Lang.T("script.hint.api"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
    }

    /// <summary>
    /// Fills the list: the examples, then the saved scripts (newest first); selects the entry of that name if given,
    /// else none.
    /// </summary>
    public void Fill(string? select = null)
    {
        _filling = true;
        _entries = [.. ScriptExamples.All.Select(e => (e.Name, e.Code, true))];
        try
        {
            if (Directory.Exists(Folder))
            {
                foreach (var f in new DirectoryInfo(Folder).GetFiles("*.csx").OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    // One that cannot be read is left out (and logged), not the rest with it.
                    try
                    {
                        _entries.Add((Path.GetFileNameWithoutExtension(f.Name), File.ReadAllText(f.FullName), false));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Script {f.FullName} not listed: {ex.Message}");
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        ScriptBox.ItemsSource = _entries.Select(e => e.Example ? Lang.T("script.example", e.Name) : e.Name).ToList();
        ScriptBox.SelectedIndex = select != null ? _entries.FindIndex(e => !e.Example && e.Name == select) : -1;
        _filling = false;
    }

    /// <summary>
    /// The chosen entry's own name (an example's without "Example: "), the undo step's name when it runs; null when
    /// none is chosen.
    /// </summary>
    internal string? ChosenName => ScriptBox.SelectedIndex >= 0 && ScriptBox.SelectedIndex < _entries.Count ? _entries[ScriptBox.SelectedIndex].Name : null;

    /// <summary>A line for the status bar, in both languages.</summary>
    public event Action<Said>? Message;

    /// <summary>
    /// Saves the code under the typed name (characters a file name cannot have become _), and selects it. Another saved
    /// script of that name (not the one chosen in the list) is replaced only when the person says so.
    /// </summary>
    internal async Task Save()
    {
        string name = string.Concat((NameBox.Text ?? "").Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (name.Length == 0)
        {
            Message?.Invoke(Said.Of("script.msg.noName"));
            return;
        }
        string path = Path.Combine(Folder, name + ".csx");
        bool chosen = ScriptBox.SelectedIndex >= 0 && ScriptBox.SelectedIndex < _entries.Count && _entries[ScriptBox.SelectedIndex] is { Example: false } e && e.Name == name;
        if (File.Exists(path) && !chosen && !await Confirm(Lang.T("script.ask.replace.text", name)))
        {
            return;
        }
        try
        {
            SafeFile.WriteAllText(path, CodeBox.Text ?? "");
            Fill(name);
            Message?.Invoke(Said.Of("script.msg.saved", name, Folder));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message?.Invoke(Said.Of("script.msg.notSaved", ex.Message));
        }
    }

    /// <summary>While a script runs: Stop instead of Run.</summary>
    public void Running(bool on)
    {
        RunButton.IsEnabled = !on;
        StopButton.IsEnabled = on;
    }
}
