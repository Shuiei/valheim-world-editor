using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Grow room: the kinds that need room around them, and the cultivated ground they need.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Turns End to end and One at a time on when every chosen kind is a snapping piece (off otherwise), unless they
    /// were set by hand.
    /// </summary>
    public void AutoSnap()
    {
        bool pieces = Chosen.Count > 0 && Chosen.All(IsPiece);
        if (!EndToEndByHand)
        {
            EndToEnd = pieces;
        }
        if (!OneAtATimeByHand)
        {
            OneAtATime = pieces;
        }
    }

    /// <summary>
    /// Saplings and crops: the game lets them grow only with nothing within their grow radius; some only on cultivated
    /// ground. By prefab name.
    /// </summary>
    private static IReadOnlyDictionary<string, (float Radius, bool Cultivated)> GrowInfo => PrefabCatalog.GrowRoom;

    /// <summary>Whether any chosen kind needs room to grow.</summary>
    public bool GrowMatters => Chosen.Any(n => GrowInfo.TryGetValue(n, out var g) && g.Radius > 0);

    /// <summary>The chosen kinds that only grow on cultivated ground.</summary>
    public List<string> NeedCultivated => [.. Chosen.Where(n => GrowInfo.TryGetValue(n, out var g) && g.Cultivated)];

    /// <summary>The room a kind needs to grow, in metres (0 when it needs none or Leave room to grow is off).</summary>
    private float GrowNeed(string name) => GrowMatters && GrowRoom && GrowInfo.TryGetValue(name, out var g) ? g.Radius : 0;
}
