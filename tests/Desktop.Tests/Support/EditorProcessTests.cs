using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor the visual tests drive (EditorProcess) disposed: told to quit, it ends by itself; one that does not end
/// in time is killed and waited for. Either way its data folder goes. Needs a display, as the visual tests do.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class EditorProcessTests
{
    /// <summary>Skips the test, with the reason, when the editor could not start (no display).</summary>
    private static void NeedStarted(EditorProcess editor)
    {
        if (!editor.Available)
        {
            editor.Dispose();
            Assert.Skip(editor.Why ?? "the editor did not start");
        }
    }

    [Fact]
    public void AnEditorToldToQuitEndsByItselfAndItsDataGoes()
    {
        var editor = new EditorProcess();
        NeedStarted(editor);

        editor.Dispose();

        Assert.Equal(0, editor.ExitCode);
        Assert.False(Directory.Exists(editor.Data));
    }

    [Fact]
    public void AnEditorThatDoesNotEndInTimeIsKilledAndWaitedFor()
    {
        // No wait at all: the editor is still closing its window when the time is up.
        var editor = new EditorProcess { QuitWait = TimeSpan.Zero };
        NeedStarted(editor);

        editor.Dispose();

        // Killed: ended by a signal (128 + SIGKILL) on Linux and macOS, with -1 on Windows.
        Assert.Equal(OperatingSystem.IsWindows() ? -1 : 137, editor.ExitCode);
        Assert.False(Directory.Exists(editor.Data));
    }
}
