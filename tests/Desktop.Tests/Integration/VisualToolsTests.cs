using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Pictures of the tools drawing in the real editor (OpenGL): a zone or box or path drawn, the place and brush previews,
/// a copied area while pasting, a stroke pending, and the look switching.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class VisualToolsTests : VisualEditorPictures
{
    /// <summary>
    /// Starts the tests on the shared editor process.
    /// </summary>
    /// <param name="editor">The editor process the tests drive.</param>
    public VisualToolsTests(EditorProcess editor) : base(editor)
    {
    }

    [Fact]
    public void DrawingAZoneSelectsAndShowsTheHandles()
    {
        Open();
        Do("area 0 0 3", "key E");
        Do("mouse down -40 -40", "mouse move 40 -40", "mouse move 40 40", "mouse move -40 40");
        // The zone being drawn.
        ShowsSomething(Picture("lasso"));
        var s = Do("mouse move -40 -39", "mouse up -40 -39");
        // The selection settles on the next frames (once in a while, under load, it was not there yet).
        for (int i = 0; i < 20 && s.GetProperty("selected").GetInt32() == 0; i++)
        {
            s = Editor.Send("wait 100");
        }
        Assert.True(s.GetProperty("selected").GetInt32() > 0, s.ToString());
        Assert.True(s.GetProperty("mode").GetString() == "Select", s.ToString());
        // The selection's boxes and the handles.
        ShowsSomething(Picture("selected"));
        Clean();
    }

    [Fact]
    public void PlacePreviewsAndPlacesTrees()
    {
        Open();
        Do("area 0 0 3", "key T", "mouse move 5 5");
        ShowsSomething(Picture("place-preview"));
        var s = Do("mouse down 5 5", "mouse up 5 5");
        Assert.True(s.GetProperty("added").GetInt32() > 0, s.ToString());
        ShowsSomething(Picture("placed"));
        Clean();
    }

    [Fact]
    public void TheBrushCircleFollowsTheMouseAndAStrokeChangesTheGround()
    {
        Open();
        Do("area 0 0 3", "key D1", "mouse move 0 0");
        ShowsSomething(Picture("brush"));
        // Held for a moment: the brush works frame by frame while the button is down.
        var s = Do("mouse down 0 0", "wait 150", "mouse move 2 0", "wait 150", "mouse move 4 0", "wait 150", "mouse up 4 0");
        Assert.Equal(1, s.GetProperty("pending").GetInt32());
        Clean();
    }

    [Fact]
    public void AnAreaBoxAPathAndTheTapeAreDrawn()
    {
        Open();
        Do("area 0 0 3", "key B", "mouse down -20 -20", "mouse move 0 0", "mouse move 20 20", "mouse up 20 20");
        ShowsSomething(Picture("area-box"));
        Do("key Escape", "key P", "mouse down -30 0", "mouse up -30 0", "mouse down 0 10", "mouse up 0 10", "mouse down 30 0", "mouse up 30 0");
        ShowsSomething(Picture("path"));
        Do("key Escape", "key M", "mouse down -20 5", "mouse up -20 5", "mouse down 25 -5", "mouse up 25 -5");
        ShowsSomething(Picture("tape"));
        Clean();
    }

    [Fact]
    public void ACopiedAreaIsShownWhilePasting()
    {
        Open();
        Do("area 0 0 3", "key B", "mouse down -30 -30", "mouse move 0 0", "mouse move 30 30", "mouse up 30 30", "key C ctrl", "key V ctrl", "mouse move 10 10");
        Assert.Equal("Paste", Editor.Send("state").GetProperty("mode").GetString());
        ShowsSomething(Picture("paste"));
        var s = Do("mouse down 10 10", "mouse up 10 10");
        Assert.True(s.GetProperty("added").GetInt32() > 0, s.ToString());
        Clean();
    }

    [Fact]
    public void AStrokeIsPendingAndTheAreaStillDraws()
    {
        Open();
        Editor.Send("area 0 0 3");
        var s = Editor.Send("stroke raise 0 0 30");
        Assert.Equal(1, s.GetProperty("pending").GetInt32());
        ShowsSomething(Picture("after-stroke"));
    }

    [Fact]
    public void TheLookSwitchesAndALowerResolutionStillDraw()
    {
        Open();
        Editor.Send("area 0 0 3");
        try
        {
            var sharp = Picture("res-sharp");
            // Fewer pixels, stretched to the view: the same scene, a little softer.
            Editor.Send("look res fast");
            var fast = Picture("res-fast");
            Assert.True(fast.Spread > sharp.Spread * 0.7, $"brightness spread {sharp.Spread:0.0} sharp, {fast.Spread:0.0} fast");
            ShowsSomething(fast);
            Assert.True(Math.Abs(fast.Mean - sharp.Mean) < 10, $"brightness {sharp.Mean:0} sharp, {fast.Mean:0} fast");
            Assert.True(fast.Dark < 0.2, $"{fast.Dark:P0} of the picture is black");
            Editor.Send("look res balanced");
            ShowsSomething(Picture("res-balanced"));
            Editor.Send("look res sharp");
            // See-through buildings, and the plain look with boxes and back.
            Editor.Send("look seethrough on");
            ShowsSomething(Picture("see-through"));
            Editor.Send("look seethrough off");
            // Without Valheim (CI) the view is in plain colours already: nothing to switch.
            if (!GameLook)
            {
                Clean();
                return;
            }
            int objects = Editor.Send("look game off").GetProperty("objects").GetInt32();
            // Plain colours and boxes: far fewer colours than the game's textures, still not dark.
            var plain = Picture("plain");
            Assert.True(plain.Colours < sharp.Colours / 2, $"{plain.Colours} colours plain, {sharp.Colours} with the game look");
            Assert.True(plain.Colours > 10 && plain.Dark < 0.2, $"{plain.Colours} colours, {plain.Dark:P0} black");
            Assert.Equal(objects, Editor.Send("look game on").GetProperty("objects").GetInt32());
            ShowsSomething(Picture("game-look"));
            Clean();
        }
        finally
        {
            // The editor is shared: back to the usual look for the other tests.
            Editor.Send("look res sharp");
            Editor.Send("look seethrough off");
            Editor.Send("look game on");
        }
    }
}
