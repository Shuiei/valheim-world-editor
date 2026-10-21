using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>An object to move (<see cref="ApplyObjects"/>).</summary>
    /// <param name="id">Its ZDOID.</param>
    /// <param name="prefab">Its prefab's hash, to find it again under a new ZDOID.</param>
    /// <param name="from">Where it stands, to find it again.</param>
    /// <param name="to">Its new place.</param>
    /// <param name="rotation">Its new turn, Euler degrees.</param>
    /// <param name="fallback">Itself at its new place: made instead when it cannot move in place, and its data.</param>
    private sealed class ObjectMove(ZDOID id, int prefab, Vector3 from, Vector3 to, Vector3 rotation, SavedObject fallback)
    {
        /// <summary>Its ZDOID.</summary>
        public ZDOID Id { get; } = id;

        /// <summary>Its prefab's hash.</summary>
        public int Prefab { get; } = prefab;

        /// <summary>Where it stands.</summary>
        public Vector3 From { get; } = from;

        /// <summary>Its new place.</summary>
        public Vector3 To { get; } = to;

        /// <summary>Its new turn, Euler degrees.</summary>
        public Vector3 Rotation { get; } = rotation;

        /// <summary>Itself at its new place.</summary>
        public SavedObject Fallback { get; } = fallback;

        /// <summary>Whether it takes the data of <see cref="Fallback"/> in place too.</summary>
        public bool Data { get; set; }
    }

    /// <summary>
    /// Moves objects (<see cref="ApplyObjects"/>): in place under their own ZDOIDs (with their new data, for those that
    /// take it), or, where a player has both places loaded or the object is gone, made again at the new place (and
    /// destroyed where it was). One that stays where it is always changes in place.
    /// </summary>
    /// <param name="moves">The objects to move.</param>
    /// <param name="session">This game's session id, made the objects' owner so the change reaches every peer.</param>
    /// <param name="gone">Where objects destroyed are added, for the mods' listeners.</param>
    /// <param name="made">Where objects made are added, for the mods' listeners.</param>
    /// <returns>Each move's object id and whether it moved in place (JSON, in the order sent); the moves in place; how
    /// many; how many objects were gone (made again all the same).</returns>
    private static (string Json, List<AppliedMove> Moved, int InPlace, int Missing) Move(
        List<ObjectMove> moves, long session,
        List<AppliedObject> gone, List<AppliedObject> made)
    {
        var objects = new ZDO[moves.Count];
        List<int> lost = [];
        for (int i = 0; i < moves.Count; i++)
        {
            objects[i] = ZDOMan.instance.GetZDO(moves[i].Id);
            if (objects[i] == null)
            {
                lost.Add(i);
            }
        }
        // Made again under a new ZDOID by another mod (as for destroys): the object of that prefab at that place.
        if (lost.Count > 0)
        {
            ZDO[] found = FindAtPlace([.. lost.Select(i => (moves[i].Prefab, moves[i].From))]);
            for (int k = 0; k < lost.Count; k++)
            {
                objects[lost[k]] = found[k];
            }
        }
        StringBuilder json = new();
        List<AppliedMove> moved = [];
        int missing = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            ZDO zdo = objects[i];
            ObjectMove move = moves[i];
            Vector3 to = move.To;
            ZDO result;
            bool inPlace = zdo != null && ((zdo.GetPosition() - to).sqrMagnitude < 1e-6f || !LoadedAtBoth(zdo.GetPosition(), to, zdo.Distant));
            if (inPlace)
            {
                AppliedObject from = Applied(zdo);
                zdo.SetOwner(session);
                zdo.SetPosition(to);
                zdo.SetRotation(Quaternion.Euler(move.Rotation));
                if (move.Data)
                {
                    move.Fallback.PutOn(zdo);
                }
                // Players' games far from both places drop the object or show it at its new place once they have
                // its new data: sent to every peer now, not only to those near where it stands.
                ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                moved.Add(new AppliedMove(from, Applied(zdo)));
                result = zdo;
            }
            else
            {
                result = move.Fallback.Make();
                made.Add(Applied(result));
                if (zdo != null)
                {
                    gone.Add(Applied(zdo));
                    zdo.SetOwner(session);
                    ZDOMan.instance.DestroyZDO(zdo);
                }
                else
                {
                    missing++;
                }
            }
            if (i > 0)
            {
                json.Append(',');
            }
            json.Append("{\"id\":\"").Append(result.m_uid.UserID.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(result.m_uid.ID.ToString(CultureInfo.InvariantCulture)).Append("\",\"inPlace\":").Append(inPlace ? "true" : "false").Append('}');
        }
        return (json.ToString(), moved, moved.Count, missing);
    }

    /// <summary>
    /// Whether a player's game (or this game's own, when it is not a dedicated server) has both places loaded: it
    /// would keep showing an object moved between them where it stood, since a game puts a standing object where it is
    /// only as it comes into range.
    /// </summary>
    /// <param name="from">Where the object stands.</param>
    /// <param name="to">Where it goes.</param>
    /// <param name="distant">The object is seen from afar (the game loads such ones in a wider area).</param>
    private static bool LoadedAtBoth(Vector3 from, Vector3 to, bool distant)
    {
        // The zones round a player whose objects its game shows (the simulation distance the server gives every
        // player): a square as wide, a little more than the game's own round area.
        SimulationDistance range = ZNet.instance.GetSyncedSimulationDistance();
        int area = distant ? range.TotalSimulationDistance : range.NearSimulationDistance;
        Vector2s a = ZoneSystem.GetZone(from), b = ZoneSystem.GetZone(to);
        bool Near(Vector2s zone, Vector2s at) => Math.Abs(zone.x - at.x) <= area && Math.Abs(zone.y - at.y) <= area;
        bool Both(Vector3 at)
        {
            Vector2s z = ZoneSystem.GetZone(at);
            return Near(a, z) && Near(b, z);
        }
        foreach (ZNetPeer peer in ZNet.instance.GetPeers())
        {
            if (Both(peer.GetRefPos()))
            {
                return true;
            }
        }
        return !ZNet.instance.IsDedicated() && Both(ZNet.instance.GetReferencePosition());
    }
}
