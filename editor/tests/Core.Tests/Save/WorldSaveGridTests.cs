using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The zone grid a save's chunk files lie on, as its world file names it (WorldSave.SavedGridWidth).</summary>
public class WorldSaveGridTests
{
    [Fact]
    public void TheSavedGridIsReadFromTheKeysWithinWhatAChunkIdCanName()
    {
        Assert.Equal(512, WorldSave.SavedGridWidth([], "w"));
        Assert.Equal(1024, WorldSave.SavedGridWidth(["ews_gridwidth 1024"], "w"));
        Assert.Equal(2048, WorldSave.SavedGridWidth(["ews_gridwidth 2048"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth 256"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth 4096"], "w"));
        Assert.Equal(512, WorldSave.SavedGridWidth(["ews_gridwidth wide"], "w"));
    }
}
