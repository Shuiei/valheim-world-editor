
namespace TerrainEditor.Editing;

/// <summary>
/// The dresser's larger set pieces, from what dungeons in other games and in old crypts are made of: a gallery over
/// the way in (Skyrim's balconies, a church's loft), ledges round a sunken floor (an arena pit), a vault within the
/// room (a chamber grave, Diablo's sub-rooms), clusters of things that belong together (a camp, a meal, a scribe's
/// desk: set dressing in readable groups), one hero piece a room would be remembered by, and corridors of several
/// kinds (a gate midway, a collapse, bones along the walls, a runner).
/// </summary>
internal sealed partial class DungeonDresser
{
    /// <summary>The hero pieces already standing in this dungeon: each stands at most once.</summary>
    private readonly HashSet<string> _heroes = [];

    /// <summary>Whether the material can carry a stone deck 4 m up on posts of its own: stone and grausten do; wood and the rest do not.</summary>
    private bool Decked => _mat.Name is "Stone" or "Grausten";

    /// <summary>The deck piece (2 × 2 m, 1 m thick) of the material: stone slabs for both.</summary>
    private const string Deck = "stone_floor_2x2";
}
