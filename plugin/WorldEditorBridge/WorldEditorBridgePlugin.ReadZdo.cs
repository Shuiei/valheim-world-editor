using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// An object as the save writes it (ZDO.Save), read whole into what makes it: nothing is made in the game until
    /// that is called.
    /// </summary>
    /// <returns>Creates the object (main thread only), relinking it when it had a link, and returns it.</returns>
    private static Func<ZDO> ReadSaveFormat(ZPackage p)
    {
        int flags = p.ReadUShort();
        Vector3 pos;
        if ((flags & 0x2000) != 0)
        {
            Vector2s v = p.ReadVector2s();
            pos = new Vector3(v.x, 0f, v.y);
        }
        else
        {
            pos = p.ReadVector3();
        }
        int prefab = p.ReadInt();
        Vector3 euler = (flags & 0x1000) != 0 ? p.ReadSmallRotation() : Vector3.zero;
        List<Action<ZDO>> values = [];
        var linkType = ZDOExtraData.ConnectionType.None;
        int linkHash = 0;
        if ((flags & 0xFF) != 0)
        {
            if ((flags & 0x1) != 0)
            {
                linkType = (ZDOExtraData.ConnectionType)p.ReadByte();
                linkHash = p.ReadInt();
            }
            void Each(int flag, Func<int, Action<ZDO>> read)
            {
                if ((flags & flag) == 0)
                {
                    return;
                }
                int n = p.ReadNumItems();
                for (int k = 0; k < n; k++)
                {
                    values.Add(read(p.ReadInt()));
                }
            }
            Each(0x2, key => { float v = p.ReadSingle(); return z => z.Set(key, v); });
            Each(0x4, key => { Vector3 v = p.ReadVector3(); return z => z.Set(key, v); });
            Each(0x8, key => { Quaternion v = p.ReadQuaternion(); return z => z.Set(key, v); });
            Each(0x10, key => { int v = p.ReadInt(); return z => z.Set(key, v); });
            Each(0x20, key => { long v = p.ReadLong(); return z => z.Set(key, v); });
            Each(0x40, key => { string v = p.ReadString(); return z => z.Set(key, v); });
            Each(0x80, key => { byte[] v = p.ReadByteArray(); return z => z.Set(key, v); });
        }
        return () =>
        {
            ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, prefab);
            zdo.Persistent = (flags & 0x100) != 0;
            zdo.Distant = (flags & 0x200) != 0;
            zdo.Type = (ZDO.ObjectType)((flags >> 10) & 3);
            zdo.SetPrefab(prefab);
            zdo.SetRotation(Quaternion.Euler(euler));
            foreach (Action<ZDO> set in values)
            {
                set(zdo);
            }
            NoteDisc(zdo);
            if (linkType != ZDOExtraData.ConnectionType.None)
            {
                Relink(zdo, linkType, linkHash);
            }
            return zdo;
        };
    }
}
