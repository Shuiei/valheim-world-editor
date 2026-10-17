using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A pasted copy's float values set from typed values (CopyView.SetFloat): a float is kept as its round-trip text and read
/// back, and none removes it.
/// </summary>
public class CopyViewWritingTests
{
    [Fact]
    public void AFloatIsKeptAsItsRoundTripText()
    {
        var data = new List<ObjectField>();
        var c = new CopyView("Cart", Vector3.Zero, Vector3.Zero, data);

        c.SetFloat("f", 1.5f);

        Assert.Equal("1.5", data.Single(f => f.Key == StableHash.Of("f")).Value);
        Assert.True(c.TryGetFloat("f", out float value) && value == 1.5f);
    }

    [Fact]
    public void ANoFloatRemovesTheValue()
    {
        var data = new List<ObjectField>();
        var c = new CopyView("Cart", Vector3.Zero, Vector3.Zero, data);
        c.SetFloat("f", 1.5f);

        c.SetFloat("f", null);

        Assert.Null(data.Single(f => f.Key == StableHash.Of("f")).Value);
        Assert.False(c.TryGetFloat("f", out _));
    }
}
