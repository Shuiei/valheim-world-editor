using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Desktop;

public sealed partial class WorldScene
{
    /// <summary>
    /// Objects by id wherever they are, as things gone or not (deleted, or added and undone, they are gone):
    /// what a history fitted to an area needs that the area does not show. Ids not found are left out.
    /// </summary>
    public static Dictionary<int, Thing> FindThings(WorldSave world, EditStore edits, IReadOnlyCollection<int> ids)
    {
        lock (world.Sync)
        {
            return FindThingsIn(world, edits.Deleted, edits.Added, edits.FindAdded, ids);
        }
    }

    /// <summary>
    /// <see cref="FindThings(WorldSave, EditStore, IReadOnlyCollection{int})"/> from the edits' objects taken earlier:
    /// what a reader on another thread finds as the edits were then.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="deleted">The saved objects deleted (<c>EditStore.Deleted</c>).</param>
    /// <param name="added">The objects added and not deleted again (<c>EditStore.Added</c>).</param>
    /// <param name="findAdded">An object added by id, deleted again or not (<c>EditStore.FindAdded</c>).</param>
    /// <param name="ids">The objects to find.</param>
    public static Dictionary<int, Thing> FindThings(WorldSave world, HashSet<int> deleted, List<NewObject> added, Func<int, NewObject?> findAdded,
        IReadOnlyCollection<int> ids)
    {
        lock (world.Sync)
        {
            return FindThingsIn(world, deleted, added, findAdded, ids);
        }
    }

    /// <summary><c>FindThings</c>' work, with the world's lock held.</summary>
    private static Dictionary<int, Thing> FindThingsIn(WorldSave world, HashSet<int> deleted, List<NewObject> addedNow, Func<int, NewObject?> findAdded,
        IReadOnlyCollection<int> ids)
    {
        var want = ids.ToHashSet();
        var added = addedNow.Select(n => n.Id).ToHashSet();
        var links = LinkIndex.Of(world, deleted, addedNow);
        var found = new Dictionary<int, Thing>();
        foreach (int id in want.Where(i => i < 0))
        {
            if (findAdded(id) is { } n)
            {
                found[id] = Placed(new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Gone = !added.Contains(id), Tamed = TamedOf(world, n) }, links);
            }
        }
        if (want.Any(i => i >= 0))
        {
            foreach (var (id, prefab, p, r, sc) in world.Objects)
            {
                if (want.Contains(id))
                {
                    found[id] = Placed(new Thing(id, prefab, p, r, sc.X, false) { Gone = deleted.Contains(id), Tamed = world.Tamed.Contains(id) }, links);
                }
            }
            foreach (var (id, prefab, p, ry) in world.Pieces)
            {
                if (want.Contains(id))
                {
                    found[id] = Placed(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true) { Gone = deleted.Contains(id) }, links);
                }
            }
            // Live: removed in the game since (gone for good, but still in the history).
            foreach (int id in want.Where(i => i >= 0 && i < world.ObjectRefs.Count && world.Vanished.Contains(i) && !found.ContainsKey(i)))
            {
                var o = world.ObjectRefs[id];
                found[id] = new Thing(id, o.Prefab, o.Position, Vector3.Zero, 0, o.IsPiece) { Gone = true };
            }
        }
        return found;
    }

    /// <summary>
    /// After an edit: the heights, paint mask and limit marks of a rectangle of grid points from the ground
    /// (grid points x0..x1, z0..z1, inclusive).
    /// </summary>
    public void Refresh(Ground g, int x0, int z0, int x1, int z1)
    {
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int p = gz * W + gx;
                Heights[p] = g.HeightOf(p);
                Limit[p] = g.AtLimit(p) ? 1 : 0;
                for (int c = 0; c < 4; c++)
                {
                    Mask[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(g.MaskOf(p, c) * 255f), 0, 255);
                }
            }
        }
    }
}
