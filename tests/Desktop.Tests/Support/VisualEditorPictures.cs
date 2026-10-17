using System.Text.Json;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The pictures the visual tests take with the real editor: a fresh copy of the test world opened, pictures and window
/// shots measured, and the commands sent in turn.
/// </summary>
public abstract class VisualEditorPictures(EditorProcess editor) : IDisposable
{
    /// <summary>
    /// The editor process the tests drive.
    /// </summary>
    protected EditorProcess Editor => editor;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-world-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>
    /// A fresh copy of the test world, opened (its map shown, the whole world in view).
    /// </summary>
    protected JsonElement Open()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        string world = TestWorldCopy.CopyInto(_dir);
        var opened = editor.Send($"world {world}");
        // A world opens where the map was last looked at: the last test's view (a close-up when it failed zoomed in).
        editor.Send(FormattableString.Invariant($"mapview 0 0 {WholeWorld}"));
        return opened;
    }

    /// <summary>
    /// Metres a pixel with the whole world in view (as the map shows a world first).
    /// </summary>
    private const float WholeWorld = 10500 * 2 / 900f;

    /// <summary>
    /// How much a picture shows: average brightness, its spread, distinct colours, share of near-black.
    /// </summary>
    protected PictureLook Picture(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        editor.Send($"picture {path}");
        return PictureLook.Of(path);
    }

    /// <summary>
    /// The 3D view draws with the game's look (copied from Valheim), else in plain colours (no Valheim, as in CI).
    /// </summary>
    protected bool GameLook => editor.Send("state").GetProperty("gameLook").GetBoolean();

    /// <summary>
    /// Fails when the picture is flat or has few colours. The 3D view in plain colours (flat-shaded ground, one colour
    /// a kind of object) has far fewer than the map or the game's textures, but is still no flat fill.
    /// </summary>
    protected void ShowsSomething(PictureLook l)
    {
        var s = editor.Send("state");
        bool plain = s.GetProperty("page").GetString() == "editor" && !s.GetProperty("gameLook").GetBoolean();
        double spread = plain ? 4 : 12;
        int colours = plain ? 20 : 300;
        Assert.True(l.Spread > spread, $"the picture is flat (brightness spread {l.Spread:0.0})");
        Assert.True(l.Colours > colours, $"the picture has few colours ({l.Colours})");
    }

    /// <summary>
    /// The window as the user sees it, panels and view: what the documentation's pictures are.
    /// </summary>
    protected PictureLook Shot(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        editor.Send($"shot {path}");
        Assert.True(File.Exists(path));
        return PictureLook.Of(path);
    }

    /// <summary>
    /// Every picture drawn without an OpenGL error.
    /// </summary>
    protected void Clean() => Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());

    /// <summary>
    /// Sends the commands in turn; returns the last answer.
    /// </summary>
    protected JsonElement Do(params string[] commands)
    {
        JsonElement last = default;
        foreach (string c in commands)
        {
            last = editor.Send(c);
        }
        return last;
    }
}
