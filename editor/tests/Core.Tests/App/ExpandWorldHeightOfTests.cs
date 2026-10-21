using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A biome's and a territory's height transform are read the same way from the same six fields, each the mod's
/// default when absent.
/// </summary>
public class ExpandWorldHeightOfTests
{
    private static ExpandWorld.Entry E(params (string Key, object Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => (object?)f.Value));

    private static readonly (string Key, object Value)[] Fields =
    [
        ("altitudeMultiplier", "1.5"), ("altitudeDelta", "4"), ("waterDepthMultiplier", "2"), ("minimumAltitude", "-50"), ("maximumAltitude", "300"), ("excessFactor", "-0.25"),
    ];

    [Fact]
    public void ABiomeAndATerritoryReadTheSameTransform()
    {
        List<string> notes = [];
        var expected = new HeightTransform { Multiplier = 1.5f, Delta = 4f, DepthMultiplier = 2f, Minimum = -50f, Maximum = 300f, ExcessFactor = -0.25f };
        var table = ExpandWorld.BiomesOf([E([("biome", "Highlands"), ("terrain", "mountain"), .. Fields])], notes);
        Assert.NotNull(table);
        Assert.Equal(expected, table.Of((Heightmap.Biome)0x400)!.Height);
        var territory = Assert.Single(ExpandWorld.TerritoriesOf([E([("territory", "Redlands"), .. Fields])], notes));
        Assert.Equal(expected, territory.Height);
        // Nothing given: the mod's defaults, which change no height.
        var plain = Assert.Single(ExpandWorld.TerritoriesOf([E(("territory", "Flat"), ("noBuild", "true"))], notes));
        Assert.Equal(HeightTransform.None, plain.Height);
        Assert.True(plain.Height.IsIdentity);
        Assert.Empty(notes);
    }
}
