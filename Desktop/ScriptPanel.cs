using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The Script tool's panel: a C# script (with the editor's API: Area, Ground, Objects, Noise, Rnd,
// Print), examples to start from, the scripts saved in the data folder (scripts/*.csx), Run and Stop,
// and what the script printed or got wrong. The window runs it (it has the edit session).
public sealed class ScriptPanel
{
	public Control Card { get; }
	public event Action? RunAsked;
	public event Action? StopAsked;

	internal ComboBox ScriptBox { get; }
	internal TextBox CodeBox { get; } = new()
	{
		AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("monospace"), FontSize = 12,
		Height = 330, Width = 520,
	};
	internal TextBox NameBox { get; } = new() { PlaceholderText = "name to save it as", FontSize = 12, Width = 180 };
	internal Button SaveButton { get; } = new() { Content = "Save", FontSize = 12 };
	internal Button RunButton { get; } = new Button { Content = "Run (Ctrl+Enter)", FontSize = 12 }.Classed("primary");
	internal Button StopButton { get; } = new() { Content = "Stop", FontSize = 12, IsEnabled = false };
	internal SelectableTextBlock Output { get; } = new() { FontFamily = new FontFamily("monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
	private List<(string Name, string Code)> _entries = new();
	private bool _filling;

	// The folder saved scripts go in.
	public static string Folder => Path.Combine(AppSettings.DataDir, "scripts");

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
			var (name, code) = _entries[ScriptBox.SelectedIndex];
			CodeBox.Text = code;
			NameBox.Text = name.StartsWith("Example: ", StringComparison.Ordinal) ? "" : name;
		};
		RunButton.Click += (_, _) => RunAsked?.Invoke();
		StopButton.Click += (_, _) => StopAsked?.Invoke();
		SaveButton.Click += (_, _) => Save();
		CodeBox.KeyDown += (_, e) =>
		{
			if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
			{
				e.Handled = true;
				RunAsked?.Invoke();
			}
		};
		ScriptBox.Tip("script.pick");
		CodeBox.Tip("script.code");
		NameBox.Tip("script.name");
		SaveButton.Tip("script.save");
		RunButton.Tip("script.run");
		StopButton.Tip("script.stop");
		CodeBox.Text = ScriptExamples.All[0].Code;
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Width = 520,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Script", FontSize = 14, FontWeight = FontWeight.SemiBold },
					ScriptBox,
					CodeBox,
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { RunButton, StopButton, NameBox, SaveButton } },
					new ScrollViewer { MaxHeight = 150, Content = Output },
					new TextBlock
					{
						Text = "C#, run on the open area: Area (its corners, Points(), Water), Ground (Height, Original, Set, Raise, Lower, Paint, Biome, Shape, Mountain; "
							+ "NoLimit is on), Objects (All, OfKind, Near, Place, Remove, CanPlace), Noise.At, Rnd (Seed, Range, Chance, Pick), Print. "
							+ "Coordinates are world metres. Everything the script changes is one undo step. "
							+ "Scripts are programs: they can do anything a program can on this computer, so run only ones you trust.",
						FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
	}

	// The examples, then the saved scripts (newest first).
	public void Fill(string? select = null)
	{
		_filling = true;
		_entries = ScriptExamples.All.Select(e => ("Example: " + e.Name, e.Code)).ToList();
		try
		{
			if (Directory.Exists(Folder))
			{
				_entries.AddRange(new DirectoryInfo(Folder).GetFiles("*.csx").OrderByDescending(f => f.LastWriteTimeUtc)
					.Select(f => (Path.GetFileNameWithoutExtension(f.Name), File.ReadAllText(f.FullName))));
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
		ScriptBox.ItemsSource = _entries.Select(e => e.Name).ToList();
		ScriptBox.SelectedIndex = select != null ? _entries.FindIndex(e => e.Name == select) : -1;
		_filling = false;
	}

	public event Action<string>? Message;

	private void Save()
	{
		string name = string.Concat((NameBox.Text ?? "").Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
		if (name.Length == 0)
		{
			Message?.Invoke("Type a name to save the script as.");
			return;
		}
		try
		{
			Directory.CreateDirectory(Folder);
			File.WriteAllText(Path.Combine(Folder, name + ".csx"), CodeBox.Text ?? "");
			Fill(name);
			Message?.Invoke($"Saved the script “{name}” (in {Folder}).");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Message?.Invoke("Could not save the script: " + ex.Message);
		}
	}

	// While a script runs: Stop instead of Run.
	public void Running(bool on)
	{
		RunButton.IsEnabled = !on;
		StopButton.IsEnabled = on;
	}
}
