namespace ValheimGen;

/// <summary>
/// Where the game will lay out a seed's locations (start temple, traders, bosses, dungeons...) when its
/// world first loads, before the world exists: ZoneSystem.GenerateLocations replayed with the game's
/// rules on the editor's copy of its generator. The rules are read from the game's files (GameLocations,
/// then Use); until then, or when they cannot be read, there are none (Available) and nothing is placed.
/// </summary>
/// <remarks>
/// The game picks each try's zone from a biome map of the world (AltBiomeWorldData: 2048 x 2048 points
/// 12 m apart, each biome's points listed in the order its flood fill met them), then checks the spot
/// as ZoneSystem does. Each location kind starts Unity's Random afresh from the seed and its name, so
/// the result is the game's. The kinds an alt biome (Dark Meadows, Lox Plains...) adds or blocks are
/// checked against the alt biomes laid out as the game lays them out (AltBiomeLayout), the first time
/// such a kind is tried; the prioritized kinds (start, bosses, traders...) come first and never depend
/// on them.
/// </remarks>
public static partial class SeedLocations
{

    /// <summary>
    /// The rules in use and what follows from them, swapped whole (a search running keeps its own).
    /// </summary>
    /// <param name="rules">The rules.</param>
    private sealed class State(LocationRuleSet rules)
    {
        /// <summary>
        /// The rules as given.
        /// </summary>
        public LocationRuleSet Rules { get; } = rules;
        /// <summary>
        /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
        /// </summary>
        public List<LocationRule> Ordered { get; } = [.. rules.Locations.OrderByDescending(l => l.Prioritized != 0).Where(l => l.Enable != 0 && l.Quantity != 0)];
    }

    /// <summary>
    /// The rules in use; none until Use.
    /// </summary>
    private static volatile State _state = new(new LocationRuleSet());

    /// <summary>
    /// The rules read from the game (or the tests' copy, editor/tests/fixtures/locations.json).
    /// </summary>
    public static void Use(LocationRuleSet rules) => _state = new State(rules);

    /// <summary>
    /// Whether there are rules to place with.
    /// </summary>
    public static bool Available => _state.Rules.Locations.Count > 0;

    /// <summary>Whether there are rules to lay out a world with: its own (<see cref="WorldRules.Locations"/>), else the game's.</summary>
    /// <param name="gen">The world's generator.</param>
    public static bool AvailableFor(WorldGenerator gen) => gen.Rules.Locations is { } own ? own.Locations.Count > 0 : Available;

    /// <summary>The rules a world is laid out with: its own, else the ones in use.</summary>
    private static State StateFor(WorldGenerator gen) => gen.Rules.Locations is { } own ? new State(own) : _state;

    /// <summary>
    /// The kinds laid out first (start, bosses, traders...), as the tests' copy keeps them: what the
    /// editor shows, and all they need.
    /// </summary>
    public static LocationRuleSet Prioritized(LocationRuleSet rules) => new()
    {
        Locations = [.. rules.Locations.Where(l => l.Prioritized != 0 && l.Enable != 0 && l.Quantity != 0)],
    };

    /// <summary>
    /// The rules in use.
    /// </summary>
    public static LocationRuleSet Current => _state.Rules;

    /// <summary>
    /// One location instance the game will register: its kind and where.
    /// </summary>
    /// <param name="Rule">Its kind.</param>
    /// <param name="X">World x.</param>
    /// <param name="Y">The generator's ground height there, metres.</param>
    /// <param name="Z">World z.</param>
    public sealed record Placed(LocationRule Rule, float X, float Y, float Z);

    /// <summary>
    /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
    /// </summary>
    public static IReadOnlyList<LocationRule> Ordered => _state.Ordered;

    /// <summary>
    /// How far to lay out.
    /// </summary>
    public enum Which
    {
        /// <summary>
        /// The start temple only: the first kind laid out, near the middle (no biome map needed).
        /// </summary>
        Start,
        /// <summary>
        /// The kinds laid out first: start, bosses, traders, Hildir's dungeons... (about 0.3 s).
        /// </summary>
        Prioritized,
        /// <summary>
        /// Every kind (several seconds).
        /// </summary>
        All,
    }

    /// <summary>
    /// A world's alt biomes: its own (Expand World Data's location rules), else the game's.
    /// </summary>
    internal static IReadOnlyList<AltBiomeRule> AltBiomeRulesFor(WorldGenerator gen) => RulesFor(gen).AltBiomes;

    /// <summary>
    /// A world's location rules: its own (Expand World Data's location file, or the game's changed by Expand World
    /// Size), else the game's.
    /// </summary>
    internal static LocationRuleSet RulesFor(WorldGenerator gen) => StateFor(gen).Rules;

    /// <summary>
    /// A world's alt biome layout, made the first time it is asked for (on a biome point map of its own, on every core
    /// but one) and kept while the generator lives.
    /// </summary>
    /// <param name="gen">The world's generator.</param>
    /// <param name="cancel">Stops the layout (throws; nothing is kept then).</param>
    internal static AltBiomeLayout AltBiomesOf(WorldGenerator gen, CancellationToken cancel = default) =>
        AltBiomeLayout.Of(gen, AltBiomeRulesFor(gen), () => new BiomeMap(gen, parallel: true, cancel), cancel);

    /// <summary>
    /// The seed's location instances (a kind's instances are the same whichever is asked).
    /// </summary>
    /// <param name="gen">The seed's generator.</param>
    /// <param name="which">How far to lay out.</param>
    /// <param name="parallel">The biome map on every core but one.</param>
    /// <param name="cancel">Stops the layout (throws).</param>
    public static List<Placed> Place(WorldGenerator gen, Which which = Which.Prioritized, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).Place(which, cancel);

    /// <summary>
    /// The instances of these kinds (prefab names), the game's kinds before them laid out too (a few
    /// seconds when it goes past the prioritized ones).
    /// </summary>
    public static List<Placed> PlaceKinds(WorldGenerator gen, IReadOnlyCollection<string> prefabs, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).PlaceKinds(prefabs, cancel);
}
