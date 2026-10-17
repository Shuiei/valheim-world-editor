using System.Collections.Generic;
using System.Linq;
using System.Text;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>Prefab hash of players' tombstones, which a zone reset always keeps.</summary>
    private static readonly int TombstonePrefab = "Player_tombstone".GetStableHashCode();

    /// <summary>
    /// POST /zones/reset, like the editor's offline reset: every saved object of the zones goes (player-built pieces
    /// stay when asked, the ground edits only when asked, players' tombstones always stay), the zones are no longer
    /// marked as generated and their locations as not placed, so the game builds them again.
    /// </summary>
    /// <returns>JSON: zones reset, objects removed and locations to place again.</returns>
    private (byte[] Json, AppliedChanges Changes) ResetZones(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        Dictionary<Vector2s, (bool Keep, bool Ground)> zones = [];
        for (int i = 0; i < count; i++)
        {
            Vector2s key = new((short)pkg.ReadInt(), (short)pkg.ReadInt());
            zones[key] = (pkg.ReadBool(), pkg.ReadBool());
        }
        var dict = AllZdos();
        long session = ZDOMan.GetSessionID();
        int destroyed = 0;
        foreach (ZDO zdo in new List<ZDO>(dict.Values))
        {
            if (!zdo.Persistent || !zones.TryGetValue(ZoneSystem.GetZone(zdo.GetPosition()), out var r))
            {
                continue;
            }
            int prefab = zdo.GetPrefab();
            bool terrain = prefab == TerrainCompilerPrefab;
            if (prefab == TombstonePrefab || (terrain && !r.Ground) || (!terrain && r.Keep && zdo.GetLong(ZDOVars.s_creator) != 0))
            {
                continue;
            }
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            destroyed++;
        }
        ZoneSystem zs = ZoneSystem.instance;
        var generated = (HashSet<Vector2s>)GeneratedZones.GetValue(zs);
        int locations = 0;
        foreach (Vector2s zone in zones.Keys)
        {
            generated.Remove(zone);
            if (zs.m_locationInstances.TryGetValue(zone, out ZoneSystem.LocationInstance li) && li.m_placed)
            {
                li.m_placed = false;
                zs.m_locationInstances[zone] = li;
                locations++;
            }
        }
        Logger.LogInfo($"WorldEditorBridge: reset {zones.Count} zone(s): {destroyed} object(s) removed, {locations} location(s) to place again");
        return (Encoding.UTF8.GetBytes($"{{\"zones\":{zones.Count},\"destroyed\":{destroyed},\"locations\":{locations}}}"),
            new AppliedChanges([], [], [], [.. zones.Keys.Select(z => new Zone(z.x, z.y))]));
    }
}
