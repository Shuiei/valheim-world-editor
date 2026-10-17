using System.Collections.Generic;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// The objects of these prefabs at these places (within 10 cm: a re-made object keeps its position), each at most
    /// once; null where none is.
    /// </summary>
    /// <param name="wanted">Prefab hash and position of each object to find.</param>
    private static ZDO[] FindAtPlace(List<(int Prefab, Vector3 Position)> wanted)
    {
        var found = new ZDO[wanted.Count];
        Dictionary<int, List<int>> byPrefab = [];
        for (int i = 0; i < wanted.Count; i++)
        {
            if (!byPrefab.TryGetValue(wanted[i].Prefab, out var list))
            {
                byPrefab[wanted[i].Prefab] = list = [];
            }
            list.Add(i);
        }
        var dict = AllZdos();
        foreach (ZDO zdo in dict.Values)
        {
            if (!byPrefab.TryGetValue(zdo.GetPrefab(), out var list) || list.Count == 0)
            {
                continue;
            }
            Vector3 p = zdo.GetPosition();
            int at = list.FindIndex(i => (wanted[i].Position - p).sqrMagnitude < 0.01f);
            if (at >= 0)
            {
                found[list[at]] = zdo;
                list.RemoveAt(at);
            }
        }
        return found;
    }

    /// <summary>How many peers (players' games) are connected to this game.</summary>
    private static int Peers() => ZNet.instance?.GetPeers().Count ?? 0;

    /// <summary>
    /// Destroys the objects of these prefabs at these places (within 10 cm: a re-made object keeps its position),
    /// each at most once.
    /// </summary>
    /// <param name="wanted">Prefab hash and position of each object to find.</param>
    /// <param name="session">This game's session id, made the objects' owner so the destroy reaches every peer.</param>
    /// <param name="gone">Where the objects destroyed are added, for the mods' listeners.</param>
    /// <returns>How many were found and destroyed.</returns>
    private static int DestroyAtPlace(List<(int Prefab, Vector3 Position)> wanted, long session, List<AppliedObject> gone)
    {
        Dictionary<int, List<Vector3>> byPrefab = [];
        foreach (var (prefab, pos) in wanted)
        {
            if (!byPrefab.TryGetValue(prefab, out var list))
            {
                byPrefab[prefab] = list = [];
            }
            list.Add(pos);
        }
        var dict = AllZdos();
        int found = 0;
        foreach (ZDO zdo in new List<ZDO>(dict.Values))
        {
            if (!byPrefab.TryGetValue(zdo.GetPrefab(), out var places) || places.Count == 0)
            {
                continue;
            }
            Vector3 p = zdo.GetPosition();
            int at = places.FindIndex(w => (w - p).sqrMagnitude < 0.01f);
            if (at < 0)
            {
                continue;
            }
            places.RemoveAt(at);
            gone.Add(Applied(zdo));
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            found++;
        }
        return found;
    }
}
