using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// An object as the save writes it (ZDO.Save), read whole: nothing changes in the game until it is made
    /// (<see cref="SavedObject.Make"/>) or its data put on an object (<see cref="SavedObject.PutOn"/>).
    /// </summary>
    private static SavedObject ReadSaveFormat(ZPackage p)
    {
        var o = new SavedObject { Flags = p.ReadUShort() };
        if ((o.Flags & 0x2000) != 0)
        {
            Vector2s v = p.ReadVector2s();
            o.Position = new Vector3(v.x, 0f, v.y);
        }
        else
        {
            o.Position = p.ReadVector3();
        }
        o.Prefab = p.ReadInt();
        o.Euler = (o.Flags & 0x1000) != 0 ? p.ReadSmallRotation() : Vector3.zero;
        if ((o.Flags & 0xFF) == 0)
        {
            return o;
        }
        if ((o.Flags & 0x1) != 0)
        {
            o.LinkType = (ZDOExtraData.ConnectionType)p.ReadByte();
            o.LinkHash = p.ReadInt();
        }
        void Each<T>(int flag, List<KeyValuePair<int, T>> into, Func<T> read)
        {
            if ((o.Flags & flag) == 0)
            {
                return;
            }
            int n = p.ReadNumItems();
            for (int k = 0; k < n; k++)
            {
                int key = p.ReadInt();
                into.Add(new KeyValuePair<int, T>(key, read()));
            }
        }
        Each(0x2, o.Floats, p.ReadSingle);
        Each(0x4, o.Vec3s, p.ReadVector3);
        Each(0x8, o.Quats, p.ReadQuaternion);
        Each(0x10, o.Ints, p.ReadInt);
        Each(0x20, o.Longs, p.ReadLong);
        Each(0x40, o.Strings, p.ReadString);
        Each(0x80, o.Bytes, p.ReadByteArray);
        return o;
    }

    /// <summary>An object read from the save format (<see cref="ReadSaveFormat"/>).</summary>
    private sealed class SavedObject
    {
        /// <summary>Its save flags (persistent, distant, type, which values follow).</summary>
        public int Flags { get; set; }

        /// <summary>Where it stands.</summary>
        public Vector3 Position { get; set; }

        /// <summary>Its prefab's hash.</summary>
        public int Prefab { get; set; }

        /// <summary>Its turn, Euler degrees.</summary>
        public Vector3 Euler { get; set; }

        /// <summary>Its link to another object, as the save keeps it (none, or a type and the other's hash).</summary>
        public ZDOExtraData.ConnectionType LinkType { get; set; } = ZDOExtraData.ConnectionType.None;

        /// <summary>The linked object's hash in the save.</summary>
        public int LinkHash { get; set; }

        /// <summary>Its values by key, of each type.</summary>
        public List<KeyValuePair<int, float>> Floats { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, Vector3>> Vec3s { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, Quaternion>> Quats { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, int>> Ints { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, long>> Longs { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, string>> Strings { get; } = [];

        /// <inheritdoc cref="Floats"/>
        public List<KeyValuePair<int, byte[]>> Bytes { get; } = [];

        /// <summary>
        /// Creates the object (main thread only), relinking it when it had a link, hands it to the mods (Announce) and
        /// returns it.
        /// </summary>
        public ZDO Make()
        {
            ZDO zdo = ZDOMan.instance.CreateNewZDO(Position, Prefab);
            zdo.Persistent = (Flags & 0x100) != 0;
            zdo.Distant = (Flags & 0x200) != 0;
            zdo.Type = (ZDO.ObjectType)((Flags >> 10) & 3);
            zdo.SetPrefab(Prefab);
            zdo.SetRotation(Quaternion.Euler(Euler));
            SetValues(zdo);
            if (LinkType != ZDOExtraData.ConnectionType.None)
            {
                Relink(zdo, LinkType, LinkHash);
            }
            Announce(zdo);
            return zdo;
        }

        /// <summary>
        /// Gives an object of the same prefab this one's values in place, under its own ZDOID (main thread only): values
        /// it has that this one has not are taken away, the others set; its place, turn and link are left as they are.
        /// Its data's revision goes up, so every peer is sent the new data.
        /// </summary>
        public void PutOn(ZDO zdo)
        {
            ZDOExtraData.GetData(zdo.m_uid, out var floats, out var vec3s, out var quats, out var ints, out var longs, out var strings, out var bytes, out _);
            static IEnumerable<int> Stale<T>(List<KeyValuePair<int, T>> had, List<KeyValuePair<int, T>> now) =>
                had == null ? [] : had.Select(kv => kv.Key).Except(now.Select(kv => kv.Key)).ToList();
            foreach (int key in Stale(floats, Floats))
            {
                zdo.RemoveFloat(key);
            }
            foreach (int key in Stale(vec3s, Vec3s))
            {
                zdo.RemoveVec3(key);
            }
            foreach (int key in Stale(quats, Quats))
            {
                zdo.RemoveQuaternion(key);
            }
            foreach (int key in Stale(ints, Ints))
            {
                zdo.RemoveInt(key);
            }
            foreach (int key in Stale(longs, Longs))
            {
                zdo.RemoveLong(key);
            }
            foreach (int key in Stale(strings, Strings))
            {
                zdo.RemoveString(key);
            }
            foreach (int key in Stale(bytes, Bytes))
            {
                zdo.RemoveByteArray(key);
            }
            SetValues(zdo);
            zdo.DataRevision++;
        }

        /// <summary>Sets each of this object's values on a ZDO.</summary>
        private void SetValues(ZDO zdo)
        {
            Floats.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Vec3s.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Quats.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Ints.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Longs.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Strings.ForEach(kv => zdo.Set(kv.Key, kv.Value));
            Bytes.ForEach(kv => zdo.Set(kv.Key, kv.Value));
        }
    }
}
