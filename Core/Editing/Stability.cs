using System.Numerics;

namespace TerrainEditor.Editing;

/// <summary>
/// The game's structural support (WearNTear.UpdateSupport), worked out for a set of building pieces, so
/// the editor shows what would hold and what would fall in game. Each piece has its material's support:
/// full on the ground, else the best it gets from the pieces it rests on (their colliders that its own
/// boxes, grown by 0.3 m, touch), less the material's loss for the distance between their centres of
/// mass and the angle (horizontal loses more than vertical); two supports on opposite sides average.
/// Repeated until nothing changes; pieces below their material's minimum break, then the rest is worked
/// out again (the collapse spreading). The pieces' colliders, materials and centres are read from the
/// player's own Valheim (GameData; the tests use tests/fixtures/piece-support.json). Not counted: other
/// objects (rocks, trees) the game would also take as full support, and mesh colliders' exact shapes
/// (their boxes).
/// </summary>
public static partial class Stability
{
    /// <summary>A collider of a piece, in its own frame.</summary>
    /// <param name="C">Its middle.</param>
    /// <param name="H">Its half size.</param>
    /// <param name="Q">Its rotation.</param>
    /// <param name="Aabb">A non-box collider (its axis-aligned bounds count).</param>
    /// <param name="Counts">On a layer the game's support check looks at.</param>
    internal sealed record Box(Vector3 C, Vector3 H, Quaternion Q, bool Aabb, bool Counts);

    /// <summary>What support needs of a piece.</summary>
    /// <param name="Material">Its WearNTear material (0 wood, 1 stone, 2 iron...; see Stability.Material).</param>
    /// <param name="Supports">Other pieces can rest on it.</param>
    /// <param name="Com">Its centre of mass, in its own frame.</param>
    /// <param name="Boxes">Its colliders.</param>
    /// <param name="Free">
    /// The game never checks its support (WearNTear.m_noSupportWear off): it stays up, at its material's full
    /// support, wherever it is.
    /// </param>
    internal sealed record Data(int Material, bool Supports, Vector3 Com, Box[] Boxes, bool Free = false);

    /// <summary>None until Use: no world or Workshop opens before then (GameData).</summary>
    private static volatile Dictionary<string, Data> _pieces = [];

    /// <summary>The layers WearNTear's overlap test sees: Default, piece, terrain, static_solid, Default_small.</summary>
    private static readonly HashSet<int> SupportLayers = [0, 10, 11, 15, 20];
}
