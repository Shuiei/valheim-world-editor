using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Frame rates, in the same format as the web editor's logs (perf-window.log, perf-browser.log), in
// perf-native.log in the data folder, to compare: a sample every 0.2 s while the view is used (the
// camera moving, or still while something else happens), with a header line saying what is shown.
// Written only while switched on (Record frame rates).
public sealed class PerfLog
{
	public bool On { get; set; }
	public static string FilePath => Path.Combine(AppSettings.DataDir, "perf-native.log");

	private long _t0 = -1;
	private bool _moved, _busy;
	private int _frames;
	private double _work, _gap;
	private long _lastFrame;
	private string _header = "";
	private readonly List<string> _lines = new();
	private long _sentAt;

	public void Frame(long now, double work, bool moved, bool busy, string about)
	{
		if (_t0 < 0)
		{
			_t0 = now;
		}
		_frames++;
		_work += work;
		if (_lastFrame > 0)
		{
			_gap = Math.Max(_gap, now - _lastFrame);
		}
		_lastFrame = now;
		_moved |= moved;
		_busy |= busy;
		if (now - _t0 < 200)
		{
			return;
		}
		if (On && (_moved || _busy))
		{
			if (about != _header)
			{
				_header = about;
				_lines.Add($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{BuildInfo.Version}  native ({(OperatingSystem.IsWindows() ? "Windows" : "Linux")})  {about}");
			}
			double span = now - _t0;
			_lines.Add($"{DateTime.Now:HH:mm:ss.fff}  {(_moved ? "moving" : "still ")}  {Math.Round(_frames / (span / 1000)),3} fps  {_frames,2} frames  {_work / _frames,5:0.0} ms work  longest gap {Math.Round(_gap)} ms");
		}
		_t0 = now;
		_frames = 0;
		_work = _gap = 0;
		_moved = _busy = false;
		if (_lines.Count > 0 && now - _sentAt > 2000)
		{
			Flush(now);
		}
	}

	public void Flush(long now = 0)
	{
		_sentAt = now;
		if (_lines.Count == 0)
		{
			return;
		}
		try
		{
			if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 5_000_000)
			{
				File.Move(FilePath, FilePath + ".old", overwrite: true);
			}
			File.AppendAllLines(FilePath, _lines);
		}
		catch (IOException)
		{
		}
		_lines.Clear();
	}

	public void Restart() => _header = "";
}
