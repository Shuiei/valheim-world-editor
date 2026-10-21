// The Transform and GameObject levels of the build pieces, read in the order of the bundle (see GameCatalogues).

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// What the pieces' readers walk below the prefabs (pieces, piece-support, piece-place), read once for
    /// all, first, each time in the order of the file (the bundle is read in blocks, and a prefab's objects are far
    /// apart): a level's Transforms, the GameObjects of the level above and the colliders of the one above
    /// that, as many times as there are levels. Below snap points nothing is read; a level is read before
    /// its parent's GameObject tells whether to go there.
    /// </summary>
    private static void PieceTrees(Bundle b, Game game) => TreesRead.GetValue(b, _ =>
    {
        IEnumerable<long> Of(long go, Func<int, bool> type) => b.Field(go) is { } g ? b.ComponentIds(g).Where(c => type(c.Type)).Select(c => c.Pid) : [];
        var gos = PieceRoots(b, game).Select(p => p.Pid).ToList();
        // The Transforms to read, with their parent's GameObject (0 for a prefab's root).
        var transforms = gos.SelectMany(g => Of(g, t => t == TransformType)).Select(t => (Pid: t, Parent: 0L)).ToList();
        var cols = new List<long>();
        while (transforms.Count + gos.Count + cols.Count > 0)
        {
            b.ReadInOrder(transforms.Select(t => t.Pid).Concat(gos).Concat(cols));
            cols = [.. gos.SelectMany(g => Of(g, IsCollider))];
            gos = [];
            var next = new List<(long Pid, long Parent)>();
            foreach (var (t, parent) in transforms)
            {
                if ((parent == 0 || b.Field(parent) is { } p && !IsSnapPoint(p)) && b.Field(t) is { } tr)
                {
                    long go = tr["m_GameObject.m_PathID"].AsLong;
                    gos.Add(go);
                    next.AddRange(tr["m_Children.Array"].Children.Select(c => (c["m_PathID"].AsLong, go)));
                }
            }
            transforms = next;
        }
        return new object();
    });

    /// <summary>
    /// The bundles <c>PieceTrees</c> has read already.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bundle, object> TreesRead = [];
}
