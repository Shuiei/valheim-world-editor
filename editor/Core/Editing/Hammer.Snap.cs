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
    /// <param name="prefab">The piece's prefab name.</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="pos">Where it is before snapping.</param>
    /// <param name="pieces">The pieces already there.</param>
    /// <param name="own">
    /// Manual snapping (the editor's own): only this snap point of the piece is matched (wrapped to its number of snap
    /// points), against the nearest snap point within <see cref="ManualSnapDistance"/>; null, the game's way.
    /// </param>
    public static (Vector3 Position, int? SnappedTo) Snap(string prefab, Quaternion rotation, Vector3 pos, IReadOnlyList<Placed> pieces, int? own = null)
    {
        if (Get(prefab) is not { Snaps.Length: > 0 } data)
        {
            return (pos, null);
        }
        int? only = own is int k ? Wrap(k, data.Snaps.Length) : null;
        if (NearestSnapMove(data, rotation, pos, pieces, only) is not { } nearest)
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
    /// <param name="data">The piece's placing data.</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="pos">Where it is.</param>
    /// <param name="pieces">The pieces already there.</param>
    /// <param name="only">Only this snap point of the piece, within <see cref="ManualSnapDistance"/>; null: any, within <see cref="SnapDistance"/>.</param>
    internal static (Vector3 Move, int Index)? NearestSnapMove(Data data, Quaternion rotation, Vector3 pos, IReadOnlyList<Placed> pieces, int? only = null)
    {
        float bestD = only == null ? SnapDistance : ManualSnapDistance;
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
                for (int a = 0; a < own.Length; a++)
                {
                    if (only is int o && a != o)
                    {
                        continue;
                    }
                    var aw = own[a];
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

    /// <summary>How many snap points a piece has (0: none, or not a piece the hammer knows).</summary>
    public static int SnapCount(string prefab) => Get(prefab)?.Snaps.Length ?? 0;

    /// <summary>
    /// Where a piece's snap point is in the world, the piece at pos turned by rotation; the index wraps to its number of
    /// snap points. Null when it has none.
    /// </summary>
    public static Vector3? SnapPointAt(string prefab, Quaternion rotation, Vector3 pos, int index) =>
        Get(prefab) is { Snaps.Length: > 0 } data ? pos + Vector3.Transform(data.Snaps[Wrap(index, data.Snaps.Length)], rotation) : null;

    /// <summary>An index wrapped into 0 … count - 1 (negative ones from the end).</summary>
    private static int Wrap(int index, int count) => (index % count + count) % count;
}
