using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Started with a world folder: its map opens.
/// </summary>
[Collection("Visual folder")]
[Trait("Category", "Visual")]
public sealed class FolderVisualTests(FolderEditorProcess editor)
{
    [Fact]
    public void AWorldFolderOnTheCommandLineOpensItsMap()
    {
        Assert.SkipUnless(editor.Available, editor.Why ?? "");
        DirectVisualTests.WaitForPage(editor, "map");
    }
}
