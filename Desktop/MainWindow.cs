using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The prototype's window: the 3D view filling it, and a small panel with the frame rate, what is
// loaded, and the Record frame rates switch.
public sealed class MainWindow : Window
{
	private readonly GlView _view = new();
	private readonly TextBlock _fps = new() { FontSize = 13 }, _info = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly PerfLog _perf = new();
	private readonly TextBlock _selection = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 166, 75)), TextWrapping = TextWrapping.Wrap };
	private ModelStore? _models;
	private string Name(WorldScene.Thing t) => _models?.NameOf(t.Prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(t.Prefab) ?? t.Prefab.ToString();

	public GlView View => _view;

	// The View panel (top right): which kinds of objects are drawn, with how many the area has, and the water.
	private readonly Dictionary<ObjectKind, CheckBox> _kindBoxes = new();
	internal IReadOnlyDictionary<ObjectKind, CheckBox> KindBoxes => _kindBoxes;
	internal CheckBox WaterBox { get; } = new() { Content = "Water", IsChecked = true, FontSize = 12 };

	private Control ViewPanel()
	{
		var list = new StackPanel { Spacing = 2 };
		list.Children.Add(new TextBlock { Text = "VIEW", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
		foreach (var k in ObjectKinds.All)
		{
			var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = _view.IsShown(k), FontSize = 12 };
			box.IsCheckedChanged += (_, _) => _view.SetShown(k, box.IsChecked == true);
			_kindBoxes[k] = box;
			list.Children.Add(box);
		}
		WaterBox.IsCheckedChanged += (_, _) => _view.ShowWater = WaterBox.IsChecked == true;
		list.Children.Add(WaterBox);
		_view.KindCounts += counts =>
		{
			foreach (var (k, box) in _kindBoxes)
			{
				box.Content = $"{ObjectKinds.Label(k)}  ({counts.GetValueOrDefault(k):N0})";
			}
		};
		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(12, 10),
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Child = list,
		};
	}

	// load: false opens the window without a world (tests).
	public MainWindow(bool load = true)
	{
		Title = "Valheim World Editor (native preview)";
		Width = 1500;
		Height = 950;
		Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
		var record = new CheckBox { Content = "Record frame rates", FontSize = 12 };
		record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) _perf.Flush(); };
		ToolTip.SetTip(record, $"A line every 0.2 s while the view is used, in {PerfLog.FilePath}");
		var panel = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(12, 10),
			Margin = new Thickness(10),
			MaxWidth = 380,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel { Spacing = 6, Children = { _fps, _info, _selection, record } },
		};
		// Takes the mouse for the 3D view (see GlView.Attach).
		var surface = new Border { Background = Brushes.Transparent };
		Content = new Grid { Children = { _view, surface, panel, ViewPanel() } };
		_view.Attach(surface, this);
		_view.Perf = _perf;
		_view.StatsChanged += s => _fps.Text = $"{s.Fps} frames/s · {s.WorkMs:0.0} ms of work each · {s.Objects:N0} objects ({s.Instances:N0} model parts in {s.Batches:N0} draws){(s.PendingModels > 0 ? $" · {s.PendingModels} kinds loading" : "")}";
		_view.SelectionChanged += things => _selection.Text = things.Count == 0 ? "" : things.Count == 1
			? $"Selected: {Name(things[0])} at {things[0].Position.X:0.0}, {things[0].Position.Z:0.0} (height {things[0].Position.Y:0.0})"
			: $"Selected: {things.Count} objects ({string.Join(", ", things.GroupBy(Name).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}"))})";
		_view.Status += t => { Options.Say(t); Dispatcher.UIThread.Post(() => _info.Text = t + "\n" + _info.Text); };
		Closing += (_, _) => _perf.Flush();
		_info.Text = "Loading the world…";
		Opened += async (_, _) =>
		{
			if (!load)
			{
				return;
			}
			Options.Say("window open");
			try
			{
				var scene = await Task.Run(() => WorldScene.Load(WorldScene.FindWorld(Options.World), Options.ZoneX, Options.ZoneZ, Options.Size));
				var models = await Task.Run(ModelStore.Open);
				_models = models;
				_info.Text = scene.LoadInfo + (models == null ? "\nNo game models copied yet: boxes stand in (open the web editor once to copy the game's look)." : "")
					+ "\nClick picks an object (Shift adds) · right drag turns · middle or left drag slides · wheel zooms · WASD moves";
				_view.Show(scene, models);
			}
			catch (Exception ex)
			{
				_info.Text = "Could not open the world: " + ex.Message;
				Options.Say(_info.Text);
			}
		};
	}
}
