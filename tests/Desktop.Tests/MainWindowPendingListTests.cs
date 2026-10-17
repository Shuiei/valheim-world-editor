using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The list of what a save will change, shown in the save bar: the zones first, then the deleted, added and reset counts,
/// each only when there are some.
/// </summary>
public class MainWindowPendingListTests
{
    [Fact]
    public void NothingPendingGivesNoText()
    {
        Assert.Equal("", MainWindow.PendingList("", 0, 0, 0));
    }

    [Fact]
    public void ZonesAloneAreTheWholeList()
    {
        Assert.Equal("2 zones", MainWindow.PendingList("2 zones", 0, 0, 0));
    }

    [Fact]
    public void DeletedAndResetCountsFollowTheZonesInOrderAndAddedIsLeftOutWhenZero()
    {
        string expected = $"zones, {Lang.T("save.count.deleted", 2)}, {Lang.T("save.count.resets", 1)}";
        Assert.Equal(expected, MainWindow.PendingList("zones", 2, 0, 1));
    }

    [Fact]
    public void AddedCountIsListedWhenThereAreAddedObjects()
    {
        Assert.Equal($"z, {Lang.T("save.count.added", 3)}", MainWindow.PendingList("z", 0, 3, 0));
    }
}
