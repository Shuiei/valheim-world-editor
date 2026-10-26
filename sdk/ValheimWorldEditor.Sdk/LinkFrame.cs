using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// A place and turn in another object's frame (see <see cref="ILinkPlacer"/>): metres and a turn, x right, y up, z
/// forward, as the game's local space.
/// </summary>
public sealed class LinkFrame
{
    /// <summary>A place and turn.</summary>
    /// <param name="offset">The place, in metres, in the other object's frame.</param>
    /// <param name="turn">The turn, in the other object's frame.</param>
    public LinkFrame(Vector3 offset, Quaternion turn)
    {
        Offset = offset;
        Turn = turn;
    }

    /// <summary>The place, in metres, in the other object's frame.</summary>
    public Vector3 Offset { get; }

    /// <summary>The turn, in the other object's frame.</summary>
    public Quaternion Turn { get; }
}
