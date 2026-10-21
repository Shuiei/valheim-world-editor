
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Lists an object where the editor shows it: a piece with its creator, a disc, a runestone, or any other shown object.</summary>
    /// <param name="site">The object.</param>
    /// <param name="f">The data its sections gave.</param>
    /// <param name="terrain">Whether it carries ground data (then it is not listed as an object).</param>
    private void RecordPlacement(ZdoSite site, ZdoFields f, bool terrain)
    {
        if (f.Creator != 0)
        {
            Creators[f.Creator] = Creators.GetValueOrDefault(f.Creator) + 1;
            Pieces.Add((site.Id, site.Prefab, site.Position, site.Rotation.Y));
        }
        else if (site.Shown && !terrain)
        {
            Objects.Add((site.Id, site.Prefab, site.Position, site.Rotation, f.Scale));
        }
        else if (site.Prefab == LocationProxyPrefab && Editing.Uplift.IsDisc(f.Location))
        {
            Discs.Add((site.Id, site.Position));
        }
        else if (site.Prefab == LocationProxyPrefab && Terrain.PrefabCatalog.IsRunestone(f.Location))
        {
            // A runestone: listed under its location's name (see PrefabCatalog.RunestoneLocations).
            Objects.Add((site.Id, f.Location, site.Position, site.Rotation, f.Scale));
        }
    }
}
