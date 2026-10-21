using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which blueprint ids are safe to read back: an id is valid only when the file-name cleaning leaves it as it is, so
/// it holds no folder part and no character that would change its file.
/// </summary>
public class BlueprintStoreIdValidityTests
{
    private static bool ValidId(string id) => PrivateMember.Call<bool>(typeof(BlueprintStore), "ValidId", null, id);

    [Theory]
    [InlineData("house")]
    [InlineData("my house-2.0_final")]
    public void AnIdTheCleaningKeepsIsValid(string id)
    {
        Assert.True(ValidId(id));
    }

    [Fact]
    public void AnEmptyIdIsNotValid()
    {
        Assert.False(ValidId(""));
    }

    [Fact]
    public void AnIdWithAFolderPartIsNotValid()
    {
        Assert.False(ValidId("a/b"));
    }

    [Fact]
    public void AnIdWithACharacterTheCleaningReplacesIsNotValid()
    {
        Assert.False(ValidId("a?b"));
    }

    [Fact]
    public void AnIdMadeOnlyOfDotsIsNotValid()
    {
        Assert.False(ValidId(".."));
    }

    [Fact]
    public void AnIdWithATrailingSpaceIsNotValid()
    {
        Assert.False(ValidId("name "));
    }

    [Fact]
    public void AnIdOf80CharactersIsValidAndOneOf81IsNot()
    {
        Assert.True(ValidId(new string('a', 80)));
        Assert.False(ValidId(new string('a', 81)));
    }
}
