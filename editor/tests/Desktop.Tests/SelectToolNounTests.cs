using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a linked group is called in messages: the catalogue's "group" when the link gives no name, else the name it gives.
/// </summary>
public class SelectToolNounTests
{
    [Fact]
    public void AGroupWithTheDefaultNameIsCalledGroupFromTheCatalogue()
    {
        Assert.Equal(Said.Of("select.groupNoun"), PrivateMember.Call<Said>(typeof(SelectTool), "Noun", null, LinkRule.DefaultName));
    }

    [Fact]
    public void AGroupWithAGivenNameIsCalledByThatNameInBothLanguages()
    {
        Assert.Equal(Said.Raw("vehicle"), PrivateMember.Call<Said>(typeof(SelectTool), "Noun", null, "vehicle"));
    }
}
