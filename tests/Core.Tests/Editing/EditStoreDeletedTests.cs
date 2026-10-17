using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Marking saved objects deleted in the edit store: the mark is set and cleared by id.
/// </summary>
public class EditStoreDeletedTests
{
    [Fact]
    public void ASavedObjectMarkedDeletedIsInTheDeletedSet()
    {
        var edits = new EditStore(WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test"));

        edits.SetDeleted([5], true);

        Assert.Contains(5, edits.Deleted);
    }

    [Fact]
    public void ASavedObjectUnmarkedIsNoLongerDeleted()
    {
        var edits = new EditStore(WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test"));
        edits.SetDeleted([5], true);

        edits.SetDeleted([5], false);

        Assert.DoesNotContain(5, edits.Deleted);
    }
}
