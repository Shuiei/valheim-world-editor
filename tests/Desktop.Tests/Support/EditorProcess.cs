using System.Diagnostics;
using System.Text.Json;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The real editor, drawing with OpenGL: one editor process for the whole group (started once, driven with --driver
/// commands), each test opening a fresh copy of the test world. Needs a display (and a graphics card or Mesa's software
/// renderer); without one the tests skip. Its settings and memory go to a test folder (--data), never the user's. Run
/// only these: --filter-trait Category=Visual; skip them: --filter-not-trait Category=Visual.
/// </summary>
public class EditorProcess : IDisposable
{
    private readonly Process? _p;
    private readonly Lock _sendLock = new();
    // A line asked for and not come yet (its wait timed out): the next wait takes it, as the stream allows one
    // read at a time.
    private Task<string?>? _reading;
    // Commands whose answer has not come (their wait timed out): read off before the next command, so that a late
    // answer is never taken for the next one's.
    private int _owed;
    // An owed answer did not come either: the editor no longer answers, the next commands fail at once.
    private bool _stuck;

    /// <summary>
    /// How long the editor's picture may wait for its view (VWE_PICTURE_WAIT, see Driver.PictureWait): longer under
    /// coverage (CI on main), which measures the editor too and makes its close-ups minutes long.
    /// </summary>
    private static readonly TimeSpan PictureWait = TimeSpan.FromMinutes(Environment.GetEnvironmentVariable("VWE_COVERAGE") == "1" ? 8 : 3);

    public string? Why { get; }

    /// <summary>How long Dispose waits for the editor to quit before killing it.</summary>
    public TimeSpan QuitWait { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>The editor's exit code once it has ended (137 when killed), else null.</summary>
    public int? ExitCode => _p is { HasExited: true } ? _p.ExitCode : null;
    public bool Available => _p != null;
    // The editor's data folder (--data): its settings, log...
    public string Data { get; } = Path.Combine(Path.GetTempPath(), "vwe-visual-" + Guid.NewGuid().ToString("N")[..8]);

    public EditorProcess()
        : this([])
    {
    }

    /// <summary>
    /// Started with more options (the world to open at once, for example), and environment variables.
    /// </summary>
    protected EditorProcess(string[] extra, IReadOnlyDictionary<string, string>? env = null)
    {
        if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Why = "no display to open the editor on";
            return;
        }
        // The copy beside the tests, so that its coverage adds to theirs (EditorApp).
        string app = EditorApp.Path;
        var psi = new ProcessStartInfo(EditorApp.Dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        // The game's data from the tests' copies, as the tests in this process have it (Fixtures.UseGameData): the
        // same with or without Valheim on the computer (CI has none).
        foreach (string a in new[] { app, "--data", Data, "--gamedata", Fixtures.Root, "--driver" }.Concat(extra))
        {
            psi.ArgumentList.Add(a);
        }
        psi.Environment["VWE_PICTURE_WAIT"] = ((int)PictureWait.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var (k, v) in env ?? new Dictionary<string, string>())
        {
            psi.Environment[k] = v;
        }
        _p = Process.Start(psi)!;
        _p.ErrorDataReceived += (_, _) => { };
        _p.BeginErrorReadLine();
        string? first = Next(TimeSpan.FromSeconds(60));
        if (first != "@@ ready")
        {
            Why = "the editor did not start: " + first;
            // Ended before its data folder is deleted (Dispose), which it may still be writing.
            _p.Kill(entireProcessTree: true);
            Assert.True(_p.WaitForExit(15000), "the editor did not end within 15 s of being killed");
            _p = null;
        }
    }

    /// <summary>
    /// The next answer line ("@@ ..."), skipping the editor's other output.
    /// </summary>
    private string? Next(TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var read = _reading ??= _p!.StandardOutput.ReadLineAsync();
            if (!read.Wait(timeout - watch.Elapsed))
            {
                return null;
            }
            _reading = null;
            if (read.Result == null)
            {
                return null;
            }
            if (read.Result.StartsWith("@@ ", StringComparison.Ordinal))
            {
                return read.Result;
            }
        }
        return null;
    }

    /// <summary>
    /// Sends a command; its answer's JSON (an error fails the test).
    /// </summary>
    public JsonElement Send(string command)
    {
        string answer = Answer(command);
        Assert.True(answer.StartsWith("@@ ok ", StringComparison.Ordinal), $"{command}: {answer}");
        return JsonDocument.Parse(answer["@@ ok ".Length..]).RootElement.Clone();
    }

    /// <summary>
    /// Sends a command; its answer line, "@@ ok ..." or the editor's error (only no answer at all fails the test).
    /// </summary>
    public string Answer(string command)
    {
        lock (_sendLock)
        {
            Assert.False(_stuck, $"{command}: the editor stopped answering (an earlier command's answer never came)");
            for (; _owed > 0; _owed--)
            {
                if (Next(PictureWait + TimeSpan.FromMinutes(1)) == null)
                {
                    _stuck = true;
                    Assert.Fail($"{command}: the editor stopped answering (an earlier command's answer never came)");
                }
            }
            _p!.StandardInput.WriteLine(command);
            _p.StandardInput.Flush();
            // Longer than the editor's own wait for a picture (Driver.PictureWait), so its error comes back.
            string? answer = Next(PictureWait + TimeSpan.FromMinutes(1));
            if (answer == null)
            {
                _owed++;
            }
            Assert.True(answer != null, $"{command}: no answer from the editor");
            return answer!;
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (_p != null)
        {
            // An editor that ended before being told to quit failed: telling it then throws, and fails the clean-up.
            _p.StandardInput.WriteLine("quit");
            _p.StandardInput.Flush();
            if (!_p.WaitForExit(QuitWait))
            {
                // Ended before its data folder is deleted, which it may still be writing.
                _p.Kill(entireProcessTree: true);
                Assert.True(_p.WaitForExit(15000), "the editor did not end within 15 s of being killed");
            }
        }
        if (Directory.Exists(Data))
        {
            Directory.Delete(Data, true);
        }
        GC.SuppressFinalize(this);
    }
}
