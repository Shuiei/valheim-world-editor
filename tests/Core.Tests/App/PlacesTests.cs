using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Paths as typed (Places.ExpandHome): trimmed and unquoted, the home folder for a leading ~ alone or before a
/// separator, and another user's ~name left alone.
/// </summary>
public class PlacesTests
{
    [Fact]
    public void OnlyTheOwnHomeIsExpanded()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, Places.ExpandHome(" ~ "));
        Assert.Equal(home + "/worlds", Places.ExpandHome("\"~/worlds\""));
        Assert.Equal("~bob/worlds", Places.ExpandHome("~bob/worlds"));
        Assert.Equal("/srv/~/x", Places.ExpandHome("/srv/~/x"));
    }
}
