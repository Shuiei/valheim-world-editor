using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Frame rates, in the same format as the web editor's logs (perf-window.log, perf-browser.log), in perf-native.log in
/// the data folder, to compare: a sample every 0.2 s while the view is used (the camera moving, or still while
/// something else happens), with a header line saying what is shown. Written only while switched on (Record frame
/// rates).
/// </summary>
public sealed class PerfLog
{
    /// <summary>
    /// Whether Record frame rates is switched on; frames are still counted while off, but nothing is written.
    /// </summary>
    public bool On { get; set; }
    /// <summary>perf-native.log in the data folder (rolled over to .old past 5 MB).</summary>
    public static string FilePath => Path.Combine(AppSettings.DataDir, "perf-native.log");

    /// <summary>When the current 0.2 s sample began (ms), or -1 before the first frame.</summary>
    private long _t0 = -1;
    /// <summary>Whether the camera moved, or something else kept the view busy, during the current sample.</summary>
    private bool _moved, _busy;
    /// <summary>Frames drawn in the current sample.</summary>
    private int _frames;
    /// <summary>The current sample's total drawing work and longest gap between frames, in ms.</summary>
    private double _work, _gap;
    /// <summary>When the last frame was drawn (ms), 0 before the first.</summary>
    private long _lastFrame;
    /// <summary>What the last header line said is shown; a new header is written when it changes.</summary>
    private string _header = "";
    /// <summary>Lines waiting to be written to the file.</summary>
    private readonly List<string> _lines = [];
    /// <summary>When the lines were last written (ms); they are written at most every 2 s.</summary>
    private long _sentAt;

    /// <summary>
    /// Counts one drawn frame; every 0.2 s closes the sample and, when on and the view was in use, adds a line.
    /// </summary>
    /// <param name="now">The time now, in ms.</param>
    /// <param name="work">How long drawing this frame took, in ms.</param>
    /// <param name="moved">The camera moved this frame.</param>
    /// <param name="busy">Something other than the camera kept the view drawing.</param>
    /// <param name="about">What is shown (view size, renderer, world, objects, area), for the header line.</param>
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
                _lines.Add($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{BuildInfo.Version}  native ({(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux")})  {about}");
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

    /// <summary>
    /// Writes the waiting lines to the file. A file that cannot be written (a read-only data folder) loses them; the
    /// first such failure is logged, and the frames go on.
    /// </summary>
    /// <param name="now">The time now in ms, to space out the next write.</param>
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_writeFailed)
            {
                _writeFailed = true;
                Console.Error.WriteLine($"Performance log not written ({FilePath}): {ex.Message}");
            }
        }
        finally
        {
            _lines.Clear();
        }
    }

    /// <summary>A write of the file failed already (said once in the log).</summary>
    private bool _writeFailed;

    /// <summary>Makes the next sample start with a header line again.</summary>
    public void Restart() => _header = "";
}
