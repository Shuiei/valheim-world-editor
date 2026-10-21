
namespace TerrainEditor.App;

/// <summary>
/// Finding the members of a linked world: the object holding a link, a group's root, its parts and its name.
/// </summary>
public sealed partial class LinkIndex
{
    /// <summary>The object a member hangs on (holding its parent id under its rule's id key); null when none does.</summary>
    private int? HolderOf(Member m) => m.Link.Holder != 0 && _holders.TryGetValue((m.Link.Rule.IdKey, m.Link.Holder), out int up) ? up : null;

    /// <summary>What the group an object belongs to is called (its root's rule's name); null for one with no link.</summary>
    public string? NameOf(int id) => Get(RootOf(id))?.Link.Rule.Name;

    /// <summary>
    /// The objects from one up to the top of its chain (what it hangs on, what that hangs on...); null when the chain
    /// loops or goes deeper than <see cref="ObjectLinks.MaxDepth"/> (damaged data).
    /// </summary>
    private List<Member>? Chain(Member m)
    {
        var chain = new List<Member> { m };
        var seen = new HashSet<int> { m.Id };
        while (HolderOf(chain[^1]) is int up)
        {
            if (!seen.Add(up) || chain.Count > ObjectLinks.MaxDepth)
            {
                return null;
            }
            chain.Add(_members[up]);
        }
        return chain;
    }

    /// <summary>The root of an object's group (the top of its chain; itself when it hangs on nothing found).</summary>
    public int RootOf(int id)
    {
        if (Get(id) is not { } m)
        {
            return id;
        }
        var seen = new HashSet<int> { id };
        while (HolderOf(m) is int up && seen.Add(up))
        {
            m = _members[up];
        }
        return m.Id;
    }

    /// <summary>
    /// Every object of the group an object belongs to: its root first, then every object whose chain leads there. Just
    /// the object for one with no link.
    /// </summary>
    public List<int> Group(int id)
    {
        int root = RootOf(id);
        var group = new List<int> { root };
        var seen = new HashSet<int> { root };
        for (int k = 0; k < group.Count; k++)
        {
            foreach (int child in _children.GetValueOrDefault(group[k]) ?? [])
            {
                if (seen.Add(child))
                {
                    group.Add(child);
                }
            }
        }
        return group;
    }
}
