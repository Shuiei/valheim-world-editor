using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game look drawn by the real editor from a made-up Valheim's files (GameLookEditorProcess): the terrain shader
/// linked with the view's lighting, its textures, the sky and the water; the View panel's slope colours and limit marks
/// on it, a brush stroke sending the changed rows again, and another area setting it up anew.
/// </summary>
[Collection("Visual game look")]
[Trait("Category", "Visual")]
public sealed class GameLookVisualTests(GameLookEditorProcess editor) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-look-pics-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public void TheGamesLookIsReadFromItsFilesAndDrawn()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the made-up game is found in the home's Steam library only on Linux");
        Directory.CreateDirectory(_dir);
        BackToStart(editor);
        OpenArea(editor, 2);
        WaitForGameLook(editor);
        editor.Send("camera 32 32 30 35 120");
        AssertDrawn(Picture("look"));
        // The View panel's slope colours on, the limit marks off, then back.
        editor.Send("panel view");
        editor.Send("check Slope colours|on");
        editor.Send("check Limit marks|off");
        AssertDrawn(Picture("slope"));
        editor.Send("check Slope colours|off");
        editor.Send("check Limit marks|on");
        // A stroke: the changed rows' mask and heights go to the graphics card again.
        Assert.True(editor.Send("stroke raise 32 32 5").GetProperty("pending").GetInt32() >= 1);
        AssertDrawn(Picture("stroke"));
        // Another area: the last one's look goes, a new one is set up for it.
        editor.Send("area 0 0 1");
        WaitForGameLook(editor);
        AssertDrawn(Picture("another"));
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        Assert.Contains("Game look loaded.", Log(editor));
        editor.Send("panel none");
        BackToStart(editor);
    }

    /// <summary>
    /// Back on the start page from wherever the last test left the editor (the edits left pending are dropped).
    /// </summary>
    internal static void BackToStart(EditorProcess editor)
    {
        string page = editor.Send("state").GetProperty("page").GetString()!;
        if (page == "editor")
        {
            page = editor.Send("map").GetProperty("page").GetString()!;
        }
        if (page == "map" && editor.Send("click Worlds").GetProperty("page").GetString() == "map")
        {
            // Something pending: the question whether to leave anyway.
            editor.Send("click Leave anyway");
        }
        DirectVisualTests.WaitForPage(editor, "start");
    }

    /// <summary>
    /// The copy of the test world opened, and its area at zone 0, 0 this many zones across in the 3D view.
    /// </summary>
    internal static void OpenArea(EditorProcess editor, int size)
    {
        editor.Send($"world {GameLookEditorProcess.World}");
        Assert.Equal("editor", editor.Send($"area 0 0 {size}").GetProperty("page").GetString());
    }

    /// <summary>
    /// Waits for the view to have the game look set up (its files read, then sent to the graphics card at a frame).
    /// </summary>
    private static void WaitForGameLook(EditorProcess editor)
    {
        var watch = Stopwatch.StartNew();
        while (!editor.Send("wait 200").GetProperty("gameLook").GetBoolean())
        {
            if (watch.Elapsed > TimeSpan.FromMinutes(3))
            {
                Assert.Fail("the game look was not set up: " + string.Join(" / ", Log(editor).Split('\n').Where(l => l.Contains("ame look", StringComparison.Ordinal))));
            }
        }
    }

    /// <summary>
    /// What the editor said so far (its log in its data folder).
    /// </summary>
    private static string Log(EditorProcess editor) => File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));

    /// <summary>
    /// A picture of the 3D view; its share of near-black pixels, every third pixel each way.
    /// </summary>
    private double Picture(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        JsonElement s = editor.Send($"picture {path}");
        Assert.Equal("editor", s.GetProperty("page").GetString());
        using var bmp = SKBitmap.Decode(path);
        Assert.NotNull(bmp);
        int dark = 0, n = 0;
        for (int y = 0; y < bmp.Height; y += 3)
        {
            for (int x = 0; x < bmp.Width; x += 3)
            {
                var c = bmp.GetPixel(x, y);
                dark += 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue < 12 ? 1 : 0;
                n++;
            }
        }
        return dark / (double)n;
    }

    /// <summary>
    /// Fails when most of the picture is black (nothing drawn: the sky alone fills what the ground does not).
    /// </summary>
    private static void AssertDrawn(double dark) => Assert.True(dark < 0.5, $"{dark:P0} of the picture is black");
}
