using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>Cache of <c>Moves</c>, by prefab hash.</summary>
    private static readonly Dictionary<int, bool> Movers = [];

    /// <summary>
    /// Kinds that change on their own: kinds that move by themselves (creatures, ships, carts, items: they sync their
    /// transform), and kinds that rewrite their data every few seconds while a player is near (fires and torches their
    /// fuel time, smelters, cooking stations, beehives, fermenters, sap collectors). <c>Watch</c> counts their changes
    /// in a number of their own, and /zone/movers reads them alone: their zone is not read whole at every move or tick.
    /// </summary>
    /// <returns>True for those kinds; false for others and for prefabs the game does not know.</returns>
    private static bool Moves(int prefab)
    {
        if (!Movers.TryGetValue(prefab, out bool moves))
        {
#pragma warning disable IDE0031 // Unity's == null also catches destroyed objects; ?. does not
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
#pragma warning restore IDE0031
            Movers[prefab] = moves = go != null && (go.GetComponent<ZSyncTransform>() != null || go.GetComponent<Fireplace>() != null
                || go.GetComponent<Smelter>() != null || go.GetComponent<CookingStation>() != null || go.GetComponent<Beehive>() != null
                || go.GetComponent<Fermenter>() != null || go.GetComponent<SapCollector>() != null);
        }
        return moves;
    }

    /// <summary>
    /// POST /watch: two numbers per zone. The first changes when an object there is made or removed, or one that stays
    /// put changes (its data or place): the editor reads the zone whole again. The second changes when a kind that
    /// changes on its own (<see cref="Moves"/>) moves or changes its data: the editor reads those objects alone
    /// (/zone/movers). The first number is what every plugin before 1.57.3 gave (movers counted when made or removed
    /// only): editors from before 1.57.3 read it alone, as before.
    /// </summary>
    /// <returns>JSON <c>{"zones":[[x,z,"number","number"],...]}</c>, the numbers as strings (they are 64-bit unsigned).</returns>
    private static byte[] Watch(byte[] body)
    {
        StringBuilder sb = new("{\"zones\":[");
        bool first = true;
        foreach (Vector2s zone in ReadZones(body))
        {
            ulong still = 0, moving = 0;
            foreach (ZDO zdo in ZoneZdos(zone))
            {
                int prefab = zdo.GetPrefab();
                ulong h = unchecked((ulong)zdo.m_uid.UserID * 0x9E3779B97F4A7C15UL ^ zdo.m_uid.ID * 0xC2B2AE3D27D4EB4FUL ^ (uint)prefab * 0x165667B19E3779F9UL);
                ulong revision = unchecked(zdo.DataRevision * 0x27D4EB2F165667C5UL + 1);
                // Sums: the same whatever order the game keeps the zone's list in.
                if (Moves(prefab))
                {
                    still = unchecked(still + Mix(h));
                    moving = unchecked(moving + Mix(h ^ revision));
                }
                else
                {
                    still = unchecked(still + Mix(h ^ revision));
                }
            }
            sb.Append(first ? "" : ",").Append('[').Append(zone.x).Append(',').Append(zone.y)
                .Append(",\"").Append(still.ToString(CultureInfo.InvariantCulture)).Append("\",\"").Append(moving.ToString(CultureInfo.InvariantCulture)).Append("\"]");
            first = false;
        }
        return Encoding.UTF8.GetBytes(sb.Append("]}").ToString());
    }

    /// <summary>One object's share of a zone's number: its hash stirred, so close hashes do not sum alike.</summary>
    private static ulong Mix(ulong h) => unchecked((h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL);
}
