using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public static partial class Workshop
{
    /// <summary>How many of each kind of building piece stand on the plot (prefab name → count).</summary>
    public static Dictionary<string, int> Kinds(WorldScene s)
    {
        lock (s.Things)
        {
            return s.Things.Where(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null).GroupBy(t => PrefabCatalog.NameOf(t.Prefab) ?? "?").ToDictionary(g => g.Key, g => g.Count());
        }
    }

    /// <summary>How many building pieces stand on the plot.</summary>
    public static int Pieces(WorldScene s)
    {
        lock (s.Things)
        {
            return s.Things.Count(t => !t.Gone && PieceCatalog.Get(t.Prefab) != null);
        }
    }
}
