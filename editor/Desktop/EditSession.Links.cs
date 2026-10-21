using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Objects that mods link into one (ObjectLinks): reading the links, and placing the parts drawn where their mod puts them.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>The linked objects (ObjectLinks) as they are now: the world's objects and this session's.</summary>
    public LinkIndex ReadLinks()
    {
        var world = Scene.World;
        lock (world.Sync)
        {
            return LinkIndex.Of(world, Edits.Deleted, Edits.Added);
        }
    }

    /// <summary>The scene's things not gone, by object id (the first one when two have it).</summary>
    private Dictionary<int, int> ShownById()
    {
        var at = new Dictionary<int, int>();
        var list = Scene.Things;
        lock (list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Gone)
                {
                    at.TryAdd(list[i].Id, i);
                }
            }
        }
        return at;
    }

    /// <summary>
    /// After objects came, went or changed: the parts of linked groups drawn where their mod puts them now (their root
    /// or what they hang on may have moved, gone or come back).
    /// </summary>
    /// <returns>The things placed again, indices into the scene's things.</returns>
    private List<int> PlaceLinked()
    {
        var moved = new List<int>();
        var links = ReadLinks();
        if (links.IsEmpty)
        {
            return moved;
        }
        var list = Scene.Things;
        lock (list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var placed = t.Gone ? t : WorldScene.Placed(t, links);
                if (placed != t)
                {
                    list[i] = placed;
                    moved.Add(i);
                }
            }
        }
        return moved;
    }

    /// <summary>Raises ThingsChanged for these things and the linked groups' parts placed again after them.</summary>
    private void Notify(IEnumerable<int> indices)
    {
        int[] all = [.. indices.Concat(PlaceLinked()).Distinct()];
        if (all.Length > 0)
        {
            ThingsChanged?.Invoke(all);
        }
    }
}
