using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>Prefab hash of _TerrainCompiler, the per-zone object holding the zone's ground edits (TCData).</summary>
    private static readonly int TerrainCompilerPrefab = "_TerrainCompiler".GetStableHashCode();

    /// <summary>
    /// POST /terrain: sets the terrain data of each zone's terrain compiler object (creating it when the zone has
    /// none). The game syncs the object to everyone, and TerrainComp reloads when its data changes.
    /// </summary>
    /// <returns>JSON: how many zones were applied and how many compilers were made; and what the mods are told.</returns>
    private (byte[] Json, AppliedChanges Changes) ApplyTerrain(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        var dict = AllZdos();
        Dictionary<Vector2s, ZDO> compilers = [];
        foreach (ZDO zdo in dict.Values)
        {
            if (zdo.GetPrefab() == TerrainCompilerPrefab)
            {
                Vector2s zone = ZoneSystem.GetZone(zdo.GetPosition());
                // With duplicates (the game cleans them up itself), keep the one with data.
                if (!compilers.TryGetValue(zone, out ZDO other) || other.GetByteArray(ZDOVars.s_TCData) == null)
                {
                    compilers[zone] = zdo;
                }
            }
        }
        int applied = 0, created = 0;
        List<Zone> zones = [];
        for (int i = 0; i < count; i++)
        {
            int zx = pkg.ReadInt(), zz = pkg.ReadInt();
            byte[] data = pkg.ReadByteArray();
            zones.Add(new Zone(zx, zz));
            Vector2s key = new((short)zx, (short)zz);
            if (!compilers.TryGetValue(key, out ZDO tc))
            {
                tc = CreateTerrainCompiler(key);
                compilers[key] = tc;
                created++;
            }
            tc.Set(ZDOVars.s_TCData, data);
            applied++;
        }
        Logger.LogInfo($"WorldEditorBridge: applied terrain to {applied} zone(s) ({created} new)");
        return (Encoding.UTF8.GetBytes($"{{\"applied\":{applied},\"created\":{created}}}"), new AppliedChanges([], [], zones, []));
    }

    /// <summary>Like ZNetView.Awake for the _TerrainCompiler prefab, at the zone centre.</summary>
    private static ZDO CreateTerrainCompiler(Vector2s zone)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(TerrainCompilerPrefab);
#pragma warning disable IDE0031 // Unity's == null also catches destroyed objects; ?. does not
        ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
#pragma warning restore IDE0031
        ZDO zdo = ZDOMan.instance.CreateNewZDO(ZoneSystem.GetZonePos(zone), TerrainCompilerPrefab);
        zdo.Persistent = view == null || view.m_persistent;
        zdo.Type = view != null ? view.m_type : ZDO.ObjectType.Terrain;
        zdo.Distant = view != null && view.m_distant;
        zdo.SetPrefab(TerrainCompilerPrefab);
        return zdo;
    }
}
