using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Reads an object's ground data and adds it to the terrain zones; damaged data keeps the object as an ordinary one.</summary>
    /// <param name="site">The object.</param>
    /// <param name="zdo">The reader positioned at the entry.</param>
    /// <param name="pkg">The reader of the file (for where the data ends).</param>
    private void ReadTerrainField(ZdoSite site, ZdoReader zdo, ValheimReader pkg)
    {
        TerrainZone zone;
        try
        {
            zone = TerrainZone.Decode(site.Position, site.Prefab, zdo.BytesValue!);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            // Damaged ground data: the game loses only this zone's ground edits. Here the object stays an
            // ordinary one, kept byte for byte, and the rest of the world reads on.
            Log.CaughtBrief(ex, $"reading the ground data of the terrain object at {site.Position} (damaged: kept as it is)");
            return;
        }
        zone.Source = new ZdoLocation(site.File, site.Start, -1, zdo.ValueStart, pkg.Position, site.Flags);
        TerrainZones.Add(zone);
    }
}
