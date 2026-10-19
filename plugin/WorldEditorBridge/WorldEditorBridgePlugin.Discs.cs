using System.Collections.Generic;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>The game's location proxy prefab: what the editor's ground discs (No limit ground) are.</summary>
    private static readonly int LocationProxyPrefab = "LocationProxy".GetStableHashCode();

    /// <summary>The dev location a ground disc spawns (it only levels the ground).</summary>
    private static readonly int DiscLocation = "DevGround1".GetStableHashCode();

    /// <summary>How often (seconds) the discs are looked at.</summary>
    private const float DiscsEvery = 5f;

    /// <summary>The ground discs of the loaded world, found once per world and kept up to date by the bridge's own creates.</summary>
    private static readonly List<ZDOID> Discs = [];

    /// <summary>The ZDOMan the discs were found in: a new one is a new world.</summary>
    private ZDOMan _discsOf;

    /// <summary>When the discs are next looked at (Time.realtimeSinceStartup).</summary>
    private float _discsDue;

    /// <summary>Whether an object is one of the editor's ground discs.</summary>
    private static bool IsDisc(ZDO zdo) => zdo.GetPrefab() == LocationProxyPrefab && zdo.GetInt(ZDOVars.s_location) == DiscLocation;

    /// <summary>Remembers a disc the bridge just made, so that <see cref="KeepDiscsFirst"/> watches it too.</summary>
    private static void NoteDisc(ZDO zdo)
    {
        if (IsDisc(zdo))
        {
            Discs.Add(zdo.m_uid);
        }
    }

    /// <summary>
    /// Keeps every ground disc an object of the Terrain kind, so that a player's game spawns the discs before the
    /// building pieces and trees around them and the ground is finished before anything stands on it. The game makes
    /// at most a hundredth of a zone's objects per frame, the Terrain kind first and then the nearest first; a disc
    /// saved as a Solid object (the proxy prefab's own kind) comes in among the thousands of pieces, and every disc
    /// rebuilds the zone's ground when it spawns. The game puts a disc back to Solid when the player who owns it
    /// spawns it, so the discs are looked at every few seconds and put back to Terrain, which reaches every player's
    /// game before their next visit. Discs saved by older editor versions are changed at the first look.
    /// </summary>
    private void KeepDiscsFirst()
    {
        if (Time.realtimeSinceStartup < _discsDue || ZNet.instance == null || ZDOMan.instance == null || ZNet.m_loadError)
        {
            return;
        }
        _discsDue = Time.realtimeSinceStartup + DiscsEvery;
        if (!ReferenceEquals(_discsOf, ZDOMan.instance))
        {
            // The ZDOMan is made before the world is loaded into it (ZNet.Awake, then its Start): until it holds
            // something, look again later.
            var all = AllZdos();
            if (all.Count == 0)
            {
                return;
            }
            Discs.Clear();
            foreach (ZDO zdo in all.Values)
            {
                NoteDisc(zdo);
            }
            _discsOf = ZDOMan.instance;
            Logger.LogInfo($"WorldEditorBridge: {Discs.Count} ground disc(s) in the world, kept first in line");
        }
        int changed = 0;
        for (int i = Discs.Count - 1; i >= 0; i--)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(Discs[i]);
            if (zdo == null)
            {
                Discs.RemoveAt(i);
            }
            else if (zdo.Type != ZDO.ObjectType.Terrain)
            {
                zdo.SetType(ZDO.ObjectType.Terrain);
                changed++;
            }
        }
        if (changed > 0)
        {
            Logger.LogInfo($"WorldEditorBridge: {changed} ground disc(s) put back first in line");
        }
    }
}
