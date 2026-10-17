namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>
    /// The flags of the object's save format: the base flags kept, then one for each part the object has.
    /// </summary>
    /// <param name="small">Whether the position is stored in the short form.</param>
    /// <param name="rotated">Whether a rotation is stored.</param>
    private ushort SerializeFlags(bool small, bool rotated)
    {
        ushort flags = (ushort)(BaseFlags & KeptFlags);
        if (small)
        {
            flags |= SmallPositionFlag;
        }

        if (rotated)
        {
            flags |= RotationFlag;
        }

        if (Connection != null)
        {
            flags |= Connections;
        }

        if (FloatList.Count > 0)
        {
            flags |= Floats;
        }

        if (Vec3List.Count > 0)
        {
            flags |= Vec3s;
        }

        if (QuatList.Count > 0)
        {
            flags |= Quats;
        }

        if (IntList.Count > 0)
        {
            flags |= Ints;
        }

        if (LongList.Count > 0)
        {
            flags |= Longs;
        }

        if (StringList.Count > 0)
        {
            flags |= Strings;
        }

        if (ByteList.Count > 0)
        {
            flags |= ByteArrays;
        }

        return flags;
    }

    /// <summary>
    /// Writes the object's lists after its position, rotation and connection: each non-empty one as its count and its
    /// (key, value) pairs, in the order the save format reads them.
    /// </summary>
    /// <param name="w">The writer the object's bytes go to.</param>
    private void WriteParts(BinaryWriter w)
    {
        void Each<T>(List<(int Key, T Value)> list, Action<T> write)
        {
            if (list.Count == 0)
            {
                return;
            }
            ZdoBuilder.WriteNumItems(w, list.Count);
            foreach (var (k, v) in list)
            {
                w.Write(k);
                write(v);
            }
        }
        Each(FloatList, w.Write);
        Each(Vec3List, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); });
        Each(QuatList, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); });
        Each(IntList, w.Write);
        Each(LongList, w.Write);
        Each(StringList, w.Write);
        Each(ByteList, v => { w.Write(v.Length); w.Write(v); });
    }
}
