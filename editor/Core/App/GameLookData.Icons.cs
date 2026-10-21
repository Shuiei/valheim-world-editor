namespace TerrainEditor.App;

// The map's location icons, from the game's minimap.
public static partial class GameLookData
{
    /// <summary>
    /// The minimap's location icons by name (Minimap.m_locationIcons: the start, the traders...), rows top first; empty
    /// when the main scene has no minimap. An icon whose sprite cannot be read is left out.
    /// </summary>
    public static Dictionary<string, Picture> ReadLocationIcons(GameBundles game)
    {
        var icons = new Dictionary<string, Picture>(StringComparer.Ordinal);
        foreach (string bundle in GameLocations.MainSceneBundles(game))
        {
            foreach (var minimap in game.MonoBehaviours(bundle).Where(f => !f["m_locationIcons"].IsDummy))
            {
                foreach (var item in minimap["m_locationIcons"]["Array"].Children)
                {
                    string name = item["m_name"].AsString;
                    if (!icons.ContainsKey(name) && game.LoadSprite(bundle, item["m_icon"]) is var (w, h, rgba))
                    {
                        icons[name] = new Picture(w, h, TopFirst(rgba, w, h));
                    }
                }
            }
            if (icons.Count > 0)
            {
                return icons;
            }
        }
        return icons;
    }

    /// <summary>Rows bottom first (the texture's) turned top first.</summary>
    private static byte[] TopFirst(byte[] rgba, int w, int h)
    {
        byte[] flipped = new byte[rgba.Length];
        for (int r = 0; r < h; r++)
        {
            System.Array.Copy(rgba, r * w * 4, flipped, (h - 1 - r) * w * 4, w * 4);
        }
        return flipped;
    }
}
