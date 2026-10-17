using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    // ---- What changes in the zones the editor shows (it asks every few seconds: only those zones are
    // read, from the game's own per-zone lists, never the whole world).

    /// <summary>ZDOMan's private per-zone object lists, indexed by ZoneSystem.SectorToIndex.</summary>
    private static readonly FieldInfo ObjectsBySector = typeof(ZDOMan).GetField("m_objectsBySector", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Portals are kept apart from the zone lists, in their own (by zone too).</summary>
    private static readonly FieldInfo PortalObjects = typeof(ZDOMan).GetField("m_portalObjects", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Zones asked for: int count, then int x, int z each (outside the world: left out).</summary>
    private static List<Vector2s> ReadZones(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        List<Vector2s> zones = new(count);
        for (int i = 0; i < count; i++)
        {
            int x = pkg.ReadInt(), z = pkg.ReadInt();
            if (x >= -256 && x < 256 && z >= -256 && z < 256)
            {
                zones.Add(new Vector2s(x, z));
            }
        }
        return zones;
    }

    /// <summary>
    /// The saved objects of a zone (persistent ZDOs), as the game keeps them by zone: its list, and its portals
    /// (kept apart).
    /// </summary>
    private static IEnumerable<ZDO> ZoneZdos(Vector2s zone)
    {
        var bySector = (List<ZDO>[])ObjectsBySector.GetValue(ZDOMan.instance);
        var portals = (Dictionary<ZoneSystem.SectorIndex, List<ZDO>>)PortalObjects.GetValue(ZDOMan.instance);
        ZoneSystem.SectorIndex index = ZoneSystem.SectorToIndex(zone);
        IEnumerable<ZDO> list = index.Sector < bySector.Length && bySector[index.Sector] is { } inZone ? inZone : Enumerable.Empty<ZDO>();
        if (portals != null && portals.TryGetValue(index, out List<ZDO> zonePortals))
        {
            list = list.Concat(zonePortals);
        }
        foreach (ZDO zdo in list)
        {
            if (zdo.Persistent)
            {
                yield return zdo;
            }
        }
    }

    /// <summary>
    /// POST /zone: the saved objects of the zones asked for, in the snapshot's format: int count, then for each its
    /// ZDOID (long, uint) and its bytes (int length, bytes). The HTTP thread gzips it.
    /// </summary>
    private static byte[] ZoneObjects(byte[] body)
    {
        var links = SnapshotLinks();
        List<(ZDOID Id, byte[] Bytes)> found = [];
        foreach (Vector2s zone in ReadZones(body))
        {
            foreach (ZDO zdo in ZoneZdos(zone))
            {
                ZPackage one = new();
                WriteZdo(one, zdo, links);
                found.Add((zdo.m_uid, one.GetArray()));
            }
        }
        ZPackage pkg = new();
        pkg.Write(found.Count);
        foreach (var (id, bytes) in found)
        {
            pkg.Write(id.UserID);
            pkg.Write(id.ID);
            pkg.Write(bytes);
        }
        return pkg.GetArray();
    }
}
