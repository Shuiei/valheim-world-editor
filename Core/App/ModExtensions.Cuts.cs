using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The ground cuts running extensions make for the objects of their prefabs.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension cuts the ground at all.</summary>
    public static bool AnyCuts => _state.Hooks.Any(l => l.Cutters.Count > 0);

    /// <summary>Whether a running extension cuts the ground for a prefab.</summary>
    public static bool HasCuts(int prefab) => _state.Hooks.Any(l => l.Cutters.ContainsKey(prefab));

    /// <summary>Where running extensions leave the ground undrawn for an object of a prefab (none when none does).</summary>
    /// <param name="prefab">The prefab's hash.</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="world">The world around it.</param>
    public static List<GroundCut> CutsOf(int prefab, IObjectView obj, IWorldView world)
    {
        var cuts = new List<GroundCut>();
        foreach (var l in _state.Hooks)
        {
            foreach (var c in l.Cutters.GetValueOrDefault(prefab) ?? [])
            {
                cuts.AddRange(Guard(l, () => c.CutsOf(obj, world)?.Where(ValidCut).ToList() ?? [], []));
            }
        }
        return cuts;
    }

    /// <summary>Whether a cut can be made: an outline of at least three finite points.</summary>
    private static bool ValidCut(GroundCut c) =>
        c.Outline is { Length: >= 3 } && c.Outline.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y));
}
