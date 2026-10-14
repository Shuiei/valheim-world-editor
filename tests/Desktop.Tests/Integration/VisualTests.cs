using System.Text.Json;
using SkiaSharp;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Pictures drawn by the real editor (OpenGL), each test on a fresh copy of the test world: the map, the 3D view, the
/// tools' drawing, the documentation's shots.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class VisualTests(EditorProcess editor) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-world-" + Guid.NewGuid().ToString("N")[..8]);

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

    /// <summary>
    /// A fresh copy of the test world, opened (its map shown, the whole world in view).
    /// </summary>
    private JsonElement Open()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        string world = Path.Combine(_dir, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
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
    /// How much a picture shows: average brightness, its spread, distinct colours (5 bits a channel) and share of
    /// near-black.
    /// </summary>
    private sealed record Look(double Mean, double Spread, int Colours, double Dark);

    /// <summary>
    /// How much a picture shows: average brightness, its spread, distinct colours, share of near-black.
    /// </summary>
    private Look Picture(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        editor.Send($"picture {path}");
        return PictureAt(path);
    }

    /// <summary>
    /// Measures a saved picture, every third pixel each way.
    /// </summary>
    private static Look PictureAt(string path)
    {
        using var bmp = SKBitmap.Decode(path);
        Assert.NotNull(bmp);
        var colours = new HashSet<uint>();
        double sum = 0, sq = 0;
        int dark = 0, n = 0;
        for (int y = 0; y < bmp.Height; y += 3)
        {
            for (int x = 0; x < bmp.Width; x += 3)
            {
                var c = bmp.GetPixel(x, y);
                double l = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
                sum += l;
                sq += l * l;
                dark += l < 12 ? 1 : 0;
                colours.Add((uint)(c.Red >> 3 << 10 | c.Green >> 3 << 5 | c.Blue >> 3));
                n++;
            }
        }
        double mean = sum / n;
        return new Look(mean, Math.Sqrt(sq / n - mean * mean), colours.Count, dark / (double)n);
    }

    /// <summary>
    /// The 3D view draws with the game's look (copied from Valheim), else in plain colours (no Valheim, as in CI).
    /// </summary>
    private bool GameLook => editor.Send("state").GetProperty("gameLook").GetBoolean();

    /// <summary>
    /// Fails when the picture is flat or has few colours. The 3D view in plain colours (flat-shaded ground, one colour
    /// a kind of object) has far fewer than the map or the game's textures, but is still no flat fill.
    /// </summary>
    private void ShowsSomething(Look l)
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
    private Look Shot(string name)
    {
        string path = Path.Combine(_dir, name + ".png");
        editor.Send($"shot {path}");
        Assert.True(File.Exists(path));
        return PictureAt(path);
    }

    [Fact]
    public void AWindowShotHasThePanelsAndTheView()
    {
        Open();
        var map = Shot("shot-map");
        ShowsSomething(map);
        editor.Send("area 0 0 2");
        var area = Shot("shot-area");
        ShowsSomething(area);
        // The view is in it (daylight, not the empty dark the window alone gives).
        Assert.True(area.Dark < 0.3, $"{area.Dark:P0} of the window shot is black");
    }

    [Fact]
    public void TheDocumentationsCommandsPlaceTheCameraAndUseTheControls()
    {
        Open();
        editor.Send("area 0 0 2");
        var s = editor.Send("camera 10 -5 45 40 60");
        Assert.Equal(45 * Math.PI / 180, s.GetProperty("yaw").GetDouble(), 3);
        Assert.Equal(60, s.GetProperty("distance").GetDouble(), 3);
        // A tool's panel: its list entries and buttons by their words.
        editor.Send("key D1");
        editor.Send("choose Square");
        editor.Send("click History");
        editor.Send("click History");
        Assert.Equal("editor", editor.Send("state").GetProperty("page").GetString());
        editor.Send("choose Circle");
    }

    [Fact]
    public void TheEditorStillDrawsItsModelsAfterAVisitToTheMap()
    {
        Open();
        editor.Send("area 0 0 2");
        editor.Send("camera 40 20 30 50 70");
        var before = Picture("models-before");
        Assert.Equal("map", editor.Send("map").GetProperty("page").GetString());
        editor.Send("area 0 0 2");
        editor.Send("camera 40 20 30 50 70");
        var after = Picture("models-after");
        // The models came back untextured once (the textures being read were never read again in
        // the view's new OpenGL context): trees as flat white leaf cards. Same look as before.
        Assert.True(Math.Abs(after.Mean - before.Mean) < 8, $"brightness {before.Mean:0} before, {after.Mean:0} after");
        Assert.True(Math.Abs(after.Spread - before.Spread) < 8, $"spread {before.Spread:0} before, {after.Spread:0} after");
        Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
    }

    // Areas switched while their models are still being read: a model read for the area left was put
    // into the new one, with that area's object numbers (out of range: the app closed).
    [Fact]
    public void SwitchingAreasWhileModelsLoadKeepsDrawing()
    {
        Open();
        for (int n = 0; n < 4; n++)
        {
            editor.Send("area 0 0 3");
            editor.Send("area 0 0 1");
        }
        editor.Send("camera 0 0 30 50 70");
        var picture = Picture("switched");
        Assert.True(picture.Spread > 1, "the view is drawn");
        var state = editor.Send("state");
        Assert.Equal("editor", state.GetProperty("page").GetString());
        Assert.Equal(0, state.GetProperty("glErrors").GetInt32());
    }

    [Fact]
    public void TheMapDrawsTheWorld()
    {
        Assert.Equal("map", Open().GetProperty("page").GetString());
        var map = Picture("map");
        ShowsSomething(map);
        Assert.True(editor.Send("state").GetProperty("frames").GetInt32() > 10);
    }

    [Fact]
    public void TheEditorDrawsTheArea()
    {
        Open();
        var s = editor.Send("area 0 0 3");
        Assert.Equal("editor", s.GetProperty("page").GetString());
        Assert.True(s.GetProperty("objects").GetInt32() > 10);
        var area = Picture("area");
        ShowsSomething(area);
        // Daylight ground and water, not the dark of space.
        Assert.True(area.Dark < 0.2, $"{area.Dark:P0} of the picture is black");
    }

    [Fact]
    public void TheMapStillDrawsAfterAVisitToTheEditor()
    {
        Open();
        var before = Picture("map-before");
        editor.Send("area 0 0 3");
        Picture("area");
        Assert.Equal("map", editor.Send("map").GetProperty("page").GetString());
        var after = Picture("map-after");
        // The map came back black once (textures of the 3D view left bound): same look as before.
        ShowsSomething(after);
        Assert.True(Math.Abs(after.Mean - before.Mean) < 15, $"brightness {before.Mean:0} before, {after.Mean:0} after");
        Assert.True(Math.Abs(after.Dark - before.Dark) < 0.15, $"black {before.Dark:P0} before, {after.Dark:P0} after");
    }

    /// <summary>
    /// Every picture drawn without an OpenGL error.
    /// </summary>
    private void Clean() => Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());

    /// <summary>
    /// Sends the commands in turn; returns the last answer.
    /// </summary>
    private JsonElement Do(params string[] commands)
    {
        JsonElement last = default;
        foreach (string c in commands)
        {
            last = editor.Send(c);
        }
        return last;
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
            s = editor.Send("wait 100");
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
        Assert.Equal("Paste", editor.Send("state").GetProperty("mode").GetString());
        ShowsSomething(Picture("paste"));
        var s = Do("mouse down 10 10", "mouse up 10 10");
        Assert.True(s.GetProperty("added").GetInt32() > 0, s.ToString());
        Clean();
    }

    [Fact]
    public void TheMouseTurnsZoomsAndSlidesTheView()
    {
        Open();
        var s0 = Do("area 0 0 3", "key Escape");
        var s1 = Do("mouse down 0 0 right", "mouse move 20 0 right", "mouse up 20 0 right");
        Assert.NotEqual(s0.GetProperty("yaw").GetSingle(), s1.GetProperty("yaw").GetSingle());
        var s2 = Do("wheel 0 0 3");
        Assert.True(s2.GetProperty("distance").GetSingle() < s1.GetProperty("distance").GetSingle(), $"{s1} → {s2}");
        var s3 = Do("mouse down 0 0 middle", "mouse move 15 15 middle", "mouse up 15 15 middle");
        Assert.NotEqual(s2.GetProperty("targetX").GetSingle(), s3.GetProperty("targetX").GetSingle());
        ShowsSomething(Picture("moved-view"));
        Clean();
    }

    [Fact]
    public void TheMapShowsBuildingsCloseUpSearchPinsAndZoneMatches()
    {
        Open();
        var s = Do("mapview 0 0 0.5");
        Assert.Equal(0.5, s.GetProperty("mapScale").GetSingle(), 3);
        // Close up: the 1 m detail (with the edits) and the buildings' outlines. Mostly one biome: fewer
        // colours than the whole map.
        var close = Picture("map-close");
        Assert.True(editor.Send("state").GetProperty("detail").GetBoolean(), "the close-up detail is not shown");
        Assert.True(close.Spread > 12 && close.Colours > 150, $"{close}");
        // The pins and the zone matches, from 6 m a pixel: no close-up is built from there (under coverage, on
        // main, the largest close-up took minutes), and the buildings still draw up to 10 m a pixel.
        s = Do("search Beech", "mapview 0 0 6");
        Assert.True(s.GetProperty("pins").GetInt32() > 0, s.ToString());
        s = Do("zones");
        Assert.True(s.GetProperty("matches").GetInt32() > 0, s.ToString());
        ShowsSomething(Picture("map-marks"));
        Clean();
    }

    // An object found by the map's search, opened with Edit in 3D: still selected once the area is
    // drawn (the first frame dropped the selection made as the area opened).
    [Fact]
    public void AFoundObjectStaysSelectedInTheEditor()
    {
        Open();
        var s = Do("search Beech", "hit 0", "click Edit in 3D");
        for (int n = 0; n < 120 && s.GetProperty("page").GetString() != "editor"; n++)
        {
            s = editor.Send("wait 250");
        }
        Assert.Equal("editor", s.GetProperty("page").GetString());
        Picture("found");
        Assert.Equal(1, editor.Send("state").GetProperty("selected").GetInt32());
        Clean();
    }

    [Fact]
    public void AStrokeIsPendingAndTheAreaStillDraws()
    {
        Open();
        editor.Send("area 0 0 3");
        var s = editor.Send("stroke raise 0 0 30");
        Assert.Equal(1, s.GetProperty("pending").GetInt32());
        ShowsSomething(Picture("after-stroke"));
    }
    [Fact]
    public void TheLookSwitchesAndALowerResolutionStillDraw()
    {
        Open();
        editor.Send("area 0 0 3");
        try
        {
            var sharp = Picture("res-sharp");
            // Fewer pixels, stretched to the view: the same scene, a little softer.
            editor.Send("look res fast");
            var fast = Picture("res-fast");
            Assert.True(fast.Spread > sharp.Spread * 0.7, $"brightness spread {sharp.Spread:0.0} sharp, {fast.Spread:0.0} fast");
            ShowsSomething(fast);
            Assert.True(Math.Abs(fast.Mean - sharp.Mean) < 10, $"brightness {sharp.Mean:0} sharp, {fast.Mean:0} fast");
            Assert.True(fast.Dark < 0.2, $"{fast.Dark:P0} of the picture is black");
            editor.Send("look res balanced");
            ShowsSomething(Picture("res-balanced"));
            editor.Send("look res sharp");
            // See-through buildings, and the plain look with boxes and back.
            editor.Send("look seethrough on");
            ShowsSomething(Picture("see-through"));
            editor.Send("look seethrough off");
            // Without Valheim (CI) the view is in plain colours already: nothing to switch.
            if (!GameLook)
            {
                Clean();
                return;
            }
            int objects = editor.Send("look game off").GetProperty("objects").GetInt32();
            // Plain colours and boxes: far fewer colours than the game's textures, still not dark.
            var plain = Picture("plain");
            Assert.True(plain.Colours < sharp.Colours / 2, $"{plain.Colours} colours plain, {sharp.Colours} with the game look");
            Assert.True(plain.Colours > 10 && plain.Dark < 0.2, $"{plain.Colours} colours, {plain.Dark:P0} black");
            Assert.Equal(objects, editor.Send("look game on").GetProperty("objects").GetInt32());
            ShowsSomething(Picture("game-look"));
            Clean();
        }
        finally
        {
            // The editor is shared: back to the usual look for the other tests.
            editor.Send("look res sharp");
            editor.Send("look seethrough off");
            editor.Send("look game on");
        }
    }
}
