using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The kinds the editor can make now, and how the search matches them.
public sealed partial class PlacePanel
{
    /// <summary>
    /// The kinds that can be placed in this world, with their kind of object; worked out again for another world or
    /// when the mods' manifests or catalogue change (empty while no world is open).
    /// </summary>
    internal List<(string Name, ObjectKind Kind)> Creatable()
    {
        if (_scene()?.World is { } w && _creatableFrom != (w, ModManifests.Generation, ModCatalogue.Generation))
        {
            _creatableFrom = (w, ModManifests.Generation, ModCatalogue.Generation);
            // Not the prefabs a mod's manifest asks not to offer (its tools, effects, helpers), nor its prefabs the game
            // draws nothing of (sounds, effects), nor chosen any more.
            _creatable = [.. w.Creatable.Select(p => _nameOf(p)).Where(n => n != null && !NotOffered(n)).Distinct().Select(n => (n!, ObjectKinds.Of(n, false)))];
            if (T.Chosen.RemoveAll(NotOffered) > 0)
            {
                // What is chosen changed: shown again, and the tool told.
                Dispatcher.UIThread.Post(Fill);
                T.Notify();
            }
            // The chips, and the list while it shows, show only what is offered.
            Dispatcher.UIThread.Post(RenderChips);
            Dispatcher.UIThread.Post(FillList);
        }
        return _creatable;
    }

    /// <summary>
    /// Whether a prefab is not offered: a mod's manifest hides it, or it is a mod's helper the game draws nothing of (its
    /// reported model has no parts: a sound, an effect, a marker). Items and pickables are offered whatever their model.
    /// Objects of the kind already in a world still show.
    /// </summary>
    internal static bool NotOffered(string name) =>
        ModManifests.IsHidden(name) || ModCatalogue.ModelOf(name) is { Parts.Count: 0 }
            && PrefabCatalog.ItemKindOf(Save.StableHash.Of(name)) == null && ObjectKinds.Of(name, false) != ObjectKind.Pickables;

    /// <summary>
    /// Whether a kind matches the search: by its name, or for spawn points and nests by a creature they spawn.
    /// </summary>
    internal static bool Matches(string name, string q) =>
        name.Contains(q, StringComparison.OrdinalIgnoreCase) || PrefabCatalog.SpawnOf(name) is { } spawn && spawn.Creatures.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase));

    /// <summary>The list's categories, in order.</summary>
    private static readonly ObjectKind[] Order =
    [
        ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables, ObjectKind.Ore, ObjectKind.Ruins, ObjectKind.Furniture, ObjectKind.Remains,
        ObjectKind.SpawnPoints, ObjectKind.Nests, ObjectKind.Creatures, ObjectKind.Effects, ObjectKind.Buildings, ObjectKind.Other,
    ];
}
