using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Record frame rates in the real editor (PerfLog): while switched on and the view turns, samples are written to
/// perf-native.log in its data folder, a log past 5 MB rolled over to .old first.
/// </summary>
[Collection("Visual game look")]
[Trait("Category", "Visual")]
public sealed class PerfLogVisualTests(GameLookEditorProcess editor)
{
    [Fact]
    public void FrameRatesAreWrittenWhileRecordFrameRatesIsOn()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        GameLookVisualTests.BackToStart(editor);
        string log = Path.Combine(editor.Data, "perf-native.log");
        File.WriteAllBytes(log, new byte[5_000_001]);
        GameLookVisualTests.OpenArea(editor, 1);
        editor.Send("panel help");
        editor.Send("check Record frame rates|on");
        try
        {
            // The camera turns on its own: frames drawn while moving.
            editor.Send("bench 2");
        }
        finally
        {
            editor.Send("check Record frame rates|off");
            editor.Send("panel none");
        }
        Assert.Equal(5_000_001, new FileInfo(log + ".old").Length);
        string[] lines = File.ReadAllLines(log);
        Assert.StartsWith("# ", lines[0]);
        Assert.Contains(lines, l => l.Contains("moving", StringComparison.Ordinal) && l.Contains(" fps ", StringComparison.Ordinal));
        GameLookVisualTests.BackToStart(editor);
    }
}
