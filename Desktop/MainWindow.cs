using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// The prototype's window: the 3D view filling it, and a small panel with the frame rate, what is
// loaded, and the Record frame rates switch.
public sealed class MainWindow : Window
{
	private readonly GlView _view = new();
	private readonly TextBlock _fps = new() { FontSize = 13 }, _info = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly PerfLog _perf = new();

	public MainWindow()
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
			Child = new StackPanel { Spacing = 6, Children = { _fps, _info, record } },
		};
		Content = new Grid { Children = { _view, panel } };
		_view.Perf = _perf;
		_view.StatsChanged += s => _fps.Text = $"{s.Fps} frames/s · {s.WorkMs:0.0} ms of work each · {s.Objects:N0} objects ({s.Instances:N0} model parts in {s.Batches:N0} draws){(s.PendingModels > 0 ? $" · {s.PendingModels} kinds loading" : "")}";
		_view.Status += t => { Options.Say(t); Dispatcher.UIThread.Post(() => _info.Text = t + "\n" + _info.Text); };
		Closing += (_, _) => _perf.Flush();
		_info.Text = "Loading the world…";
		Opened += async (_, _) =>
		{
			Options.Say("window open");
			try
			{
				var scene = await Task.Run(() => WorldScene.Load(WorldScene.FindWorld(Options.World), Options.ZoneX, Options.ZoneZ, Options.Size));
				var models = await Task.Run(ModelStore.Open);
				_info.Text = scene.LoadInfo + (models == null ? "\nNo game models copied yet: boxes stand in (open the web editor once to copy the game's look)." : "")
					+ "\nRight drag turns · middle or left drag slides · wheel zooms · WASD moves";
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
