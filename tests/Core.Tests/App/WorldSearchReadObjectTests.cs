using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading an object for a world search: an object with no bytes is passed over (WorldSearchContents.ReadObject).
/// </summary>
public class WorldSearchReadObjectTests
{
    [Fact]
    public void AnObjectWithNoBytesIsPassedOver()
    {
        Func<byte[]> empty = () => [];
        Assert.Null(PrivateMember.Call(typeof(WorldSearchContents), "ReadObject", null, empty));
    }
}
