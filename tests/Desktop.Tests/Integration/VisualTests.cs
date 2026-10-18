using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Pictures drawn by the real editor (OpenGL), each test on a fresh copy of the test world: the map, the 3D view and
/// the documentation's shots.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class VisualTests : VisualEditorPictures
{
    /// <summary>
    /// Starts the tests on the shared editor process.
    /// </summary>
    /// <param name="editor">The editor process the tests drive.</param>
    public VisualTests(EditorProcess editor) : base(editor)
    {
    }

    [Fact]
    public void AWindowShotHasThePanelsAndTheView()
    {
        Open();
        var map = Shot("shot-map");
        ShowsSomething(map);
        Editor.Send("area 0 0 2");
        var area = Shot("shot-area");
        ShowsSomething(area);
        // The view is in it (daylight, not the empty dark the window alone gives).
        Assert.True(area.Dark < 0.3, $"{area.Dark:P0} of the window shot is black");
    }

    [Fact]
    public void TheDocumentationsCommandsPlaceTheCameraAndUseTheControls()
    {
        Open();
        Editor.Send("area 0 0 2");
        var s = Editor.Send("camera 10 -5 45 40 60");
        Assert.Equal(45 * Math.PI / 180, s.GetProperty("yaw").GetDouble(), 3);
        Assert.Equal(60, s.GetProperty("distance").GetDouble(), 3);
        // A tool's panel: its list entries and buttons by their words.
        Editor.Send("key D1");
        Editor.Send("choose Square");
        Editor.Send("click History");
        Editor.Send("click History");
        Assert.Equal("editor", Editor.Send("state").GetProperty("page").GetString());
        Editor.Send("choose Circle");
    }

    [Fact]
    public void TheEditorStillDrawsItsModelsAfterAVisitToTheMap()
    {
        Open();
        Editor.Send("area 0 0 2");
        Editor.Send("camera 40 20 30 50 70");
        var before = Picture("models-before");
        Assert.Equal("map", Editor.Send("map").GetProperty("page").GetString());
        Editor.Send("area 0 0 2");
        Editor.Send("camera 40 20 30 50 70");
        var after = Picture("models-after");
        // The models came back untextured once (the textures being read were never read again in
        // the view's new OpenGL context): trees as flat white leaf cards. Same look as before.
        Assert.True(Math.Abs(after.Mean - before.Mean) < 8, $"brightness {before.Mean:0} before, {after.Mean:0} after");
        Assert.True(Math.Abs(after.Spread - before.Spread) < 8, $"spread {before.Spread:0} before, {after.Spread:0} after");
        Assert.Equal(0, Editor.Send("state").GetProperty("glErrors").GetInt32());
    }

    // Areas switched while their models are still being read: a model read for the area left was put
    // into the new one, with that area's object numbers (out of range: the app closed).
    [Fact]
    public void SwitchingAreasWhileModelsLoadKeepsDrawing()
    {
        Open();
        for (int n = 0; n < 4; n++)
        {
            Editor.Send("area 0 0 3");
            Editor.Send("area 0 0 1");
        }
        Editor.Send("camera 0 0 30 50 70");
        var picture = Picture("switched");
        Assert.True(picture.Spread > 1, "the view is drawn");
        var state = Editor.Send("state");
        Assert.Equal("editor", state.GetProperty("page").GetString());
        Assert.Equal(0, state.GetProperty("glErrors").GetInt32());
    }

    [Fact]
    public void TheMapDrawsTheWorld()
    {
        Assert.Equal("map", Open().GetProperty("page").GetString());
        var map = Picture("map");
        ShowsSomething(map);
        Assert.True(Editor.Send("state").GetProperty("frames").GetInt32() > 10);
    }

    [Fact]
    public void TheEditorDrawsTheArea()
    {
        Open();
        var s = Editor.Send("area 0 0 3");
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
        Editor.Send("area 0 0 3");
        Picture("area");
        Assert.Equal("map", Editor.Send("map").GetProperty("page").GetString());
        var after = Picture("map-after");
        // The map came back black once (textures of the 3D view left bound): same look as before.
        ShowsSomething(after);
        Assert.True(Math.Abs(after.Mean - before.Mean) < 15, $"brightness {before.Mean:0} before, {after.Mean:0} after");
        Assert.True(Math.Abs(after.Dark - before.Dark) < 0.15, $"black {before.Dark:P0} before, {after.Dark:P0} after");
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
            s = Editor.Send("wait 250");
        }
        Assert.Equal("editor", s.GetProperty("page").GetString());
        Picture("found");
        Assert.Equal(1, Editor.Send("state").GetProperty("selected").GetInt32());
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
        Assert.True(Editor.Send("state").GetProperty("detail").GetBoolean(), "the close-up detail is not shown");
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
}
