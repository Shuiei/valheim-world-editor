using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The catalogue's ordered dictionary: its entries come out in the order their keys were first set, and setting an
/// existing key again replaces its value in place.
/// </summary>
public class CatalogueJsonDictOrderTests
{
    [Fact]
    public void EntriesComeOutInTheOrderTheirKeysWereFirstSet()
    {
        var d = new CatalogueJson.Dict { ["b"] = 1L, ["a"] = 2L, ["c"] = null };
        string[] keys = ["b", "a", "c"];
        Assert.Equal(keys, d.Select(p => p.Key));
    }

    [Fact]
    public void SettingAKeyAgainReplacesItsValueAndKeepsItsPlace()
    {
        var d = new CatalogueJson.Dict { ["b"] = 1L, ["a"] = 2L };
        d["b"] = 3L;
        var entries = d.ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("b", entries[0].Key);
        Assert.Equal(3L, entries[0].Value);
        Assert.Equal("a", entries[1].Key);
    }
}
