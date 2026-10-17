using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Records an object that has no data sections: its extent, its template and whether it is listed.</summary>
    /// <param name="site">The object.</param>
    /// <param name="end">Where the object ends in the file.</param>
    private void RecordWithoutData(ZdoSite site, long end)
    {
        site.Ref.End = end;
        RememberTemplate(site.Prefab, site.Id);
        site.File.Objects.Add((site.Start, end));
        if (site.Shown)
        {
            Objects.Add((site.Id, site.Prefab, site.Position, site.Rotation, Vector3.Zero));
        }
        if (site.Tracked && site.Prefab != LocationProxyPrefab)
        {
            Placed.Add(new PlacedObject(site.Prefab, 0, site.Position, site.Rotation, 0));
        }
    }

    /// <summary>Records an object read with its data: its extent, placement, links, creator and what the editor lists.</summary>
    /// <param name="site">The object.</param>
    /// <param name="f">The data its sections gave.</param>
    /// <param name="end">Where the object ends in the file.</param>
    private void RecordObject(ZdoSite site, ZdoFields f, long end)
    {
        site.Ref.End = end;
        site.File.Objects.Add((site.Start, end));
        if (site.Tracked && (site.Prefab != LocationProxyPrefab || f.Location != 0))
        {
            Placed.Add(new PlacedObject(site.Prefab, f.Location, site.Position, site.Rotation, f.TimeCreated));
        }
        bool terrain = TerrainZones.Count > 0 && TerrainZones[^1].Source is { } last && last.File == site.File && last.Start == site.Start;
        if (f.LinkInts != null && ObjectLinks.Read(f.Links, f.LinkInts, f.LinkVec3s, f.LinkQuats) is { } link)
        {
            Links[site.Id] = (link, site.Rotation);
        }
        site.Ref.IsPiece = f.Creator != 0;
        site.Ref.Creator = f.Creator;
        site.Ref.IsTerrain = terrain;
        if (!terrain)
        {
            RememberTemplate(site.Prefab, site.Id);
        }
        RecordPlacement(site, f, terrain);
        // Only this object's own terrain data can still lack its end (the last zone added, if it is this
        // object's): looking through every zone for each object made loading a large world slow.
        if (terrain && TerrainZones[^1].Source is { End: < 0 } src)
        {
            TerrainZones[^1].Source = src with { End = end };
        }
    }
}
