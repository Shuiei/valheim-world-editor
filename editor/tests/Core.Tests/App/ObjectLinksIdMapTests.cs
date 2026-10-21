using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The id map handed to the extension rewrites: an old id that was given a new one is found with its new id, and an id
/// that was not renumbered is not found.
/// </summary>
public class ObjectLinksIdMapTests
{
    /// <summary>An id map over these old-to-new ids, as the link remap hands it to the extensions.</summary>
    private static ValheimWorldEditor.Sdk.IIdMap MapOf(Dictionary<int, int> ids)
    {
        object map = PrivateCall.Create(PrivateMember.Nested(typeof(ObjectLinks), "IdMap"), [typeof(Dictionary<int, int>)], ids);
        return (ValheimWorldEditor.Sdk.IIdMap)map;
    }

    [Fact]
    public void AnIdThatWasGivenANewOneIsFoundWithIt()
    {
        var map = MapOf(new Dictionary<int, int> { [10] = 77 });
        Assert.True(map.TryGetNew(10, out int newId));
        Assert.Equal(77, newId);
    }

    [Fact]
    public void AnIdThatWasNotRenumberedIsNotFound()
    {
        var map = MapOf(new Dictionary<int, int> { [10] = 77 });
        Assert.False(map.TryGetNew(11, out int newId));
        Assert.Equal(0, newId);
    }
}
