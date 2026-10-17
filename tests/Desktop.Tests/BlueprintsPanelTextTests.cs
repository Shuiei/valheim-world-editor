using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The text a blueprint panel shows and reads from a blueprint file: the first names of a list with an ellipsis when
/// there are more, and the "#Tags:" line of a file.
/// </summary>
public class BlueprintsPanelTextTests
{
    [Fact]
    public void AListOfFiveNamesIsShownInFullWithoutAnEllipsis()
    {
        List<string> names = ["a", "b", "c", "d", "e"];

        Assert.Equal("a, b, c, d, e", PrivateMember.Call<string>(typeof(BlueprintsPanel), "FirstFew", null, names));
    }

    [Fact]
    public void AListOfSixNamesShowsTheFirstFiveAndAnEllipsis()
    {
        List<string> names = ["a", "b", "c", "d", "e", "f"];

        Assert.Equal("a, b, c, d, e…", PrivateMember.Call<string>(typeof(BlueprintsPanel), "FirstFew", null, names));
    }

    [Fact]
    public void TheTagsLineGivesWhatFollowsItsMarker()
    {
        const string text = "name\n#Tags: wood, stone\nbody";

        Assert.Equal(" wood, stone", PrivateMember.Call<string>(typeof(BlueprintsPanel), "TagsOf", null, text));
    }

    [Fact]
    public void TheTagsMarkerIsFoundWhateverItsCase()
    {
        Assert.Equal(" wood", PrivateMember.Call<string>(typeof(BlueprintsPanel), "TagsOf", null, "#tags: wood"));
    }

    [Fact]
    public void AFileWithoutATagsLineHasNoTags()
    {
        Assert.Null(PrivateMember.Call<string?>(typeof(BlueprintsPanel), "TagsOf", null, "name\nbody"));
    }
}
