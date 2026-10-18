using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game look drawn by the real editor from a made-up Valheim's files (GameLookEditorProcess): the terrain shader
/// linked with the view's lighting, its textures, the sky and the water; the View panel's slope colours and limit marks
/// on it, a brush stroke sending the changed rows again, and another area setting it up anew. And the game's models drawn
/// for the objects that have one (a see-through chest, a cut-out tree, a plain rock), the object under the pointer picked
/// by its mesh, the models' boxes and a benchmark run through the driver.
/// </summary>
[Collection("Visual game look")]
[Trait("Category", "Visual")]
public sealed class GameLookVisualTests(GameLookEditorProcess editor) : VisualPicturesTestBase("look-pics")
{
    [Fact]
    public void TheGamesLookIsReadFromItsFilesAndDrawn()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the made-up game is found in the home's Steam library only on Linux");
        Directory.CreateDirectory(Dir);
        BackToStart(editor);
        OpenArea(editor, 2);
        WaitForGameLook(editor);
        editor.Send("camera 32 32 30 35 120");
        AssertDrawn(Picture("look"));
        // The models of the game's files: drawn (the picture waits for them), the chest see-through over what is behind
        // it, in the usual view and with the buildings see-through; the chest picked by its mesh under the pointer.
        Assert.True(editor.Send("state").GetProperty("models").GetBoolean());
        // The camera on the chest's ground point, 30 m off and 45° down; the pointer 1.5 m up the chest's square.
        editor.Send("camera -11.5 -23.5 0 45 30");
        AssertDrawn(Picture("models"));
        editor.Send("look seethrough on");
        AssertDrawn(Picture("models-seethrough"));
        editor.Send("look seethrough off");
        editor.Send("key E");
        editor.Send("mouse3 move -11.5 1.5 -23.5");
        AssertDrawn(Picture("models-hover"));
        editor.Send("key Escape");
        // The models' boxes, by their meshes: the square's 3 m, none for a kind without a model.
        JsonElement boxes = editor.Send("bounds piece_chest_wood Nothing");
        float[] chest = [.. boxes.GetProperty("piece_chest_wood").EnumerateArray().Select(v => v.GetSingle())];
        Assert.Equal([-1.5f, 0f, 0f, 1.5f, 3f, 0f], chest);
        Assert.Equal(JsonValueKind.Null, boxes.GetProperty("Nothing").ValueKind);
        // A short benchmark: frames drawn for a second, reported.
        Assert.False(string.IsNullOrWhiteSpace(editor.Send("bench 1").GetProperty("bench").GetString()));
        editor.Send("camera 32 32 30 35 120");
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

    // A dungeon of the game's own rooms generated into the area: each room has a model in the made-up game, so they are
    // drawn solid (as the locations' parts are), 5000 m up where the dungeon goes.
    [Fact]
    public void TheGamesRoomsAreDrawnWithTheirModels()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the made-up game is found in the home's Steam library only on Linux");
        Directory.CreateDirectory(Dir);
        BackToStart(editor);
        OpenArea(editor, 3);
        WaitForGameLook(editor);
        editor.Send("camera 0 0 0 60 120");
        editor.Send("click Dungeon");
        // No dungeon in the area (the world copy is fresh, and leaving drops what the other test left): Generate is open,
        // the game's rooms chosen. Place in the world waits for the dungeon to be made (the button is off until then).
        Assert.StartsWith("@@ ok ", editor.Answer("choose The game's own rooms"));
        string placed = "";
        for (int i = 0; i < 600 && !placed.StartsWith("@@ ok ", StringComparison.Ordinal); i++)
        {
            Thread.Sleep(100);
            placed = editor.Answer("click Place in the world");
        }
        Assert.StartsWith("@@ ok ", placed);
        editor.Send("camera 0 0 0 45 80 5000");
        AssertDrawn(Picture("rooms"));
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        editor.Send("key Escape");
        BackToStart(editor);
    }

    /// <summary>
    /// The copy of the test world opened, and its area at zone 0, 0 this many zones across in the 3D view.
    /// </summary>
    internal static void OpenArea(GameLookEditorProcess editor, int size)
    {
        editor.Send($"world {editor.World}");
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
        string path = Path.Combine(Dir, name + ".png");
        JsonElement s = editor.Send($"picture {path}");
        Assert.Equal("editor", s.GetProperty("page").GetString());
        return PictureLook.Of(path).Dark;
    }

    /// <summary>
    /// Fails when most of the picture is black (nothing drawn: the sky alone fills what the ground does not).
    /// </summary>
    private static void AssertDrawn(double dark) => Assert.True(dark < 0.5, $"{dark:P0} of the picture is black");
}
