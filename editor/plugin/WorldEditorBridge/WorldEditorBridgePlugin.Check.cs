using System.Collections.Generic;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Logs, a second after an apply destroyed objects, how many of them the game still has and how many other objects
    /// of the same prefab stand within 10 cm of their places (made again by something on the server, or standing
    /// there all along; the objects the same apply created are not counted), with the object and peer counts: what a
    /// "the pieces stayed for the players" report needs. One pass over the game's objects, each looked up by the metre
    /// cell of its place, so a large apply costs no more than a small one per object.
    /// </summary>
    /// <param name="gone">The objects the apply destroyed.</param>
    /// <param name="made">The objects the same apply created.</param>
    private void CheckDestroyed(List<AppliedObject> gone, List<AppliedObject> made)
    {
        if (ZDOMan.instance == null)
        {
            return;
        }
        var dict = AllZdos();
        HashSet<ZDOID> ids = [];
        foreach (AppliedObject o in made)
        {
            ids.Add(new ZDOID(o.User, o.Id));
        }
        // The destroyed places by prefab and metre cell: an object is checked against those of its own cell and the
        // eight around it only.
        Dictionary<(int Prefab, int X, int Z), List<Vector3>> cells = [];
        HashSet<int> prefabs = [];
        int still = 0;
        foreach (AppliedObject o in gone)
        {
            ZDOID id = new(o.User, o.Id);
            ids.Add(id);
            if (dict.ContainsKey(id))
            {
                still++;
            }
            Vector3 p = new(o.X, o.Y, o.Z);
            var key = (o.Prefab, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            if (!cells.TryGetValue(key, out var list))
            {
                cells[key] = list = [];
            }
            list.Add(p);
            prefabs.Add(o.Prefab);
        }
        bool Near(int prefab, Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.x), cz = Mathf.FloorToInt(p.z);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (cells.TryGetValue((prefab, cx + dx, cz + dz), out var list))
                    {
                        int at = list.FindIndex(w => (w - p).sqrMagnitude < 0.01f);
                        if (at >= 0)
                        {
                            list.RemoveAt(at);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        int nearby = 0;
        foreach (ZDO zdo in dict.Values)
        {
            if (!ids.Contains(zdo.m_uid) && prefabs.Contains(zdo.GetPrefab()) && Near(zdo.GetPrefab(), zdo.GetPosition()))
            {
                nearby++;
            }
        }
        Logger.LogInfo($"WorldEditorBridge: 1 s after the apply: {still} of {gone.Count} destroyed object(s) still exist, {nearby} other object(s) of their prefab within 10 cm of their place (made again, or there all along); {dict.Count} object(s), {Peers()} peer(s)");
    }
}
