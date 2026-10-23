using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The link placers running extensions give for their prefabs (ILinkPlacer): the frame a linked object's parts hang in,
/// and where a part stands on what it hangs on, when the data alone does not say it.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>Whether a running extension places linked parts for a prefab (as a parent or as a part).</summary>
    public static bool HasLinkPlacer(int prefab) => _state.Hooks.Any(l => l.Placers.ContainsKey(prefab));

    /// <summary>
    /// A prefab's name as an extension placing for it named it (null when none does): what its placer is shown, where
    /// the game's catalogue does not name it (a world opened before the game's data is read).
    /// </summary>
    public static string? LinkPlacerPrefabName(int prefab) =>
        _state.Hooks.Select(l => l.PlacerPrefabs.GetValueOrDefault(prefab)).FirstOrDefault(n => n != null);

    /// <summary>
    /// The frame the parts of an object of a prefab hang in, in its own frame (the first a running extension gives);
    /// null: its own frame.
    /// </summary>
    /// <param name="prefab">The object's prefab hash.</param>
    /// <param name="obj">The object, as drawn.</param>
    public static (Vector3 Offset, Quaternion Turn)? LinkFrameOf(int prefab, IObjectView obj)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Placers.GetValueOrDefault(prefab) ?? [])
            {
                if (Usable(Guard(l, () => p.FrameOf(obj), null)) is { } frame)
                {
                    return frame;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Where a part of a prefab stands in the frame of what it hangs on (the first a running extension gives); null: as
    /// its data says.
    /// </summary>
    /// <param name="prefab">The part's prefab hash.</param>
    /// <param name="part">The part, at its saved place and turn.</param>
    /// <param name="parent">What it hangs on, as drawn.</param>
    public static (Vector3 Offset, Quaternion Turn)? LinkPlaceOf(int prefab, IObjectView part, IObjectView parent)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Placers.GetValueOrDefault(prefab) ?? [])
            {
                if (Usable(Guard(l, () => p.PlaceOf(part, parent), null)) is { } place)
                {
                    return place;
                }
            }
        }
        return null;
    }

    /// <summary>A frame's place and turn, its turn normalized; null for none, or numbers that are not (NaN, infinite, no turn).</summary>
    private static (Vector3 Offset, Quaternion Turn)? Usable(LinkFrame? f)
    {
        if (f == null)
        {
            return null;
        }
        var (o, q) = (f.Offset, f.Turn);
        bool finite = float.IsFinite(o.X) && float.IsFinite(o.Y) && float.IsFinite(o.Z)
            && float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);
        return finite && q.LengthSquared() > 1e-8f ? (o, Quaternion.Normalize(q)) : null;
    }
}
