using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The stand-in game's players drawn in the real editor's 3D view (their posts, light blue), once an area of the live
/// world is open.
/// </summary>
[Collection("Visual live")]
[Trait("Category", "Visual")]
public sealed class LivePlayersVisualTests(LiveEditorProcess editor)
{
    [Fact]
    public void TheGamesPlayersStandInTheView()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        DirectVisualTests.WaitForPage(editor, "map");
        string dir = Path.Combine(Path.GetTempPath(), "vwe-visual-players-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal("editor", editor.Send("area 0 0 2").GetProperty("page").GetString());
            // The players are asked for as the area opens (the game answers at once).
            editor.Send("wait 500");
            string path = Path.Combine(dir, "players.png");
            editor.Send($"picture {path}");
            Assert.True(File.Exists(path));
            Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
        }
        finally
        {
            // Back on the map, where the other tests of the live editor start.
            editor.Send("map");
            Directory.Delete(dir, true);
        }
    }
}
