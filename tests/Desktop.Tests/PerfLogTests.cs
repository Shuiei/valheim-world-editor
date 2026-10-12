using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The frame-rate log: nothing written while off or while the view sits idle, a header when what is shown changes,
/// a line per 0.2 s sample, written at most every 2 s, and the file rolled over past 5 MB. Its data folder is the
/// test's own; it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class PerfLogTests : IDisposable
{
    private readonly string? _data = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-perf-" + Guid.NewGuid().ToString("N")[..8]);

    public PerfLogTests()
    {
        Directory.CreateDirectory(_dir);
        AppSettings.DataDirOverride = _dir;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        AppSettings.DataDirOverride = _data;
        Directory.Delete(_dir, recursive: true);
    }

    /// <summary>The log's lines, none when it was not written.</summary>
    private static string[] Lines() => File.Exists(PerfLog.FilePath) ? File.ReadAllLines(PerfLog.FilePath) : [];

    /// <summary>Frames every 20 ms from <paramref name="from"/> to <paramref name="to"/> (ms).</summary>
    private static void Frames(PerfLog log, long from, long to, bool moved, string about = "1000x800 world")
    {
        for (long t = from; t <= to; t += 20)
        {
            log.Frame(t, 4, moved, busy: false, about);
        }
    }

    [Fact]
    public void SamplesAreWrittenOnlyWhileOnAndTheViewIsUsed()
    {
        Assert.Equal(Path.Combine(_dir, "perf-native.log"), PerfLog.FilePath);
        var log = new PerfLog();
        // Off: counted, not written.
        Frames(log, 1000, 4000, moved: true);
        log.Flush();
        Assert.Empty(Lines());
        // On but idle: nothing either.
        log.On = true;
        Frames(log, 4020, 7000, moved: false);
        log.Flush();
        Assert.Empty(Lines());
        // Moving: a header, then a line per sample, written every 2 s.
        Frames(log, 7020, 10000, moved: true);
        string[] lines = Lines();
        Assert.StartsWith("# ", lines[0]);
        Assert.Contains($"v{BuildInfo.Version}", lines[0]);
        Assert.EndsWith("1000x800 world", lines[0]);
        Assert.Contains(lines, l => l.Contains("moving", StringComparison.Ordinal) && l.Contains(" fps ", StringComparison.Ordinal) && l.Contains("ms work", StringComparison.Ordinal));
        Assert.Equal(1, lines.Count(l => l.StartsWith('#')));
        // Something else shown: a new header; Restart writes it again even when the same.
        Frames(log, 10020, 10500, moved: true, about: "1000x800 other");
        log.Restart();
        Frames(log, 10520, 11000, moved: true, about: "1000x800 other");
        log.Flush();
        Assert.Equal(3, Lines().Count(l => l.StartsWith('#')));
    }

    [Fact]
    public void ABigLogIsRolledOver()
    {
        File.WriteAllBytes(PerfLog.FilePath, new byte[5_000_001]);
        var log = new PerfLog { On = true };
        Frames(log, 1000, 1400, moved: true);
        log.Flush();
        Assert.Equal(5_000_001, new FileInfo(PerfLog.FilePath + ".old").Length);
        Assert.StartsWith("# ", Lines()[0]);
    }
}
