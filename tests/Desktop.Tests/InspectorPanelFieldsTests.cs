using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The values an object's data shows in the Inspector: each int, by its key and its text.
/// </summary>
public sealed class InspectorPanelFieldsTests
{
    [Fact]
    public void AnIntIsListedWithItsKeyAndItsText()
    {
        int key = StableHash.Of("probe");
        var z = ZdoData.Parse(ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, 0x0B00, Vector3.Zero, Vector3.Zero, 0f, [(key, 5)]));
        Assert.Contains(InspectorPanel.FieldsOf(z), f => f.Section == "ints" && f.Key == key && f.Value == "5");
    }

    [Fact]
    public void AFieldCarriesTheNameOfItsKey()
    {
        int key = StableHash.Of("probe");
        var z = ZdoData.Parse(ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, 0x0B00, Vector3.Zero, Vector3.Zero, 0f, [(key, 5)]));
        var field = Assert.Single(InspectorPanel.FieldsOf(z), f => f.Key == key);
        Assert.Equal(ZdoKeys.NameOf(key), field.Name);
        Assert.Equal("5", field.Now);
    }
}
