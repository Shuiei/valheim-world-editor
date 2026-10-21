using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Mirrors ZDO.Save (world version 41), reading the live data instead of the save-time copy.
    /// </summary>
    /// <param name="pkg">Where the object is written.</param>
    /// <param name="zdo">The object.</param>
    /// <param name="links">Each linked object's link, from <c>SnapshotLinks</c>.</param>
    private static void WriteZdo(ZPackage pkg, ZDO zdo, Dictionary<ZDOID, (ZDOExtraData.ConnectionType Type, int Hash)> links)
    {
        ZDOExtraData.GetData(zdo.m_uid, out var floats, out var vec3s, out var quats, out var ints, out var longs, out var strings, out var bytes, out ZDOConnection _);
        bool hasConn = links.TryGetValue(zdo.m_uid, out var conn);
        Vector3 euler = ZdoRotation != null ? (Vector3)ZdoRotation.GetValue(zdo) : zdo.GetRotation().eulerAngles;
        // Same test as ZDO.Save (Vector3.CloseToZero).
        bool rotated = Mathf.Abs(euler.x) > 1E-05f || Mathf.Abs(euler.y) > 1E-05f || Mathf.Abs(euler.z) > 1E-05f;
        int flags = Bit(hasConn, 0x1) | Bit(floats.Count > 0, 0x2) | Bit(vec3s.Count > 0, 0x4) | Bit(quats.Count > 0, 0x8)
            | Bit(ints.Count > 0, 0x10) | Bit(longs.Count > 0, 0x20) | Bit(strings.Count > 0, 0x40) | Bit(bytes.Count > 0, 0x80)
            | Bit(zdo.Persistent, 0x100) | Bit(zdo.Distant, 0x200) | (((int)zdo.Type & 3) << 10) | Bit(rotated, 0x1000);
        pkg.Write((ushort)flags);
        pkg.Write(zdo.GetPosition());
        pkg.Write(zdo.GetPrefab());
        if (rotated)
        {
            pkg.WriteSmallRotation(euler);
        }
        if ((flags & 0xFF) == 0)
        {
            return;
        }
        if (hasConn)
        {
            pkg.Write((byte)conn.Type);
            pkg.Write(conn.Hash);
        }
        Section(pkg, floats, pkg.Write);
        Section(pkg, vec3s, pkg.Write);
        Section(pkg, quats, pkg.Write);
        Section(pkg, ints, pkg.Write);
        Section(pkg, longs, pkg.Write);
        Section(pkg, strings, pkg.Write);
        Section(pkg, bytes, pkg.Write);
    }

    /// <summary>The save flag <paramref name="bit"/> when <paramref name="on"/>, else nothing.</summary>
    private static int Bit(bool on, int bit)
    {
        return on ? bit : 0;
    }

    /// <summary>One typed data section of ZDO.Save: item count, then key and value of each; nothing when empty.</summary>
    private static void Section<T>(ZPackage pkg, List<KeyValuePair<int, T>> items, Action<T> write)
    {
        if (items.Count == 0)
        {
            return;
        }
        pkg.WriteNumItems(items.Count);
        foreach (var kv in items)
        {
            pkg.Write(kv.Key);
            write(kv.Value);
        }
    }
}
