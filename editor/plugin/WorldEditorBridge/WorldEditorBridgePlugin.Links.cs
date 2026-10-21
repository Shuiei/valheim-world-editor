using System.Collections.Generic;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Links between objects (a spawner and what it spawned, two portals...), as the save writes them: a hash on the
    /// object holding the link and the same hash, marked Target, on its target. The hash is kept with both objects'
    /// ids (main thread only), so an object made again from the snapshot (an undone delete) gets its link back, and
    /// links to it follow it to its new id.
    /// </summary>
    private static readonly Dictionary<int, (ZDOExtraData.ConnectionType Type, ZDOID Source, ZDOID Target)> Links = [];

    /// <summary>The hash of the link <paramref name="source"/> holds, from its id; odd, so never 0.</summary>
    private static int LinkHash(ZDOID source) => unchecked((int)(source.UserID * 486187739L) ^ (int)source.ID * 16777619) | 1;

    /// <summary>
    /// The link each object carries in the save (type and hash), recording every link in <c>Links</c> on the way.
    /// An object both holding a link and the target of one carries the one it holds. Main thread only.
    /// </summary>
    private static Dictionary<ZDOID, (ZDOExtraData.ConnectionType Type, int Hash)> SnapshotLinks()
    {
        var of = new Dictionary<ZDOID, (ZDOExtraData.ConnectionType, int)>();
        var targets = new List<(ZDOID, ZDOExtraData.ConnectionType, int)>();
        foreach (ZDOID source in ZDOExtraData.GetAllConnectionZDOIDs())
        {
            ZDOConnection c = ZDOExtraData.GetConnection(source);
            if (c == null || c.m_type == ZDOExtraData.ConnectionType.None)
            {
                continue;
            }
            int hash = LinkHash(source);
            Links[hash] = (c.m_type, source, c.m_target);
            of[source] = (c.m_type, hash);
            if (c.m_target != ZDOID.None)
            {
                targets.Add((c.m_target, c.m_type | ZDOExtraData.ConnectionType.Target, hash));
            }
        }
        // One link per object in the save: the one it holds before the one it is the target of.
        foreach (var (target, type, hash) in targets)
        {
            if (!of.ContainsKey(target))
            {
                of[target] = (type, hash);
            }
        }
        return of;
    }

    /// <summary>
    /// An object made again from the snapshot (its old link: type and hash) in place of one that is gone (an undone
    /// delete, a move): its link back, and the links to its old id now to it. A copy of an object still there (a
    /// paste) takes no link: it would take the original's.
    /// </summary>
    private static void Relink(ZDO made, ZDOExtraData.ConnectionType type, int hash)
    {
        if (!Links.TryGetValue(hash, out var link))
        {
            return;
        }
        bool holder = (type & ZDOExtraData.ConnectionType.Target) == 0;
        ZDOID old = holder ? link.Source : link.Target;
        if (ZDOMan.instance.GetZDO(old) != null)
        {
            return;
        }
        if (holder)
        {
            if (link.Target == ZDOID.None || ZDOMan.instance.GetZDO(link.Target) != null)
            {
                made.SetConnection(link.Type, link.Target);
            }
            Links[hash] = (link.Type, made.m_uid, link.Target);
        }
        else
        {
            Links[hash] = (link.Type, link.Source, made.m_uid);
        }
        foreach (ZDOID source in ZDOExtraData.GetAllConnectionZDOIDs())
        {
            ZDOConnection c = ZDOExtraData.GetConnection(source);
            if (c != null && c.m_target == old && ZDOMan.instance.GetZDO(source) is { } other)
            {
                other.UpdateConnection(c.m_type, made.m_uid);
            }
        }
    }
}
