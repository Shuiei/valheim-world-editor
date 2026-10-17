using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The app's start (Program), run as the app is (ValheimWorldEditor.dll in a process of its own): the jobs that end at
/// once (Claude's relay with nothing to relay, the exports not told where to write) and a command line that cannot be
/// read, none of which opens a window; the relay answering on its own standard input and output; the handler of SIGTERM
/// and SIGHUP; and the order Windows tries its ways of drawing in.
/// </summary>
public sealed class ProgramTests
{
    /// <summary>
    /// Runs the app with these arguments, its input closed at once, its settings and data in an empty folder of its own
    /// (never the user's, deleted after); its exit code, output and errors.
    /// </summary>
    private static async Task<(int Code, string Output, string Error)> Run(params string[] args)
    {
        string home = EditorApp.MakeHome("vwe-program-");
        try
        {
            using var p = EditorApp.Start(home, args);
            p.StandardInput.Close();
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancel.CancelAfter(TimeSpan.FromMinutes(2));
            var output = p.StandardOutput.ReadToEndAsync(cancel.Token);
            var error = p.StandardError.ReadToEndAsync(cancel.Token);
            try
            {
                await p.WaitForExitAsync(cancel.Token);
            }
            catch (OperationCanceledException)
            {
                p.Kill();
                throw;
            }
            return (p.ExitCode, await output, await error);
        }
        finally
        {
            EditorApp.RemoveHome(home);
        }
    }

    [Fact]
    public async Task ClaudesRelayEndsWithItsInputAndSaysNothingElse()
    {
        var (code, output, _) = await Run("--mcp-stdio");
        Assert.Equal(0, code);
        Assert.Equal("", output);
    }

    // The program as the relay, with settings of its own (Claude not let in): a request is answered with the error on
    // its standard output, and the program ends with its input.
    [Fact]
    public async Task TheProgramRelaysOnItsStandardInputAndOutput()
    {
        string home = EditorApp.MakeHome("vwe-relay-");
        try
        {
            // Its settings from an empty folder: never the user's.
            using var p = EditorApp.Start(home, "--mcp-stdio");
            var errors = p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await p.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
            p.StandardInput.Close();
            string said = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            await p.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            Assert.True(p.ExitCode == 0, await errors);
            Assert.Contains("does not let Claude connect", said);
            Assert.Contains("\"id\":1", said);
        }
        finally
        {
            EditorApp.RemoveHome(home);
        }
    }

    [Fact]
    public async Task AnExportNotToldWhereToWriteSaysSo()
    {
        var (code, _, error) = await Run("--export-test-data");
        Assert.Equal(2, code);
        Assert.Contains("--export-test-data needs the folder", error);
        (code, _, error) = await Run("--export-locations");
        Assert.Equal(2, code);
        Assert.Contains("--export-locations needs the file", error);
    }

    [Fact]
    public async Task ACommandLineThatCannotBeReadStopsTheStart()
    {
        var (code, output, error) = await Run("--world");
        Assert.Equal(2, code);
        Assert.Equal("ValheimWorldEditor: --world needs a value after it.", error.Trim());
        Assert.Equal("", output);
    }

    [Fact]
    public async Task ADataFolderThatCannotBeMadeStopsTheStart()
    {
        // Under a file: no folder can be made there.
        string file = Path.GetTempFileName();
        try
        {
            var (code, _, error) = await Run("--data", Path.Combine(file, "data"));
            Assert.Equal(2, code);
            Assert.StartsWith($"ValheimWorldEditor: --data {Path.Combine(file, "data")} cannot be used: ", error.Trim());
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Direct3D (ANGLE) first unless the driver's OpenGL is chosen in Settings; software last either way.
    [Fact]
    public void WindowsDrawsThroughDirect3DUnlessTheDriversOpenGlIsChosen()
    {
        Assert.Equal([Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software], Program.WindowsRendering(driverOpenGl: false));
        Assert.Equal([Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software], Program.WindowsRendering(driverOpenGl: true));
    }

    [AvaloniaFact]
    public void SigtermIsTakenToCloseTheApp()
    {
        var c = new PosixSignalContext(PosixSignal.SIGTERM);
        Program.Quit(c);
        // The signal does not end the process at once: the app shuts down on its UI thread (the tests' app has no
        // desktop lifetime to shut down).
        Assert.True(c.Cancel);
        Dispatcher.UIThread.RunJobs();
    }
}
