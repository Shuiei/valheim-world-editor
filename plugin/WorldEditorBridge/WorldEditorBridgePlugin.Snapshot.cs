using System.Collections.Generic;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// GET /snapshot. Layout (all little endian, strings as .NET BinaryWriter strings):
    /// <code>
    ///   "VWEB" int version, string world, string seedName, int seed, int worldGenVersion, double netTime
    ///   int length + ZoneSystem package (as ZoneSystem.Save writes it, uncompressed)
    ///   short 41, int count, count objects (ZDO.Save chunk format), then count x (long user, uint id)
    /// </code>
    /// Only persistent objects are written; the HTTP thread gzips it.
    /// </summary>
    private byte[] Snapshot()
    {
        World w = ZNet.World;
        ZPackage pkg = new();
        pkg.Write((byte)'V'); pkg.Write((byte)'W'); pkg.Write((byte)'E'); pkg.Write((byte)'B');
        pkg.Write(SnapshotVersion);
        pkg.Write(w.m_name);
        pkg.Write(w.m_seedName);
        pkg.Write(w.m_seed);
        pkg.Write(w.m_worldGenVersion);
        pkg.Write(ZNet.instance.GetTimeSeconds());
        byte[] zones = ZoneSystemData();
        pkg.Write(zones.Length);
        foreach (byte b in zones)
        {
            pkg.Write(b);
        }
        var dict = AllZdos();
        List<ZDO> zdos = new(dict.Count);
        foreach (ZDO zdo in dict.Values)
        {
            if (zdo.Persistent)
            {
                zdos.Add(zdo);
            }
        }
        var links = SnapshotLinks();
        pkg.Write((short)41);
        pkg.Write(zdos.Count);
        foreach (ZDO zdo in zdos)
        {
            WriteZdo(pkg, zdo, links);
        }
        foreach (ZDO zdo in zdos)
        {
            pkg.Write(zdo.m_uid.UserID);
            pkg.Write(zdo.m_uid.ID);
        }
        return pkg.GetArray();
    }

    /// <summary>Same layout as ZoneSystem.Save: generated zones, location version, global keys, locations.</summary>
    private static byte[] ZoneSystemData()
    {
        ZoneSystem zs = ZoneSystem.instance;
        ZPackage p = new();
        var generated = (HashSet<Vector2s>)GeneratedZones.GetValue(zs);
        p.Write(generated.Count);
        foreach (Vector2s z in generated)
        {
            p.Write(z);
        }
        p.Write(zs.m_locationVersion);
        var keys = (HashSet<string>)GlobalKeys.GetValue(zs);
        p.Write(keys.Count);
        foreach (string k in keys)
        {
            p.Write(k);
        }
        p.Write(zs.LocationsGenerated);
        p.Write(zs.m_locationInstances.Count);
        foreach (ZoneSystem.LocationInstance li in zs.m_locationInstances.Values)
        {
            p.Write(li.m_location?.m_prefabName.GetStableHashCode() ?? 0);
            p.Write(li.m_position.x);
            p.Write(li.m_position.y);
            p.Write(li.m_position.z);
            p.Write(li.m_placed);
        }
        return p.GetArray();
    }
}
