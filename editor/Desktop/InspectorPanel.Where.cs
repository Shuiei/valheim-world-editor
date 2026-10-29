using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// The lines under the card's title: the object's id, place and turn (and whether it has a connection), what it
    /// spawns when it is a spawn point or a nest, and the mod it comes from.
    /// </summary>
    /// <param name="z">The object's data.</param>
    /// <param name="id">The object's id in the area (0: none).</param>
    /// <param name="name">Its name as the game shows it, null when the catalogue has none.</param>
    private static string Where(ZdoData z, int id, string? name)
    {
        var lines = new List<string>
        {
            z.Connection != null
                ? Lang.T("inspect.whereConnected", id, z.Position.X, z.Position.Y, z.Position.Z, z.Rotation.Y)
                : Lang.T("inspect.where", id, z.Position.X, z.Position.Y, z.Position.Z, z.Rotation.Y),
        };
        if (PrefabCatalog.SpawnOf(z.Prefab) is { } spawn)
        {
            lines.Add(spawn.Nest ? Lang.T("inspect.nest", spawn.Summary()) : Lang.T("inspect.spawnPoint", spawn.Summary()));
        }
        if (name != null && ModCatalogue.IsModded(name))
        {
            lines.Add(ModCatalogue.ModOf(name) is string mod ? Lang.T("inspect.fromMod", mod) : Lang.T("inspect.fromAMod"));
        }
        return string.Join("\n", lines);
    }
}
