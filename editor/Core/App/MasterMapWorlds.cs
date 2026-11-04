using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// Worlds drawn on a master map: the rules a world follows when its land and biomes are the map's (the generator then
/// keeps everything else the game's, see <see cref="GeneratorRules.Land"/>).
/// </summary>
public static class MasterMapWorlds
{
    /// <summary>The generator's id, as a drawn world's file will name it.</summary>
    public const string Id = "vwe.mastermap";

    /// <summary>The version of how a map becomes land (MasterMapLand): another version makes other land of the same map.</summary>
    public const string LandVersion = "1";

    /// <summary>The game's biome of each kind of the legend, in <see cref="MasterMap.Kind"/> order.</summary>
    private static readonly Heightmap.Biome[] Biomes = [.. MasterMap.Legend.Select(e => e.Biome)];

    /// <summary>
    /// The rules of the world a map draws: <paramref name="over"/> (the game's own by default) with the map's land. Lays
    /// the land out (a second or so).
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="over">The rules the rest follows; null for the game's.</param>
    public static WorldRules Rules(MasterMap map, WorldRules? over = null) =>
        (over ?? WorldRules.Vanilla) with { Generator = Generator(map, DrawnWorldGenerator.Settings(map)) };

    /// <summary>
    /// The generator's rules for a map: its land, its start, and the header a new world's file starts with (the map).
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="settings">Its settings (the world file's header, read or to write).</param>
    internal static GeneratorRules Generator(MasterMap map, ValheimWorldEditor.Sdk.GeneratorSettings settings) =>
        new(Id, Lang.T("masterMap.name"), LandVersion, settings.Description)
        {
            Land = new MasterMapLand(map.Cells, map.Heights, Biomes),
            Start = map.StartPoint,
            Header = new GeneratorHeader(WorldGenerators.Drawn, settings),
        };
}
