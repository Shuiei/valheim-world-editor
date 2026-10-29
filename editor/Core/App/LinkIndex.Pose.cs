using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Where a member is drawn: its position and turn, from its parent's frame up to the root it is stored at.
/// </summary>
public sealed partial class LinkIndex
{
    /// <summary>
    /// Where the mod puts an object (position in metres, turn as Unity Euler degrees): a part at its offset in its
    /// parent's frame, the parent where its own parent puts it, and so on up to the root, which is where it is stored.
    /// A carried part (its placement's) keeps where it is stored relative to its parent's stored place, drawn with its
    /// parent wherever that is drawn. A running extension's link placer (ILinkPlacer) may turn the frame a parent's
    /// parts hang in, or place a part itself. An object whose parent, or any parent up its chain, is missing (or whose
    /// chain loops) stays where it is stored. Null for an object with no link.
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
            var parent = chain[k + 1];
            var part = chain[k];
            var link = part.Link;
            if (link.Carried)
            {
                // Where it is stored, in its parent's stored frame.
                var stored = Quaternion.Inverse(BlueprintFormats.FromEuler(parent.Rotation));
                p += Vector3.Transform(Vector3.Transform(part.Position - parent.Position, stored), q);
                q = Quaternion.Normalize(q * stored * BlueprintFormats.FromEuler(part.Rotation));
            }
            else if (Placed(part, parent, p, q) is var (offset, turn))
            {
                p += Vector3.Transform(offset, q);
                q = Quaternion.Normalize(q * turn);
            }
            else
            {
                if (Frame(parent, p, q) is var (fo, ft))
                {
                    p += Vector3.Transform(fo, q);
                    q = Quaternion.Normalize(q * ft);
                }
                p += Vector3.Transform(link.Offset, q);
                q = Quaternion.Normalize(q * link.Turn);
            }
        }
        return (p, BlueprintFormats.ToEuler(q));
    }

    /// <summary>Backs the extensions' placers: a member's data by id (null: none is read).</summary>
    private Func<int, ZdoData?>? _read;

    /// <summary>The members' data read for the placers, by id (null: unreadable).</summary>
    private readonly Dictionary<int, ZdoData?> _data = [];

    /// <summary>A member's data, read once (null when it cannot be read, or the index has no world).</summary>
    private ZdoData? DataOf(Member m)
    {
        if (_read == null)
        {
            return null;
        }
        lock (_data)
        {
            if (!_data.TryGetValue(m.Id, out var z))
            {
                _data[m.Id] = z = _read(m.Id);
            }
            return z;
        }
    }

    /// <summary>A member as an extension sees it, at a place and turn.</summary>
    private ObjectView? ViewOf(Member m, Vector3 position, Quaternion turn) =>
        DataOf(m) is { } z
            ? new ObjectView(Terrain.PrefabCatalog.NameOf(m.Prefab) ?? ModExtensions.LinkPlacerPrefabName(m.Prefab) ?? "", position, BlueprintFormats.ToEuler(turn), z)
            : null;

    /// <summary>The frame a running extension says a parent's parts hang in (the parent drawn at p, q); null: its own.</summary>
    private (Vector3, Quaternion)? Frame(Member parent, Vector3 p, Quaternion q) =>
        parent.Prefab != 0 && ModExtensions.HasLinkPlacer(parent.Prefab) && ViewOf(parent, p, q) is { } view
            ? ModExtensions.LinkFrameOf(parent.Prefab, view)
            : null;

    /// <summary>Where a running extension says a part stands on its parent (drawn at p, q); null: as its data says.</summary>
    private (Vector3, Quaternion)? Placed(Member part, Member parent, Vector3 p, Quaternion q) =>
        part.Prefab != 0 && ModExtensions.HasLinkPlacer(part.Prefab)
        && ViewOf(part, part.Position, BlueprintFormats.FromEuler(part.Rotation)) is { } self && ViewOf(parent, p, q) is { } holder
            ? ModExtensions.LinkPlaceOf(part.Prefab, self, holder)
            : null;
}
