using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map drawn by the real editor (OpenGL) with its zone grid on, and a picture of it that cannot be written said
/// so as an error.
/// </summary>
[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class MapViewVisualTests(EditorProcess editor) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-map-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// A fresh copy of the test world, opened on its map.
    /// </summary>
    private void Open()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        string world = Path.Combine(_dir, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        editor.Send($"world {world}");
    }

    [Fact]
    public void TheZoneGridIsDrawnCloseUpAndAPictureNotWrittenIsAnError()
    {
        Open();
        try
        {
            editor.Send("check Zone grid|on");
            Assert.Equal(4, editor.Send("mapview 0 0 4").GetProperty("mapScale").GetSingle(), 3);
            string path = Path.Combine(_dir, "grid.png");
            editor.Send($"picture {path}");
            Assert.True(new FileInfo(path).Length > 0);
            // A folder that is not there: the editor's answer is an error, and it goes on.
            var e = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => editor.Send($"picture {Path.Combine(_dir, "no such folder", "grid.png")}"));
            Assert.Contains("@@ error", e.Message);
            Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        }
        finally
        {
            editor.Send("check Zone grid|off");
        }
    }
}
