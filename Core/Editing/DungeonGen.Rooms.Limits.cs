namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>
    /// How many rooms to join: two and six more for each step of the size, within the kind's fewest and most when Expand
    /// World Data's dungeon file gives them.
    /// </summary>
    /// <param name="s">The settings (the size).</param>
    /// <param name="kind">The kind of dungeon.</param>
    private static int RoomTarget(Settings s, Dungeons.Kind kind)
    {
        int target = 2 + 6 * Math.Clamp(s.Size, 1, 4);
        return kind.Rooms is var (min, max) ? Math.Clamp(target, Math.Min(min, max), Math.Max(min, max)) : target;
    }

    /// <summary>
    /// A room to join to an open end of this type: of the rooms whose openings fit and the kind's limits allow, those
    /// still needed first, weighted as the game weighs its rooms (Room.m_weight); null when none fits.
    /// </summary>
    /// <param name="normal">The kind's ordinary rooms.</param>
    /// <param name="type">The open end's type.</param>
    /// <param name="kind">The kind of dungeon.</param>
    /// <param name="counts">How many of each game room are placed.</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers.</param>
    private static Dungeons.Room? PickRoom(List<Dungeons.Room> normal, string type, Dungeons.Kind kind, Dictionary<string, int> counts, Random rnd)
    {
        var fits = Allowed([.. normal.Where(r => Dungeons.Matching(r, type).Any())], kind, counts);
        if (fits.Count == 0)
        {
            return null;
        }
        var needed = Needed(fits, kind, counts);
#pragma warning disable S2245 // A dungeon must come out the same from its seed; nothing here is a secret.
        return Weighted.Pick(needed.Count > 0 ? needed : fits, r => r.Weight, rnd.NextDouble());
#pragma warning restore S2245
    }

    /// <summary>Whether to join more rooms: fewer than the target, or a room the kind requires still missing.</summary>
    private static bool Wanting(int placed, int target, Dungeons.Kind kind, Dictionary<string, int> counts) => placed < target || Missing(kind, counts);

    /// <summary>
    /// The rooms that may still be joined: those the kind's limits do not hold back (a room placed its most times is
    /// left out, as the mod takes it out of the rooms to pick from).
    /// </summary>
    /// <param name="fits">The rooms whose openings fit.</param>
    /// <param name="kind">The kind of dungeon.</param>
    /// <param name="counts">How many of each game room are placed.</param>
    private static List<Dungeons.Room> Allowed(List<Dungeons.Room> fits, Dungeons.Kind kind, Dictionary<string, int> counts) =>
        [.. fits.Where(r => !kind.Limits.Any(l => l.Room == r.Game && l.Max > 0 && counts.GetValueOrDefault(r.Game) >= l.Max))];

    /// <summary>
    /// Of the rooms that may be joined, those the dungeon still needs (its required rooms not placed yet, rooms short of
    /// their required count); none when it needs none of them.
    /// </summary>
    /// <param name="allowed">The rooms that may be joined.</param>
    /// <param name="kind">The kind of dungeon.</param>
    /// <param name="counts">How many of each game room are placed.</param>
    private static List<Dungeons.Room> Needed(List<Dungeons.Room> allowed, Dungeons.Kind kind, Dictionary<string, int> counts) =>
        [.. allowed.Where(r => Short(r.Game, kind, counts))];

    /// <summary>Whether the dungeon still needs more of a game room: required and not placed, or short of its required count.</summary>
    private static bool Short(string game, Dungeons.Kind kind, Dictionary<string, int> counts) =>
        (kind.Required.Contains(game) && counts.GetValueOrDefault(game) == 0)
        || kind.Limits.Any(l => l.Room == game && l.Required > counts.GetValueOrDefault(game));

    /// <summary>Whether the dungeon still needs a room the kind requires (by its required rooms or its limits).</summary>
    private static bool Missing(Dungeons.Kind kind, Dictionary<string, int> counts) =>
        kind.Required.Concat(kind.Limits.Select(l => l.Room)).Any(game => Short(game, kind, counts));
}
