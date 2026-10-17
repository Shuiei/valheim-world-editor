using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The modifiers of placed objects: a placed object of a prefab with modifiers brings them to the zones they reach, and
/// an object with none brings nothing.
/// </summary>
public class TerrainModifiersPlacementTests
{
    [Fact]
    public void APlacedObjectWithModifiersBringsThemToItsZone()
    {
        var m = new TerrainModifiers([new PlacedObject(StableHash.Of("raise"), 0, new Vector3(0, 0, 0), Vector3.Zero, 0)]);

        Assert.True(m.Count > 0);
        Assert.NotEmpty(m.InZone(0, 0));
    }

    [Fact]
    public void APlacedObjectWithoutModifiersBringsNone()
    {
        var m = new TerrainModifiers([new PlacedObject(StableHash.Of("NoSuchPrefab"), 0, new Vector3(0, 0, 0), Vector3.Zero, 0)]);

        Assert.Equal(0, m.Count);
        Assert.Empty(m.InZone(0, 0));
    }
}
