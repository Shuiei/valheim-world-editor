using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Presets: the built-in ones, their names, and loading one into the tool.
public sealed partial class PlaceTool
{
    /// <summary>A preset: kinds with their weights and the scatter settings.</summary>
    /// <param name="Name">The preset as listed.</param>
    /// <param name="Kinds">The kinds by prefab name, with their weights.</param>
    /// <param name="Density">Objects per 100 m².</param>
    /// <param name="Spacing">The least distance between objects, in metres.</param>
    /// <param name="SizeMin">Smallest scale, in percent.</param>
    /// <param name="SizeMax">Largest scale, in percent.</param>
    /// <param name="Tilt">Most random lean, in degrees.</param>
    /// <param name="Clump">Clumping, 0 to 100%.</param>
    /// <param name="Patch">The clumping pattern's size, in metres.</param>
    public sealed record Preset(string Name, Dictionary<string, int> Kinds, float Density, float Spacing, float SizeMin, float SizeMax, float Tilt, float Clump = 0, float Patch = 30);

    /// <summary>The presets that come with the editor.</summary>
    public static readonly Preset[] BuiltIn =
    [
        new("Meadows woods", new() { ["Beech1"] = 4, ["Birch1"] = 1, ["Birch2"] = 1, ["Oak1"] = 1, ["Bush01"] = 2 }, 3, 4, 80, 120, 3, 50, 35),
        new("Black forest", new() { ["FirTree"] = 3, ["Pinetree_01"] = 3, ["FirTree_big"] = 1, ["Bush01"] = 1 }, 4, 3.5f, 80, 130, 2, 30, 40),
        new("Swamp", new() { ["SwampTree1"] = 3, ["SwampTree2"] = 1 }, 2, 5, 80, 120, 4),
        new("Berry patch", new() { ["RaspberryBush"] = 2, ["BlueberryBush"] = 1 }, 6, 3, 90, 110, 0, 70, 15),
        new("Forest floor", new() { ["Pickable_Branch"] = 3, ["Pickable_Stone"] = 2, ["Pickable_Mushroom"] = 1, ["Pickable_Dandelion"] = 1 }, 2, 3, 100, 100, 0),
        new("Meadows rocks", new() { ["Rock_3"] = 2, ["Rock_4"] = 1, ["Rock_7"] = 1 }, 1, 6, 60, 140, 10, 40, 25),
    ];

    /// <summary>
    /// A preset's name as listed: a built-in one's in the language in use, one of yours as you named it.
    /// </summary>
    /// <param name="p">The preset.</param>
    public static string ShownName(Preset p)
    {
        return Array.IndexOf(BuiltIn, p) switch
        {
            0 => Lang.T("place.preset.meadowsWoods"),
            1 => Lang.T("place.preset.blackForest"),
            2 => Lang.T("place.preset.swamp"),
            3 => Lang.T("place.preset.berryPatch"),
            4 => Lang.T("place.preset.forestFloor"),
            5 => Lang.T("place.preset.meadowsRocks"),
            _ => p.Name,
        };
    }

    /// <summary>Loads a preset (kinds the world cannot make are left out). Returns what to say.</summary>
    /// <param name="p">The preset.</param>
    /// <param name="canMake">Whether the world can make a kind (by prefab name).</param>
    public Said Load(Preset p, Func<string, bool> canMake)
    {
        var name = new Said(ShownName(p), p.Name);
        var kinds = p.Kinds.Where(k => canMake(k.Key)).ToList();
        if (kinds.Count == 0)
        {
            return Said.Of("place.msg.presetNone", name);
        }
        Chosen.Clear();
        foreach (var (n, w) in kinds)
        {
            Chosen.Add(n);
            Weights[n] = w;
        }
        (Density, Spacing, SizeMin, SizeMax, Tilt, Clump, Patch, RandomYaw) = (p.Density, p.Spacing, p.SizeMin, p.SizeMax, p.Tilt, p.Clump, p.Patch, true);
        AutoSnap();
        NewLayout();
        int left = p.Kinds.Count - kinds.Count;
        string list = string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}"));
        return left > 0 ? Said.Of("place.msg.presetLeftOut", name, list, left) : Said.Of("place.msg.preset", name, list);
    }
}
