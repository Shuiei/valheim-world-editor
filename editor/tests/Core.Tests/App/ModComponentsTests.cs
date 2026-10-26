using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's components read for mods' extensions (ModComponents): their fields handed over as the SDK says (numbers
/// as long or double, texts, lists, maps), none without a game or classes asked for, read from the game's bundles (the
/// made-up CatalogueGame) and kept until the classes asked for change.
/// </summary>
[Collection("DataDir")]
public sealed class ModComponentsTests : IDisposable
{
    /// <summary>A pretend home: the caches go to its data folder.</summary>
    private readonly GameHome _home = new("vwe-components-");

    /// <summary>Forgets what was read and drops the pretend home.</summary>
    public void Dispose()
    {
        ModComponents.Refresh(null, []);
        _home.Dispose();
    }

    [Fact]
    public void FieldsAreHandedOverAsTheSdkSays()
    {
        var parsed = ModComponents.Parse("""
            {"TerrainOp": [{"prefab": "road", "object": "op", "fields": {"m_on": 1, "m_radius": 2.5, "m_name": "x", "m_list": [1, 2.0], "m_settings": {"m_deep": 3}}}]}
            """);
        var c = Assert.Single(parsed["TerrainOp"]);
        Assert.Equal(("road", "op"), (c.Prefab, c.ObjectName));
        Assert.Equal(1L, c.Fields["m_on"]);
        Assert.Equal(2.5, c.Fields["m_radius"]);
        Assert.Equal("x", c.Fields["m_name"]);
        Assert.Equal([1L, 2.0], (IReadOnlyList<object?>)c.Fields["m_list"]!);
        Assert.Equal(3L, ((IReadOnlyDictionary<string, object?>)c.Fields["m_settings"]!)["m_deep"]);
    }

    [Fact]
    public void WithoutAGameOrClassesThereAreNone()
    {
        ModComponents.Refresh(null, ["TerrainOp"]);
        Assert.Empty(ModComponents.View.Components("TerrainOp"));
        using var game = CatalogueGame.Make();
        game.Save();
        ModComponents.Refresh(game.Bundles(), []);
        Assert.Empty(ModComponents.Of("TerrainOp"));
    }

    [Fact]
    public void TheClassesAskedForAreReadFromTheGameAndKept()
    {
        using var game = CatalogueGame.Make();
        game.Save();
        ModComponents.Refresh(game.Bundles(), ["TerrainOp"]);
        var ops = ModComponents.View.Components("TerrainOp");
        Assert.Equal(["paved_test", "raise_test"], ops.Select(o => o.Prefab).Order());
        Assert.Equal("raise_op", ops.Single(o => o.Prefab == "raise_test").ObjectName);
        var settings = (IReadOnlyDictionary<string, object?>)ops.Single(o => o.Prefab == "paved_test").Fields["m_settings"]!;
        Assert.Equal(2L, settings["m_paintType"]);
        Assert.False(ops[0].Fields.ContainsKey("m_GameObject"));
        Assert.Empty(ModComponents.Of("Ship"));
        string cache = GameData.CacheFile("mod-components");
        Assert.StartsWith("mod-components TerrainOp ", File.ReadLines(cache).First());
        // Asked again: from the cache, the same.
        ModComponents.Refresh(game.Bundles(), ["TerrainOp"]);
        Assert.Equal(2, ModComponents.Of("TerrainOp").Count);
    }
}
