using System.Text.Json;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Started straight in the 3D editor (--world, --zone): the area is open, and the benchmark runs.
/// </summary>
[Collection("Visual direct")]
[Trait("Category", "Visual")]
public sealed class DirectVisualTests(DirectEditorProcess editor)
{
    [Fact]
    public void TheWorldOpensStraightInTheEditorAndTheBenchmarkRuns()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        // The area loads after the window opens: wait for it.
        JsonElement s = editor.Send("state");
        for (int i = 0; i < 200 && s.GetProperty("page").GetString() != "editor"; i++)
        {
            Thread.Sleep(100);
            s = editor.Send("state");
        }
        Assert.Equal("editor", s.GetProperty("page").GetString());
        Assert.True(s.GetProperty("objects").GetInt32() > 0);
        string bench = editor.Send("bench 1").GetProperty("bench").GetString()!;
        Assert.Matches(@"^\d+ fps over \d+\.\d s, work .* ms, gaps median \d+ ms, 95% \d+ ms, max \d+ ms, view \d+×\d+ px$", bench);
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        // What it said is in its log, in the tests' data folder.
        string log = File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));
        Assert.StartsWith($"Valheim World Editor {BuildInfo.Version}, ", log);
        Assert.Contains("window open", log);
    }

    /// <summary>
    /// Waits (up to 30 s) for the editor to show this page; returns its state.
    /// </summary>
    internal static JsonElement WaitForPage(EditorProcess editor, string page)
    {
        JsonElement s = editor.Send("state");
        for (int i = 0; i < 300 && s.GetProperty("page").GetString() != page; i++)
        {
            Thread.Sleep(100);
            s = editor.Send("state");
        }
        Assert.Equal(page, s.GetProperty("page").GetString());
        return s;
    }
}
