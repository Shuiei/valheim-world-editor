using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// What running extensions keep of a copied place and put down where it is pasted (IAreaCopier), each call guarded.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension keeps anything with copies.</summary>
    public static bool AnyCopiers => _state.Hooks.Any(l => l.Copiers.Count > 0);

    /// <summary>
    /// What the running extensions keep of a place being copied, by copier name (the mod's name and the copier's id,
    /// "ExampleVehicles/canals"; the first of a name): those that keep something. A copier that throws turns its
    /// extension off (the others go on).
    /// </summary>
    /// <param name="area">The area copied from.</param>
    /// <param name="region">What is copied.</param>
    public static Dictionary<string, byte[]> CopyArea(IAreaView area, CopyRegion region)
    {
        var kept = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var l in _state.Hooks)
        {
            foreach (var (id, copier) in l.Copiers)
            {
                if (Guard(l, () => copier.Copy(area, region), null) is { Length: > 0 } bytes)
                {
                    kept.TryAdd(l.Mod + "/" + id, bytes);
                }
            }
        }
        return kept;
    }

    /// <summary>
    /// Puts down what a pasted copy kept: each running copier's edit, in the order kept. What no running copier reads
    /// (its mod's extension not installed or turned off) is left, its name listed.
    /// </summary>
    /// <param name="area">The area pasted into.</param>
    /// <param name="kept">What the copy kept, by copier name (see <see cref="CopyArea"/>).</param>
    /// <param name="copies">Where each copy lands.</param>
    /// <param name="unread">The names of what no running copier read; added to.</param>
    public static List<ToolEdit> PasteArea(IAreaView area, IReadOnlyDictionary<string, byte[]> kept, IReadOnlyList<CopyPlacement> copies, ICollection<string> unread)
    {
        var edits = new List<ToolEdit>();
        foreach (var (name, bytes) in kept)
        {
            if (CopierNamed(name) is not var (l, copier))
            {
                unread.Add(name);
                continue;
            }
            if (Guard(l, () => copier.Paste(area, bytes, copies), null) is { } edit)
            {
                edits.Add(edit);
            }
        }
        return edits;
    }

    /// <summary>The running copier of a name, with its extension; null when none.</summary>
    private static (Loaded, IAreaCopier)? CopierNamed(string name)
    {
        foreach (var l in _state.Hooks.Where(l => l.Fault == null))
        {
            foreach (var (id, copier) in l.Copiers)
            {
                if (name == l.Mod + "/" + id)
                {
                    return (l, copier);
                }
            }
        }
        return null;
    }
}
