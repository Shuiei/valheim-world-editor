using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor process's commands: the next answer line read off the editor's output, a command sent and its answer taken
/// (an answer that never comes is owed and read off before the next command).
/// </summary>
public partial class EditorProcess
{
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
}
