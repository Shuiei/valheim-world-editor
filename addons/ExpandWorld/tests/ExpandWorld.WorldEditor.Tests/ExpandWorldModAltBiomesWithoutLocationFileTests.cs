using ValheimGen;
using WorldEditor.Tests;
using Xunit;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Data's alt biome file counts on its own: without a location file, and with Expand World Size changing
/// nothing, the rules still carry the game's own kinds with the file's alt biomes, so what each keeps out applies.
/// </summary>
public class ExpandWorldModAltBiomesWithoutLocationFileTests
{
    private static ExpandWorldMod.Entry E(params (string Key, object Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => (object?)f.Value));

    [Fact]
    public void TheGamesKindsCarryTheFilesAltBiomes()
    {
        Fixtures.UseLocations();
        var found = new ExpandWorldMod.Found("", "test", false, true, false, null, null, null, null);
        var data = new ExpandWorldMod.DataFiles(new Dictionary<string, string>(), null, null, null, null, null, [E(("name", "DarkMeadows"), ("blockLocationNames", new List<object?> { "StartTemple" }))], []);
        var rules = ExpandWorldMod.Rules(new Dictionary<string, string>(), found, data);
        Assert.NotNull(rules.Locations);
        Assert.Equal(SeedLocations.Current.Locations.Count, rules.Locations.Locations.Count);
        var alt = Assert.Single(rules.Locations.AltBiomes);
        Assert.Equal("DarkMeadows", alt.Name);
        Assert.Equal(["StartTemple"], alt.BlockLocationNames);
        // The switch on without a file (the mod then writes the game's own): the game's kinds and alt biomes stay as they are.
        var none = ExpandWorldMod.Rules(new Dictionary<string, string>(), found, data with { AltBiomes = [] });
        Assert.Null(none.Locations);
    }
}
