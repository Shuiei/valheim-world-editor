using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Where the ground is not drawn: the surface inside an outline is left out, as over the mouth of a tunnel or a pit
/// the mod digs while the game runs (the editor's ground is a height map: it has no holes of its own).
/// </summary>
public sealed class GroundCut
{
    /// <summary>A cut.</summary>
    /// <param name="outline">
    /// Its outline on the ground, in metres: x and z in the object's frame (y is ignored; the object's place, turn
    /// and size are applied), or in the world's (see <see cref="WorldSpace"/>). At least three points, in any
    /// direction round; a self-crossing outline cuts as the even-odd rule says.
    /// </param>
    public GroundCut(Vector2[] outline)
    {
        Outline = outline;
    }

    /// <summary>Its outline on the ground (x, z), in metres.</summary>
    public Vector2[] Outline { get; }

    /// <summary>Its outline is world positions, not in the object's frame.</summary>
    public bool WorldSpace { get; set; }
}
