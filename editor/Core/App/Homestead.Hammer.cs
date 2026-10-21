using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// What Homestead's hammer builds of a blueprint and what it leaves out, and a blueprint's cost without the latter.
/// </summary>
public static partial class Homestead
{
    /// <summary>
    /// Whether Homestead's hammer places a piece of a blueprint. Mirrors Homestead's own rule: the prefab has a WearNTear
    /// (it is in the support data), it is a build piece with a cost (a Piece with resources) and it is in a build tool's
    /// piece table (the pieces catalogue gives its tool). Anything else in a blueprint (a statue, a ship, a creature) the
    /// hammer skips without a word; only the editor places it, into a world or live. The T.W.I.G. (piece_TrainingDummy)
    /// is a hammer piece with a cost but no WearNTear: the hammer leaves it out too.
    /// </summary>
    /// <param name="prefab">The prefab name.</param>
    public static bool HammerPlaces(string prefab) =>
        Editing.Stability.Known(prefab) && PieceCost.Get(prefab) is { Resources.Length: > 0 } && PieceCatalog.Get(StableHash.Of(prefab))?.Tool != null;

    /// <summary>
    /// What a blueprint costs its builder in game: the pieces the hammer places (<see cref="HammerPlaces"/>). The rest,
    /// which only the editor can place, costs nothing and is not counted as without a recipe either.
    /// </summary>
    /// <param name="kinds">How many pieces of each prefab the blueprint has.</param>
    public static PieceCost.Total Cost(IEnumerable<KeyValuePair<string, int>> kinds) => PieceCost.Of(kinds.Where(k => HammerPlaces(k.Key)));

    /// <summary>
    /// The cost (<see cref="Cost(IEnumerable{KeyValuePair{string, int}})"/>) of a list of pieces by prefab name, one
    /// entry per piece.
    /// </summary>
    /// <param name="prefabs">The pieces' prefab names.</param>
    public static PieceCost.Total Cost(IEnumerable<string> prefabs) => PieceCost.Of(prefabs.Where(HammerPlaces));
}
