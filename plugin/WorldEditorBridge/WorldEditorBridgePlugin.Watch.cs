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
    /// Kinds whose own changes <c>Watch</c> leaves out. Kinds that move by themselves (creatures, ships, carts, items:
    /// they sync their transform): their moves are not changes to the world worth reading again. And kinds that
    /// rewrite their data on their own every few seconds while a player is near (fires and torches their fuel time,
    /// smelters, cooking stations, beehives, fermenters, sap collectors): that is not a change worth reading their
    /// zone again for (they would be read at every look).
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
    /// POST /watch: one number per zone that changes when an object there is made, removed or changed (its data or
    /// place; a mover or a ticking kind only when made or removed): the editor reads a zone again when it changes,
    /// and then sees every object as it is.
    /// </summary>
    /// <returns>JSON <c>{"zones":[[x,z,"number"],...]}</c>, the number as a string (it is a 64-bit unsigned).</returns>
    private static byte[] Watch(byte[] body)
    {
        StringBuilder sb = new("{\"zones\":[");
        bool first = true;
        foreach (Vector2s zone in ReadZones(body))
        {
            ulong digest = 0;
            foreach (ZDO zdo in ZoneZdos(zone))
            {
                int prefab = zdo.GetPrefab();
                ulong h = unchecked((ulong)zdo.m_uid.UserID * 0x9E3779B97F4A7C15UL ^ zdo.m_uid.ID * 0xC2B2AE3D27D4EB4FUL ^ (uint)prefab * 0x165667B19E3779F9UL);
                h ^= Moves(prefab) ? 0 : unchecked(zdo.DataRevision * 0x27D4EB2F165667C5UL + 1);
                // A sum: the same whatever order the game keeps the zone's list in.
                digest = unchecked(digest + (h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL);
            }
            sb.Append(first ? "" : ",").Append('[').Append(zone.x).Append(',').Append(zone.y).Append(",\"").Append(digest.ToString(CultureInfo.InvariantCulture)).Append("\"]");
            first = false;
        }
        return Encoding.UTF8.GetBytes(sb.Append("]}").ToString());
    }
}
