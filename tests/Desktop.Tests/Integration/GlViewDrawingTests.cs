using System.Text.Json;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the real editor (OpenGL) draws beyond the usual view: walking and flying, the Mountain tool's preview, the
/// Path tool's next stretch, the Place tool's line and zone, the box of the object under the pointer, and the Dungeon
/// tool over a dungeon of the game's rooms generated into the test world.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class GlViewDrawingTests(EditorProcess editor) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-draw-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>
    /// A fresh copy of the test world, opened, its area around zone 0, 0 in the 3D view (3 zones across), the camera
    /// over the world's middle.
    /// </summary>
    private void OpenArea()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        string world = Path.Combine(_dir, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        editor.Send($"world {world}");
        Do("area 0 0 3", "key Escape", "camera 0 0 0 60 120");
    }

    /// <summary>
    /// Sends the commands in turn; returns the last answer.
    /// </summary>
    private void Do(params string[] commands)
    {
        foreach (string c in commands)
        {
            editor.Send(c);
        }
    }

    /// <summary>
    /// A picture of the view, which must be written.
    /// </summary>
    private void Picture(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        editor.Send($"picture {path}");
        Assert.True(File.Exists(path), path);
    }

    /// <summary>
    /// Every picture drawn without an OpenGL error.
    /// </summary>
    private void Clean() => Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());

    [Fact]
    public void WalkingFlyingTheMountainAndPathPreviewsAndThePlaceToolsLineAndZoneDraw()
    {
        OpenArea();
        // F: walking at eye height, then flying, then back around a point ahead.
        editor.Send("key F");
        Picture("walk");
        editor.Send("key F");
        editor.Send("wait 200");
        editor.Send("key F");
        Do("camera 0 0 0 60 120", "click Mountain", "mouse move 0 0");
        Picture("mountain");
        // The Path tool: the stretch a click would add, from the last point to the pointer.
        Do("key P", "mouse down -30 0", "mouse up -30 0", "mouse move 0 10");
        Picture("path-next");
        editor.Send("key Escape");
        // The Place tool's line: its points and the line through them (Enter places along it).
        Do("key T", "click Line", "mouse down -10 0", "mouse up -10 0", "mouse down 10 0", "mouse up 10 0", "mouse move 15 5");
        Picture("place-line");
        editor.Send("key Enter");
        // A zone: its corners and its outline.
        Do("click Zone", "mouse down -10 -10", "mouse up -10 -10", "mouse down 10 -10", "mouse up 10 -10", "mouse down 0 10", "mouse up 0 10", "mouse move 2 2");
        Picture("place-zone");
        // Select: a click on nothing clears the selection, then the object at the line's start is pointed at.
        Do("key Escape", "key Escape", "key E", "mouse down 40 40", "mouse up 40 40", "mouse move -10 0");
        Picture("hover");
        Clean();
    }

    [Fact]
    public void TheDungeonToolDrawsAGeneratedDungeonOfTheGamesRooms()
    {
        OpenArea();
        editor.Send("click Dungeon");
        // No dungeon in the area: Generate is open, the game's rooms chosen (with a dungeon already there, it is
        // the one drawn).
        if (editor.Answer("choose The game's own rooms").StartsWith("@@ ok ", StringComparison.Ordinal))
        {
            // Place in the world waits for the dungeon to be made (the button is off until then).
            string placed = "";
            for (int i = 0; i < 600 && !placed.StartsWith("@@ ok ", StringComparison.Ordinal); i++)
            {
                Thread.Sleep(100);
                placed = editor.Answer("click Place in the world");
            }
            Assert.StartsWith("@@ ok ", placed);
            string message = JsonDocument.Parse(placed["@@ ok ".Length..]).RootElement.GetProperty("message").GetString()!;
            Assert.StartsWith("Generated ", message);
        }
        Picture("dungeon");
        // Pointed at and clicked where it went (5000 m over the ground there): a room picked, then deleted, which
        // leaves open ends. The point may be out of view (the dungeon is moved clear of others): then nothing is
        // pointed at.
        editor.Answer("mouse3 move 0 5000 0");
        editor.Answer("mouse3 down 0 5000 0");
        editor.Answer("mouse3 up 0 5000 0");
        Picture("dungeon-picked");
        editor.Send("key Delete");
        editor.Answer("mouse3 move 0 5000 0");
        Picture("dungeon-open-ends");
        Clean();
        // The shared editor back in the View tool (the dungeon's cut off) for the other tests.
        editor.Send("key Escape");
    }
}
