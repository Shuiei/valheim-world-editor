using System.Diagnostics;
using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Starting the driven app and talking to it: its answer lines and the commands sent to it.
/// </summary>
public sealed partial class Editor
{
    /// <summary>
    /// Starts the app driven in the stand-in home: run with dotnet when it is a dll, with its window size and the extra
    /// arguments.
    /// </summary>
    /// <param name="app">The app's dll or executable; null for the Debug build in editor/Desktop/bin.</param>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <param name="extra">More command-line arguments for the app.</param>
    /// <param name="data">The data folder the app reads (XDG_DATA_HOME).</param>
    /// <returns>The started process.</returns>
    private Process StartApp(string? app, int width, int height, IReadOnlyList<string>? extra, string data)
    {
        app ??= Path.Combine(Repo, "editor", "Desktop", "bin", "Debug", "net10.0", "ValheimWorldEditor.dll");
        bool dll = app.EndsWith(".dll", StringComparison.Ordinal);
        var start = new ProcessStartInfo(dll ? "dotnet" : app)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (dll)
        {
            start.ArgumentList.Add(app);
        }
        foreach (string a in new[] { "--driver", "--window", $"{width}x{height}" }.Concat(extra ?? []))
        {
            start.ArgumentList.Add(a);
        }
        start.Environment["HOME"] = _home;
        start.Environment["XDG_DATA_HOME"] = data;
        start.Environment["XDG_CONFIG_HOME"] = Path.Combine(_home, ".config");
        return Process.Start(start) ?? throw new InvalidOperationException("the editor did not start");
    }

    /// <summary>The app's next answer line (starting "@@ "); other output is skipped.</summary>
    private string Answer()
    {
        while (true)
        {
            string line = _p.StandardOutput.ReadLine() ?? throw new InvalidOperationException("the editor stopped");
            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                return line;
            }
        }
    }

    /// <summary>One command; its answer's state (an error answer throws).</summary>
    /// <returns>The JSON state after "@@ ok ", cloned so it outlives the answer.</returns>
    public JsonElement Send(string command)
    {
        _p.StandardInput.WriteLine(command);
        _p.StandardInput.Flush();
        string answer = Answer();
        if (!answer.StartsWith("@@ ok ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{command}: {answer}");
        }
        using var doc = JsonDocument.Parse(answer["@@ ok ".Length..]);
        return doc.RootElement.Clone();
    }
}
