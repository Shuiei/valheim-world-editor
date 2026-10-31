using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>
    /// UpdatePlacementGhost: where the piece goes, pointed at hit, turned by rotation (and lifted, the
    /// editor's own Ctrl + wheel), then snapped (snap: the snap points' search is done). Returns the
    /// position and what it snapped to (null: nothing).
    /// </summary>
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="hit">What the build ray hits.</param>
    /// <param name="pieces">The pieces already there.</param>
    /// <param name="lift">The editor's lift, in metres.</param>
    /// <param name="snap">Whether it snaps.</param>
    /// <param name="own">Manual snapping: only this snap point of the piece (see <see cref="Snap"/>); null, the game's way.</param>
    public static (Vector3 Position, int? SnappedTo) Place(string prefab, Quaternion rotation, Hit hit, IReadOnlyList<Placed> pieces, float lift = 0, bool snap = true, int? own = null)
    {
        var data = Get(prefab);
        Vector3 point = hit.Point, normal = hit.Normal;
        Vector3 pos;
        if (data == null || (data.Ground || data.ClipGround) && hit.Piece == null || data.ClipEverything)
        {
            pos = point;
        }
        else
        {
            pos = OffHit(data, point, normal, rotation);
        }
        pos.Y += lift;
        return snap ? Snap(prefab, rotation, pos, pieces, own) : (pos, null);
    }

    /// <summary>
    /// Out along the normal, then back until its nearest collider point touches the hit. Only the
    /// colliders Unity's ClosestPoint works on: not concave meshes.
    /// </summary>
    internal static Vector3 OffHit(Data data, Vector3 point, Vector3 normal, Quaternion rotation)
    {
        var ghost = point + normal * 50f;
        Vector3? nearest = null;
        float nd = float.MaxValue;
        var lp = Vector3.Transform(point - ghost, Quaternion.Inverse(rotation));
        foreach (var shape in data.Colliders)
        {
            if (shape is Mesh { Convex: false })
            {
                continue;
            }
            var c = ghost + Vector3.Transform(Closest(shape, lp), rotation);
            float dist = Vector3.Distance(c, point);
            if (dist < nd)
            {
                nd = dist;
                nearest = c;
            }
        }
        return nearest is { } np ? point + (ghost - np) : point;
    }
}
