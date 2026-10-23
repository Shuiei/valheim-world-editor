using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The running extensions' placers (IObjectPlacer): what a mod works out from where its objects stand, set on the
/// objects a step of the editor places, pastes, moves or turns.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension places objects of a prefab.</summary>
    /// <param name="prefab">The prefab's hash.</param>
    public static bool HasPlacer(int prefab) => _state.Hooks.Any(l => l.Fault == null && l.Placers.Any(p => p.Prefabs.ContainsKey(prefab)));

    /// <summary>
    /// The name a running placer knows a prefab by (null when none places it), for the views of objects whose prefab
    /// the editor cannot name otherwise.
    /// </summary>
    /// <param name="prefab">The prefab's hash.</param>
    public static string? PlacedName(int prefab) =>
        _state.Hooks.SelectMany(l => l.Placers).Select(p => p.Prefabs.GetValueOrDefault(prefab)).FirstOrDefault(n => n != null);

    /// <summary>
    /// Hands one step's placed objects to each running placer, those of its prefabs only, all at once; changed in
    /// place. A placer that throws turns its extension off (the others go on).
    /// </summary>
    /// <param name="placed">The step's placed objects, each with its prefab's hash.</param>
    /// <param name="area">The area as it stands before the step, without the objects it moves.</param>
    public static void Place(IReadOnlyList<(int Prefab, ICopy Copy)> placed, IAreaView area)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var (placer, prefabs) in l.Placers)
            {
                var mine = placed.Where(p => prefabs.ContainsKey(p.Prefab)).Select(p => p.Copy).ToList();
                if (mine.Count > 0)
                {
                    Guard(l, () =>
                    {
                        placer.Place(mine, area);
                        return true;
                    }, false);
                }
            }
        }
    }
}
