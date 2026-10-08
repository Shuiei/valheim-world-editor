using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// What the editor remembers between runs, as the web editor kept it in the browser: the View panel's
// switches, the map's place, the brush's shape and falloff, the Area action, the Shape preset (and a
// formula of one's own), Select's options, which right-hand panel is open, the clipboard. In
// prefs.json in the data folder; a missing or broken file means the defaults. Each value has its own
// name, so remembering another control is one Bind line where it is made. Saved a moment after the
// last change (a dragged map writes once), and on Flush.
public sealed class Prefs
{
	private readonly string? _path;
	private readonly Dictionary<string, JsonNode?> _values;
	private readonly object _lock = new();
	private Timer? _timer;

	// Tests: another file, never the user's.
	internal static string? PathOverride { get; set; }

	public static string DefaultPath => PathOverride ?? Path.Combine(AppSettings.DataDir, "prefs.json");

	private Prefs(string? path, Dictionary<string, JsonNode?> values)
	{
		_path = path;
		_values = values;
	}

	// The remembered values (from DefaultPath unless another file is given).
	public static Prefs Load(string? path = null)
	{
		path ??= DefaultPath;
		var values = new Dictionary<string, JsonNode?>();
		try
		{
			if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o)
			{
				foreach (var (k, v) in o)
				{
					values[k] = v?.DeepClone();
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
		}
		return new Prefs(path, values);
	}

	// Remembered only while the program runs (the tests' windows: never written, never shared).
	public static Prefs InMemory() => new(null, new());

	public bool Has(string key)
	{
		lock (_lock)
		{
			return _values.ContainsKey(key);
		}
	}

	// The value remembered under this name, when there is one of that type.
	public bool TryGet<T>(string key, out T value)
	{
		lock (_lock)
		{
			value = default!;
			if (!_values.TryGetValue(key, out var node) || node == null)
			{
				return false;
			}
			try
			{
				if (node.Deserialize<T>() is T v)
				{
					value = v;
					return true;
				}
			}
			catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NotSupportedException)
			{
			}
			return false;
		}
	}

	public T Get<T>(string key, T fallback) => TryGet(key, out T v) ? v : fallback;

	public void Set<T>(string key, T value)
	{
		lock (_lock)
		{
			_values[key] = JsonSerializer.SerializeToNode(value);
			Later();
		}
	}

	public void Remove(string key)
	{
		lock (_lock)
		{
			if (_values.Remove(key))
			{
				Later();
			}
		}
	}

	private void Later()
	{
		if (_path == null)
		{
			return;
		}
		_timer ??= new Timer(_ => Flush());
		_timer.Change(300, Timeout.Infinite);
	}

	// Writes the file now (a file that cannot be written is left as it was: the values stay in memory).
	public void Flush()
	{
		if (_path == null)
		{
			return;
		}
		string text;
		lock (_lock)
		{
			_timer?.Change(Timeout.Infinite, Timeout.Infinite);
			var o = new JsonObject();
			foreach (var (k, v) in _values.OrderBy(kv => kv.Key, StringComparer.Ordinal))
			{
				o[k] = v?.DeepClone();
			}
			text = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
		}
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
			string temp = _path + ".tmp";
			File.WriteAllText(temp, text);
			File.Move(temp, _path, true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
		}
	}

	// A switch (CheckBox, ToggleButton): set from what is remembered, remembered when changed.
	public void Bind(ToggleButton box, string key)
	{
		if (TryGet(key, out bool on))
		{
			box.IsChecked = on;
		}
		box.IsCheckedChanged += (_, _) => Set(key, box.IsChecked == true);
	}

	// A list of texts (ComboBox): the entry remembered by its text, so a list that changes order or
	// grows keeps the choice; an entry no longer there leaves the list as it is.
	public void Bind(ComboBox box, string key)
	{
		Restore(box, key);
		box.SelectionChanged += (_, _) =>
		{
			if (box.SelectedItem is string text)
			{
				Set(key, text);
			}
		};
	}

	// Chooses the remembered entry, when the list has it (for lists filled again later).
	public bool Restore(ComboBox box, string key)
	{
		if (TryGet(key, out string? text) && text != null && box.ItemsSource is IEnumerable<object> items)
		{
			int i = items.Select(o => o as string).ToList().IndexOf(text);
			if (i >= 0)
			{
				box.SelectedIndex = i;
				return true;
			}
		}
		return false;
	}
}
