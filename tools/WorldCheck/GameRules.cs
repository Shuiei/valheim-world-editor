using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;

/// <summary>The game's location rules and a seed's generator, for the checks against what the game made.</summary>
internal static class GameRules
{
    /// <summary>
    /// The game's location rules read from a Valheim folder (else the one the editor finds) and put in use; null, said,
    /// when they cannot be read.
    /// </summary>
    internal static SeedLocations.RuleSet? Use(string? valheim)
    {
        var bundles = GameBundles.ForGame(valheim ?? GameLook.FindValheim(AppSettings.Load().ValheimPath), null);
        if (bundles == null || GameLocations.Load(bundles, null) is not { } rules)
        {
            Console.Error.WriteLine("The game's location rules could not be read (Valheim not found?).");
            return null;
        }
        SeedLocations.Use(rules);
        return rules;
    }

    /// <summary>The generator of a new world of this seed name, as this game version makes it.</summary>
    internal static WorldGenerator Generator(string seedName) =>
        WorldGenerator.Create(new World { SeedName = seedName, Seed = WorldCreator.SeedOf(seedName), WorldGenVersion = WorldCreator.WorldGenVersion });
}
