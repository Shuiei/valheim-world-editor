using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Writes the contents of a container (see <see cref="InventoryData"/>): the header and each item.
/// </summary>
public sealed partial class InventoryData
{
    /// <summary>The inventory as a byte array in the current item format (<c>Version</c>).</summary>
    public byte[] Write()
    {
        using MemoryStream ms = new();
        using BinaryWriter w = new(ms, Encoding.UTF8);
        w.Write(Version);
        w.Write((ushort)Items.Count);
        foreach (Item it in Items)
        {
            WriteItem(w, it);
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>One item as ItemDrop.ItemData.Save writes it, in the current item format (Version).</summary>
    public static void WriteItem(BinaryWriter w, Item it)
    {
        int flags = (it.PickedUp ? 1 : 0) | (it.Equipped ? 2 : 0) | (it.Quality != 1 ? 4 : 0) | (it.Stack != 1 ? 8 : 0)
            | (it.Variant != 0 ? 0x10 : 0) | (it.CrafterId != 0 ? 0x20 : 0) | (it.Prefab != 0 ? 0x40 : 0) | (it.CustomData.Count != 0 ? 0x80 : 0);
        // Rounded: read as hundredths times 0.01f, a value cut down would come back a hundredth lower each time.
        w.Write((int)MathF.Round(it.Durability * 100f));
        w.Write((byte)it.X);
        w.Write((byte)it.Y);
        w.Write((byte)it.WorldLevel);
        w.Write((byte)flags);
        if ((flags & 4) != 0)
        {
            w.Write((ushort)it.Quality);
        }

        if ((flags & 8) != 0)
        {
            w.Write((ushort)it.Stack);
        }

        if ((flags & 0x10) != 0)
        {
            w.Write(it.Variant);
        }

        if ((flags & 0x20) != 0) { w.Write(it.CrafterId); w.Write(it.CrafterName); }
        if ((flags & 0x40) != 0)
        {
            w.Write(it.Prefab);
        }

        if ((flags & 0x80) != 0)
        {
            ZdoBuilder.WriteNumItems(w, it.CustomData.Count);
            foreach (var (k, v) in it.CustomData)
            {
                w.Write(k);
                w.Write(v);
            }
        }
        w.Write((byte)(it.Cheated ? 1 : 0));
    }
}
