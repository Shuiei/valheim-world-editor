using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
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
        if (NearestSnapMove(data, rotation, pos, pieces) is not { } nearest)
        {
            return (pos, null);
        }
        var snapped = pos + nearest.Move;
        // IsOverlappingOtherPiece: not right where the same piece stands (turned alike, unless it may overlap turned).
        bool overlaps = pieces.Any(p => p.Prefab == prefab && Vector3.Distance(p.Position, snapped) < 0.05f
            && (!data.RotatedOverlap || AngleDegrees(p.Rotation, rotation) <= 10f));
        return overlaps ? (pos, null) : (snapped, nearest.Index);
    }

    /// <summary>
    /// The closest pair of snap points of a piece at pos (turned by rotation) and of the pieces within SnapRadius,
    /// as the move that makes them meet and the index of the piece met; null when no pair is within SnapDistance.
    /// </summary>
    internal static (Vector3 Move, int Index)? NearestSnapMove(Data data, Quaternion rotation, Vector3 pos, IReadOnlyList<Placed> pieces)
    {
        float bestD = SnapDistance;
        (Vector3 Move, int Index)? best = null;
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
                        best = (bw - aw, p.Index);
                    }
                }
            }
        }
        return best;
    }
}
