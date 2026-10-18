using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map drawn by the real editor (OpenGL) with its zone grid on, and a picture of it that cannot be written said
/// so as an error.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class MapViewVisualTests(EditorProcess editor) : WorldsFolder("vwe-visual-map-")
{
    /// <summary>
    /// A fresh copy of the test world, opened on its map.
    /// </summary>
    private void Open()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        editor.Send($"world {TestWorldCopy.CopyInto(Dir)}");
    }

    [Fact]
    public void TheZoneGridIsDrawnCloseUpAndAPictureNotWrittenIsAnError()
    {
        Open();
        try
        {
            editor.Send("check Zone grid|on");
            // The grid draws under 8 m a pixel; from 6 m no close-up is built for the picture (under coverage, on
            // main, the largest close-up took minutes).
            Assert.Equal(7, editor.Send("mapview 0 0 7").GetProperty("mapScale").GetSingle(), 3);
            string path = Path.Combine(Dir, "grid.png");
            editor.Send($"picture {path}");
            Assert.True(new FileInfo(path).Length > 0);
            // A folder that is not there: the editor's answer is an error, and it goes on.
            var e = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => editor.Send($"picture {Path.Combine(Dir, "no such folder", "grid.png")}"));
            Assert.Contains("@@ error", e.Message);
            Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        }
        finally
        {
            editor.Send("check Zone grid|off");
        }
    }
}
