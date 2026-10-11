using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Started with --live and --token: the game's world opens on the map.
/// </summary>
[Collection("Visual live")]
[Trait("Category", "Visual")]
public sealed class LiveVisualTests(LiveEditorProcess editor)
{
    [Fact]
    public void ALiveGameOnTheCommandLineOpensItsMap()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        DirectVisualTests.WaitForPage(editor, "map");
    }
}
