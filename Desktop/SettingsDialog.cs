using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Settings: folders on this computer (the web start page's settings): the Valheim game folder, more
// BepInEx folders to look for the plugin in, and more world folders to list.
public static class SettingsDialog
{
	private static string Expand(string path)
	{
		path = path.Trim().Trim('"');
		if (path.StartsWith('~'))
		{
			path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
		}
		return Path.GetFullPath(path);
	}

	// Checks and applies the choices; the problems, or null when saved.
	public static string? Apply(AppSettings settings, string? valheim, IEnumerable<string> bepInEx, IEnumerable<string> worlds)
	{
		var problems = new List<string>();
		string? game = string.IsNullOrWhiteSpace(valheim) ? null : Expand(valheim);
		if (game != null && GameLook.BundlesDir(game) == null)
		{
			problems.Add("The Valheim folder must be the game's folder, the one with valheim_Data.");
		}
		List<string> Clean(IEnumerable<string> list, string what) => list.Select(p => p.Trim()).Where(p => p.Length > 0).Select(Expand).Distinct()
			.Where(p => { if (Directory.Exists(p)) return true; problems.Add($"{what} not found: {p}"); return false; }).ToList();
		var bep = Clean(bepInEx, "BepInEx folder");
		var wl = Clean(worlds, "World folder");
		if (problems.Count > 0)
		{
			return string.Join(" ", problems);
		}
		bool gameChanged = game != settings.ValheimPath;
		settings.ValheimPath = game;
		settings.BepInExFolders = bep;
		settings.WorldFolders = wl;
		settings.Save();
		if (gameChanged)
		{
			// Another game folder: its look (copied again when it is another version).
			CheckGameLook(settings);
		}
		return null;
	}

	// Tests: what a changed game folder starts (the game-look copy otherwise).
	internal static Action<AppSettings> CheckGameLook { get; set; } = GameLook.Check;

	// Tests: the folder picker (null: the system's).
	internal static Func<string, Task<string?>>? PickFolder { get; set; }

	public static async Task<bool> Show(Window owner, AppSettings settings)
	{
		bool saved = false;
		var dialog = new Window
		{
			Title = "Settings: folders on this computer",
			Width = 720,
			Height = 640,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
		};
		var muted = new SolidColorBrush(Color.FromRgb(142, 151, 166));
		TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 14, 0, 4) };
		TextBlock Hint(string t) => new() { Text = t, FontSize = 12, Foreground = muted, TextWrapping = TextWrapping.Wrap };
		async Task<string?> Pick(string title)
		{
			if (PickFolder is { } pick)
			{
				return await pick(title);
			}
			var picked = await dialog.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = title });
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		}
		var valheim = new TextBox { Text = settings.ValheimPath, Watermark = "automatic (Steam libraries)" }.Tip("settings.valheim");
		var browse = new Button { Content = "Browse…" }.Tip("settings.browse");
		browse.Click += async (_, _) => { if (await Pick("The Valheim game folder (with valheim_Data)") is string p) valheim.Text = p; };
		var auto = new Button { Content = "Automatic" }.Tip("settings.auto");
		auto.Click += (_, _) => valheim.Text = "";
		StackPanel List(List<string> initial, string pickTitle, out Func<IEnumerable<string>> read)
		{
			var rows = new StackPanel { Spacing = 4 };
			var boxes = new List<TextBox>();
			void Add(string text)
			{
				var box = new TextBox { Text = text }.Tip("settings.folder");
				var x = new Button { Content = "✕" }.Tip("settings.remove");
				var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
				var br = new Button { Content = "Browse…" }.Tip("settings.browse");
				br.Click += async (_, _) => { if (await Pick(pickTitle) is string p) box.Text = p; };
				Grid.SetColumn(br, 1);
				Grid.SetColumn(x, 2);
				row.Children.Add(box);
				row.Children.Add(br);
				row.Children.Add(x);
				x.Click += (_, _) => { rows.Children.Remove(row); boxes.Remove(box); };
				boxes.Add(box);
				rows.Children.Add(row);
			}
			foreach (var f in initial)
			{
				Add(f);
			}
			var add = new Button { Content = "Add a folder" }.Tip("settings.add");
			add.Click += (_, _) => Add("");
			read = () => boxes.Select(b => b.Text ?? "");
			return new StackPanel { Spacing = 4, Children = { rows, add } };
		}
		var bep = List(settings.BepInExFolders, "A BepInEx folder or a mod manager profile", out var readBep);
		var worlds = List(settings.WorldFolders, "A world folder or a folder of worlds", out var readWorlds);
		var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap };
		var save = new Button { Content = "Save", IsDefault = true }.Tip("settings.save");
		save.Click += (_, _) =>
		{
			if (Apply(settings, valheim.Text, readBep(), readWorlds()) is string problem)
			{
				error.Text = problem;
				return;
			}
			saved = true;
			dialog.Close();
		};
		var cancel = new Button { Content = "Cancel", IsCancel = true }.Tip("dialog.cancel");
		cancel.Click += (_, _) => dialog.Close();
		string detected = GameLook.FindValheim(null) is string d ? $"Found automatically: {d}" : "Not found automatically: choose the folder.";
		dialog.Content = new ScrollViewer
		{
			Content = new StackPanel
			{
				Margin = new Thickness(18),
				Spacing = 6,
				Children =
				{
					H2("Valheim game folder"),
					Hint(detected),
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4, Children = { valheim, Col(browse, 1), Col(auto, 2) } },
					Hint("The folder with valheim_Data. Used for the game's look, and to find BepInEx for \"My game\"."),
					H2("BepInEx folders"),
					bep,
					Hint("Where else to look for the plugin for \"My game\": a BepInEx folder, a mod manager profile, or a folder of profiles. The Valheim folder and the default r2modman / Thunderstore Mod Manager profiles are always searched."),
					H2("World folders"),
					worlds,
					Hint($"Always listed under \"A saved world\": a world folder, or a folder of worlds (a server's <savedir>/worlds_local, a backup folder…). Valheim's usual folders are always searched: {string.Join(", ", Places.WorldRoots().Where(Directory.Exists))}"),
					error,
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { cancel, save } },
				},
			},
		};
		await dialog.ShowDialog(owner);
		return saved;
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}
}
