using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Where a member is drawn: its position and turn, from its parent's frame up to the root it is stored at.
/// </summary>
public sealed partial class LinkIndex
{
    /// <summary>
    /// Where the mod puts an object (position in metres, turn as Unity Euler degrees): a part at its offset in its
    /// parent's frame, the parent where its own parent puts it, and so on up to the root, which is where it is stored.
    /// An object whose parent, or any parent up its chain, is missing (or whose chain loops) stays where it is stored.
    /// Null for an object with no link.
    /// </summary>
    public (Vector3 Position, Vector3 Rotation)? Pose(int id)
    {
        if (Get(id) is not { } m)
        {
            return null;
        }
        // The top hanging on something missing (a swivel whose vehicle is gone): the mod never places the chain.
        if (Chain(m) is not { Count: > 1 } chain || chain[^1].Link.Holder != 0)
        {
            return (m.Position, m.Rotation);
        }
        var top = chain[^1];
        Vector3 p = top.Position;
        Quaternion q = BlueprintFormats.FromEuler(top.Rotation);
        for (int k = chain.Count - 2; k >= 0; k--)
        {
            var link = chain[k].Link;
            p += Vector3.Transform(link.Offset, q);
            q = Quaternion.Normalize(q * link.Turn);
        }
        return (p, BlueprintFormats.ToEuler(q));
    }
}
