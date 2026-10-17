using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ground discs of an uplift (No limit ground): each disc is a location proxy the game spawns as dev ground, and a
/// disc and an object centred on the same point take the same point of the block.
/// </summary>
[Collection("World files")]
public class UpliftDiscTests
{
    [Fact]
    public void ADiscIsALocationProxyTheGameSpawnsAsDevGround1()
    {
        var z = ZdoData.Parse(Uplift.DiscBytes(new Vector3(10, 55, -20)));
        Assert.Equal(WorldSave.LocationProxyPrefab, z.Prefab);
        Assert.Equal(55, z.Position.Y, 0.001f);
        Assert.Contains(z.IntList, i => i.Key == StableHash.Of("location") && i.Value == StableHash.Of("DevGround1"));
        Assert.Contains(z.IntList, i => i.Key == StableHash.Of("seed"));
        // Persistent (kept in the save), a solid object like the game's own proxies.
        Assert.Equal(0x100 | (2 << 10), z.BaseFlags & 0xFF00 & ~0x1000 & ~0x2000);
    }

    [Theory]
    [InlineData(0.5f, 0.5f)]
    [InlineData(-1.5f, 2.5f)]
    [InlineData(3.2f, -0.7f)]
    public void AnObjectAndADiscCentredOnItTakeTheSamePoint(float x, float z)
    {
        // At rounded halves to even (MathF.Round) and Disc rounded them up: an object at x.5 was taken for another
        // point than a disc centred on it.
        var block = new Uplift.Block([.. from zz in Enumerable.Range(-1, 3) from zx in Enumerable.Range(-1, 3) select (zx, zz)]);
        var at = new Vector3(x, 0, z);
        var middle = new List<int>();

        block.Disc(at, 0, middle.Add);

        Assert.Equal(block.At(at), Assert.Single(middle));
    }
}
