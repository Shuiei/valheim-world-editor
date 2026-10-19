using System.Text.Json;

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
/// the result is the game's, with one exception: alt biomes (Dark Meadows, Lox Plains...) are given to
/// biome areas in an order the game shuffles with an unseeded random, different at each load. The few
/// kinds an alt biome adds or blocks cannot be known (Uncertain), and are left out; the prioritized
/// kinds (start, bosses, traders...) come first and never depend on them.
/// </remarks>
public static partial class SeedLocations
{
    /// <summary>
    /// How ToJson writes each line: nulls (a game kind's altBiome) left out.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// The rules in use and what follows from them, swapped whole (a search running keeps its own).
    /// </summary>
    /// <param name="rules">The rules.</param>
    private sealed class State(RuleSet rules)
    {
        /// <summary>
        /// The rules as given.
        /// </summary>
        public RuleSet Rules { get; } = rules;
        /// <summary>
        /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
        /// </summary>
        public List<Rule> Ordered { get; } = [.. rules.Locations.OrderByDescending(l => l.Prioritized != 0).Where(l => l.Enable != 0 && l.Quantity != 0)];
        /// <summary>
        /// Names of the kinds some alt biome blocks.
        /// </summary>
        public HashSet<string> Blocked { get; } = [.. rules.AltBiomes.SelectMany(a => a.BlockLocationNames)];
    }

    /// <summary>
    /// The rules in use; none until Use.
    /// </summary>
    private static volatile State _state = new(new RuleSet());

    /// <summary>
    /// The rules read from the game (or the tests' copy, tests/fixtures/locations.json).
    /// </summary>
    public static void Use(RuleSet rules) => _state = new State(rules);

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
    public static RuleSet Prioritized(RuleSet rules) => new()
    {
        Locations = [.. rules.Locations.Where(l => l.Prioritized != 0 && l.Enable != 0 && l.Quantity != 0)],
    };

    /// <summary>
    /// The rules in use.
    /// </summary>
    public static RuleSet Current => _state.Rules;

    /// <summary>
    /// One location instance the game will register: its kind and where.
    /// </summary>
    /// <param name="Rule">Its kind.</param>
    /// <param name="X">World x.</param>
    /// <param name="Y">The generator's ground height there, metres.</param>
    /// <param name="Z">World z.</param>
    public sealed record Placed(Rule Rule, float X, float Y, float Z);

    /// <summary>
    /// The kinds whose spots depend on the alt biomes (added by one, or blocked by one somewhere).
    /// </summary>
    public static bool Uncertain(Rule r) => Uncertain(_state, r);

    /// <summary>
    /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
    /// </summary>
    public static IReadOnlyList<Rule> Ordered => _state.Ordered;

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
    /// The seed's location instances, the uncertain kinds left out (a kind's instances are the same
    /// whichever is asked).
    /// </summary>
    /// <param name="gen">The seed's generator.</param>
    /// <param name="which">How far to lay out.</param>
    /// <param name="parallel">The biome map on every core but one.</param>
    /// <param name="cancel">Stops the layout (throws).</param>
    public static List<Placed> Place(WorldGenerator gen, Which which = Which.Prioritized, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).Place(which, cancel);

    /// <summary>
    /// The instances of these kinds (prefab names), the game's kinds before them laid out too (a few
    /// seconds when it goes past the prioritized ones). A kind an alt biome blocks somewhere is given
    /// everywhere (close to the game: an alt biome covers a few areas only).
    /// </summary>
    public static List<Placed> PlaceKinds(WorldGenerator gen, IReadOnlyCollection<string> prefabs, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).PlaceKinds(prefabs, cancel);

    /// <summary>
    /// Whether a kind depends on the alt biomes under these rules.
    /// </summary>
    private static bool Uncertain(State state, Rule r) => r.AltBiome != null || state.Blocked.Contains(r.Name);
}
