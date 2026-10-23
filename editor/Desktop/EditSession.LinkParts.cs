using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Linked groups: which things a selection brings along with its groups.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Things with the rest of the linked groups (ObjectLinks) and tie groups (ObjectTies) they belong to: each group's
    /// parts in the area come after the things given (which keep their order).
    /// </summary>
    /// <param name="indices">Indices into the scene's things.</param>
    /// <param name="anyPart">Any part brings its whole group (the Select tool); false: only a root brings its parts.</param>
    /// <returns>
    /// The things, which of them are groups' parts (roots too), how many groups they hold, and what a group is called
    /// (its mod's name for them when all are of one kind, else <see cref="LinkRule.DefaultName"/>).
    /// </returns>
    public (List<int> Indices, HashSet<int> Parts, int Groups, string Noun) WithLinked(IEnumerable<int> indices, bool anyPart = true)
    {
        var list = indices.ToList();
        var parts = new HashSet<int>();
        var nouns = new HashSet<string>();
        var links = ReadLinks();
        if (links.IsEmpty)
        {
            int tied = WithTied(list, parts, nouns, anyPart);
            return (list, parts, tied, nouns.Count == 1 ? nouns.First() : LinkRule.DefaultName);
        }
        var things = Scene.Things;
        var at = ShownById();
        var have = list.ToHashSet();
        var roots = new HashSet<int>();
        foreach (int i in list.ToList())
        {
            if (i < 0 || i >= things.Count || links.Get(things[i].Id) == null)
            {
                continue;
            }
            int id = things[i].Id;
            var group = links.Group(id);
            if (group.Count < 2 || !anyPart && group[0] != id || !roots.Add(group[0]))
            {
                continue;
            }
            nouns.Add(links.NameOf(id) ?? LinkRule.DefaultName);
            parts.Add(i);
            foreach (int part in group)
            {
                if (at.TryGetValue(part, out int k))
                {
                    parts.Add(k);
                    if (have.Add(k))
                    {
                        list.Add(k);
                    }
                }
            }
        }
        int groups = roots.Count + WithTied(list, parts, nouns, anyPart);
        return (list, parts, groups, nouns.Count == 1 ? nouns.First() : LinkRule.DefaultName);
    }
}
