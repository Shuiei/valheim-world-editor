using System.Diagnostics;
using System.Text.Json;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The real editor, drawing with OpenGL: one editor process for the whole group (started once, driven with --driver
/// commands), each test opening a fresh copy of the test world. Needs a display (and a graphics card or Mesa's software
/// renderer); without one the tests skip. Its settings and memory go to a test folder (--data), never the user's. Run
/// only these: --filter Category=Visual; skip them: --filter Category!=Visual.
/// </summary>
public class EditorProcess : IDisposable
{
    private readonly Process? _p;
    private readonly Lock _sendLock = new();

    public string? Why { get; }
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
        string root = Path.GetFullPath(Path.Combine(Fixtures.Root, "..", ".."));
        string config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        string app = Path.Combine(root, "Desktop", "bin", config, "net10.0", "ValheimWorldEditor.dll");
        string dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var psi = new ProcessStartInfo(dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        // The game's data from the tests' copies, as the tests in this process have it (Fixtures.UseGameData): the
        // same with or without Valheim on the computer (CI has none).
        foreach (string a in new[] { app, "--data", Data, "--gamedata", Fixtures.Root, "--driver" }.Concat(extra))
        {
            psi.ArgumentList.Add(a);
        }
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
            _p.Kill();
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
            var read = _p!.StandardOutput.ReadLineAsync();
            if (!read.Wait(timeout - watch.Elapsed))
            {
                return null;
            }
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
        lock (_sendLock)
        {
            _p!.StandardInput.WriteLine(command);
            _p.StandardInput.Flush();
            // Longer than the editor's own wait for a picture (Driver.PictureWait), so its error comes back.
            string? answer = Next(TimeSpan.FromMinutes(4));
            Assert.NotNull(answer);
            Assert.True(answer!.StartsWith("@@ ok ", StringComparison.Ordinal), $"{command}: {answer}");
            return JsonDocument.Parse(answer["@@ ok ".Length..]).RootElement.Clone();
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (_p != null)
        {
            try
            {
                _p.StandardInput.WriteLine("quit");
                _p.StandardInput.Flush();
                if (!_p.WaitForExit(15000))
                {
                    _p.Kill();
                }
            }
            catch
            {
            }
        }
        try
        {
            Directory.Delete(Data, true);
        }
        catch
        {
        }
        GC.SuppressFinalize(this);
    }
}
