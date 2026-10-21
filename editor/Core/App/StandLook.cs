using System.Collections.Concurrent;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// How the 3D view draws the items stands hold: the held models' names and keys (a held model is drawn like a
/// kind of object, under a key of its own), and where an item goes on its stand, as the game places it.
/// </summary>
public static partial class StandLook
{
    /// <summary>The held model names by their keys (the view asks a kind's name by its key).</summary>
    private static readonly ConcurrentDictionary<int, string> Names = new();

    /// <summary>
    /// A held model's name: the item's prefab name and its use ("ShieldBronzeBuckler/stand", "HelmetBronze/hand",
    /// "SwordBronze/back", "ArmorBronzeChest/body").
    /// </summary>
    public static string ModelName(string item, GameBundles.HeldUse use) => item + "/" + UseName(use);

    /// <summary>The key a held model is drawn under (its name's stable hash), remembered for <see cref="NameOf"/>.</summary>
    public static int KeyOf(string modelName)
    {
        int key = StableHash.Of(modelName);
        Names.TryAdd(key, modelName);
        return key;
    }

    /// <summary>The held model name of a key given by <see cref="KeyOf"/>; null for any other key.</summary>
    public static string? NameOf(int key) => Names.GetValueOrDefault(key);

    /// <summary>The item and use of a held model's name; null when the name is not one.</summary>
    public static (string Item, GameBundles.HeldUse Use)? Parse(string modelName)
    {
        int slash = modelName.LastIndexOf('/');
        if (slash <= 0)
        {
            return null;
        }
        string item = modelName[..slash];
        foreach (var use in Enum.GetValues<GameBundles.HeldUse>())
        {
            if (modelName.AsSpan(slash + 1).SequenceEqual(UseName(use)))
            {
                return (item, use);
            }
        }
        return null;
    }

    /// <summary>A use's word in a model name.</summary>
    private static string UseName(GameBundles.HeldUse use) => use switch
    {
        GameBundles.HeldUse.Stand => "stand",
        GameBundles.HeldUse.Hand => "hand",
        GameBundles.HeldUse.Back => "back",
        _ => "body",
    };

    /// <summary>
    /// The use an armour stand's slot makes of its item: which of the item's children it draws and where.
    /// </summary>
    /// <param name="visSlot">The slot (VisSlot, see <see cref="GameBundles.StandPoints.Slots"/>).</param>
    public static GameBundles.HeldUse UseOfSlot(int visSlot) => visSlot switch
    {
        0 or 1 or 6 or 9 or 10 => GameBundles.HeldUse.Hand,
        2 or 3 => GameBundles.HeldUse.Back,
        _ => GameBundles.HeldUse.Body,
    };
}
