using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// Remapping the link groups of a pasted copy: each group gets new ids, its references follow them, and its values the
/// rules leave out of copies go.
/// </summary>
public static partial class ObjectLinks
{
    /// <summary>
    /// One pasted copy's objects made groups of their own. Each id gets a new one, unique in the world, and every
    /// reference to it follows (parents and the rules' references): two groups under one id would share their parts in
    /// the game. A part whose parent is in the copy is stored at its group's root when the mod saves it so; one whose
    /// parent was not copied is no part any more (its links are taken away) and stays where it was pasted. Values the
    /// rules leave out of copies go. Objects that are no group's come back as they were.
    /// </summary>
    /// <param name="copy">The copy's new objects, each with all its data (as the clipboard holds them).</param>
    /// <param name="taken">The ids in use; the new ones are added to it.</param>
    /// <param name="mirror">The copy is mirrored east to west: the parts' offsets and turns in their parents' frames too.</param>
    /// <param name="random">Draws the new ids (null: the shared one).</param>
    /// <returns>The objects as they are to be made, in the copy's order.</returns>
    public static NewObject[] Remap(IReadOnlyList<NewObject> copy, ISet<int> taken, bool mirror = false, Random? random = null)
    {
        var set = Current;
        // What has no link but names another object, or holds what copies leave out (a sail on a mast standing on its
        // own), is looked at too.
        var links = copy.Select(o => With(null, o.Data) ?? Named(set, o.Data)).ToList();
        // Values extensions rewrite (ModExtensions): theirs to decide, not dropped.
        var rewritten = ModExtensions.RewrittenKeys;
        bool rewrite = rewritten.Count > 0 && copy.Any(o => o.Data?.Any(f => f.Value != null && rewritten.Contains(f.Key)) == true);
        random ??= Random.Shared;
        // Ties (ObjectTies) made the copy's own first: a root and its members get new ids of their own.
        var tieRules = ObjectTies.Current;
        if (ObjectTies.AnyIn(copy, tieRules))
        {
            copy = ObjectTies.Remap(copy, tieRules, random);
        }
        if (links.All(l => l == null) && !rewrite)
        {
            return [.. copy];
        }
        var (at, ids) = NewCopyIds(links, taken, random);
        var result = new NewObject[copy.Count];
        for (int i = 0; i < copy.Count; i++)
        {
            result[i] = links[i] is { } link
                ? RemapPart(copy, links, i, link, at, ids, taken, random, mirror, rewritten)
                : copy[i];
        }
        if (rewrite)
        {
            ApplyExtensionRewrites(result, ids, rewritten);
        }
        return result;
    }

    /// <summary>Then the extensions rewrite the remapped objects, with the copy's new ids.</summary>
    private static void ApplyExtensionRewrites(NewObject[] result, Dictionary<int, int> ids, IReadOnlySet<int> rewritten)
    {
        var map = new IdMap(ids);
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] is { Data: { } data } o && data.Any(f => f.Value != null && rewritten.Contains(f.Key)))
            {
                var view = new CopyView(PrefabCatalog.NameOf(o.Prefab) ?? "", o.Position, o.Rotation, [.. data], o.Scale);
                ModExtensions.Rewrite(view, map);
                result[i] = o with { Data = view.Data };
            }
        }
    }

    /// <summary>A copy's new ids for the originals' (ICopyRewriter).</summary>
    private sealed class IdMap(Dictionary<int, int> ids) : ValheimWorldEditor.Sdk.IIdMap
    {
        /// <inheritdoc/>
        public bool TryGetNew(int oldId, out int newId) => ids.TryGetValue(oldId, out newId);
    }
}
