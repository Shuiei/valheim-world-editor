using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>
    /// UpdatePlacementGhost: where the piece goes, pointed at hit, turned by rotation (and lifted, the
    /// editor's own Ctrl + wheel), then snapped (snap: the snap points' search is done). Returns the
    /// position and what it snapped to (null: nothing).
    /// </summary>
    public static (Vector3 Position, int? SnappedTo) Place(string prefab, Quaternion rotation, Hit hit, IReadOnlyList<Placed> pieces, float lift = 0, bool snap = true)
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
            // Out along the normal, then back until its nearest collider point touches the hit. Only the
            // colliders Unity's ClosestPoint works on: not concave meshes.
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
            pos = nearest is { } np ? point + (ghost - np) : point;
        }
        pos.Y += lift;
        return snap ? Snap(prefab, rotation, pos, pieces) : (pos, null);
    }

    /// <summary>
    /// FindClosestSnapPoints: the piece at pos, turned by rotation, moved so that the closest pair of its
    /// snap points and those of the pieces within 10 m meets, when they are within half a metre (and
    /// unless that puts it right where the same piece already stands). Returns where it goes and what it
    /// snapped to (null: nothing, it stays at pos).
    /// </summary>
    public static (Vector3 Position, int? SnappedTo) Snap(string prefab, Quaternion rotation, Vector3 pos, IReadOnlyList<Placed> pieces)
    {
        if (Get(prefab) is not { Snaps.Length: > 0 } data)
        {
            return (pos, null);
        }
        float bestD = SnapDistance;
        Vector3? move = null;
        int? to = null;
        // The piece's own snap points in the world, worked out once for every piece near.
        var own = new Vector3[data.Snaps.Length];
        for (int k = 0; k < own.Length; k++)
        {
            own[k] = pos + Vector3.Transform(data.Snaps[k], rotation);
        }
        foreach (var p in pieces)
        {
            if (Get(p.Prefab) is not { Snaps.Length: > 0 } other || Vector3.Distance(p.Position, pos) - other.Radius > SnapRadius)
            {
                continue;
            }
            foreach (var b in other.Snaps)
            {
                var bw = p.Position + Vector3.Transform(b, p.Rotation);
                foreach (var aw in own)
                {
                    float dist = Vector3.Distance(aw, bw);
                    if (dist <= bestD)
                    {
                        bestD = dist;
                        move = bw - aw;
                        to = p.Index;
                    }
                }
            }
        }
        if (move is not { } m)
        {
            return (pos, null);
        }
        var snapped = pos + m;
        // IsOverlappingOtherPiece: not right where the same piece stands (turned alike, unless it may overlap turned).
        bool overlaps = pieces.Any(p => p.Prefab == prefab && Vector3.Distance(p.Position, snapped) < 0.05f
            && (!data.RotatedOverlap || AngleDegrees(p.Rotation, rotation) <= 10f));
        return overlaps ? (pos, null) : (snapped, to);
    }
}
