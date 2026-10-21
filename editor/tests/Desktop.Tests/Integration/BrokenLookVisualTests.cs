using System.Diagnostics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A game whose terrain shader does not compile (BrokenLookEditorProcess): the game look fails to set up on the
/// graphics card, the failure is said with the shader's log, and the area still draws in plain colours without an
/// OpenGL error.
/// </summary>
[Collection("Visual broken look")]
[Trait("Category", "Visual")]
public sealed class BrokenLookVisualTests(BrokenLookEditorProcess editor) : VisualPicturesTestBase("broken-look-pics")
{
    [Fact]
    public void AShaderThatDoesNotCompileIsSaidAndTheAreaDrawsPlainly()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the made-up game is found in the home's Steam library only on Linux");
        Directory.CreateDirectory(Dir);
        GameLookVisualTests.OpenArea(editor, 1);
        // The look's files are read in the background, then fail on the graphics card at a frame: said in the log.
        var watch = Stopwatch.StartNew();
        string log;
        while (!(log = Log()).Contains("setting up the game's look for drawing", StringComparison.Ordinal))
        {
            Assert.True(watch.Elapsed < TimeSpan.FromMinutes(3), "the broken shader was never tried: " + string.Join(" / ", log.Split('\n').Where(l => l.Contains("ame look", StringComparison.Ordinal))));
            editor.Send("wait 200");
        }
        Assert.Contains("FragmentShader", log);
        string path = Path.Combine(Dir, "plain.png");
        Assert.Equal("editor", editor.Send($"picture {path}").GetProperty("page").GetString());
        double dark = PictureLook.Of(path).Dark;
        Assert.True(dark < 0.5, $"{dark:P0} of the picture is black");
        Assert.False(editor.Send("state").GetProperty("gameLook").GetBoolean());
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        GameLookVisualTests.BackToStart(editor);
    }

    /// <summary>What the editor said so far (its log in its data folder).</summary>
    private string Log() => File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));
}
