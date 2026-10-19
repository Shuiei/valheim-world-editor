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
    /// <summary>
    /// POST /objects: destroys objects (as their owner, so every peer drops them) and creates new ones from objects
    /// in the save file format (flags, position, prefab, rotation, data), like ZDO.Load but for a new object.
    /// Editors from 1.15.4 add each destroyed object's prefab and position: another mod may have made it again under
    /// a new ZDOID (ServersideQoL does, for build pieces, plants, fires and more), and then the object of that prefab
    /// at that place goes instead. Without this, undoing a placed piece found it "already gone" while every player
    /// kept seeing the new one.
    /// Editors from 1.40.0 then send objects to move (to a plugin listing "move" in its status): each one's ZDOID, prefab
    /// and place, its new place and turn, and itself at the new place in the save format. It moves under its own ZDOID,
    /// so what other mods know of it holds, unless a player has both places loaded: a player's game puts a standing
    /// object where it is once, as it comes into range, and would not show it moving; then it is made again at its new
    /// place from those bytes and destroyed where it was, as before.
    /// </summary>
    /// <returns>JSON: destroyed (refound included), refound, missing (already gone) and the created objects' ids as
    /// "user:id" strings, in the order they were sent; each move's object id and whether it moved in place; and what the
    /// mods are told.</returns>
    private (byte[] Json, AppliedChanges Changes) ApplyObjects(byte[] body)
    {
        ZPackage pkg = new(body);
        long session = ZDOMan.GetSessionID();
        var all = AllZdos();
        int before = all.Count;
        int destroyCount = pkg.ReadInt();
        var destroyIds = new ZDOID[destroyCount];
        for (int i = 0; i < destroyCount; i++)
        {
            destroyIds[i] = new ZDOID(pkg.ReadLong(), pkg.ReadUInt());
        }
        int createCount = pkg.ReadInt();
        // Every object to make is read first: a malformed one fails the call before anything changed,
        // so the editor, which records nothing then, can send it again without doubles.
        var creates = new Func<ZDO>[createCount];
        for (int i = 0; i < createCount; i++)
        {
            creates[i] = ReadSaveFormat(new ZPackage(pkg.ReadByteArray()));
        }
        (int Prefab, Vector3 Position)[] where = null;
        if (pkg.GetPos() < pkg.Size())
        {
            where = new (int, Vector3)[destroyCount];
            for (int i = 0; i < destroyCount; i++)
            {
                where[i] = (pkg.ReadInt(), pkg.ReadVector3());
            }
        }
        var moves = new List<(ZDOID Id, int Prefab, Vector3 From, Vector3 To, Vector3 Rotation, Func<ZDO> Fallback)>();
        if (pkg.GetPos() < pkg.Size())
        {
            int moveCount = pkg.ReadInt();
            for (int i = 0; i < moveCount; i++)
            {
                moves.Add((new ZDOID(pkg.ReadLong(), pkg.ReadUInt()), pkg.ReadInt(), pkg.ReadVector3(), pkg.ReadVector3(), pkg.ReadVector3(),
                    ReadSaveFormat(new ZPackage(pkg.ReadByteArray()))));
            }
        }
        int destroyed = 0, refound = 0;
        List<int> lost = [];
        List<AppliedObject> gone = [];
        for (int i = 0; i < destroyCount; i++)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(destroyIds[i]);
            if (zdo == null)
            {
                lost.Add(i);
                continue;
            }
            gone.Add(Applied(zdo));
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            destroyed++;
        }
        if (lost.Count > 0 && where != null)
        {
            refound = DestroyAtPlace([.. lost.Select(i => where[i])], session, gone);
        }
        int missing = lost.Count - refound;
        List<AppliedObject> made = [];
        var (movesJson, moved, inPlace, movedMissing) = Move(moves, session, gone, made);
        missing += movedMissing;
        StringBuilder ids = new();
        for (int i = 0; i < createCount; i++)
        {
            ZDO zdo = creates[i]();
            made.Add(Applied(zdo));
            if (i > 0)
            {
                ids.Append(',');
            }
            ids.Append('"').Append(zdo.m_uid.UserID.ToString(CultureInfo.InvariantCulture)).Append(':').Append(zdo.m_uid.ID.ToString(CultureInfo.InvariantCulture)).Append('"');
        }
        NoteDiscs(made);
        // The game drops a destroyed object on its next update, so the count after is the one before plus what was
        // created; the check a second later says whether the destroyed ones went.
        string movedText = moves.Count > 0 ? $", moved {inPlace} in place and {moves.Count - inPlace} by making it again (a player had both places loaded, or it was gone)" : "";
        Logger.LogInfo($"WorldEditorBridge: destroyed {destroyed + refound} object(s) ({refound} found again at their place under a new id, {missing} already gone), created {createCount}{movedText}; {before} -> {all.Count} object(s) known, {Peers()} peer(s)");
        if (gone.Count > 0)
        {
            _destroyChecks.Enqueue((Time.realtimeSinceStartup + 1f, gone, made));
        }
        return (Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroyed + refound},\"refound\":{refound},\"missing\":{missing},\"created\":[{ids}],\"moves\":[{movesJson}]}}"),
            new AppliedChanges(made, gone, [], [], moved));
    }
}
