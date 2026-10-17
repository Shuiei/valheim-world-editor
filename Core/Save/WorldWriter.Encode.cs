using System.IO.Compression;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// A save format in the world the writer does not know (the index's first, then the chunk files' in order), or null
    /// when it can save every file.
    /// </summary>
    internal static int? UnwritableFormat(WorldSave world) =>
        !WorldVersion.Writable(world.SaveVersion) ? world.SaveVersion
        : world.Chunks.FirstOrDefault(c => !WorldVersion.Writable(c.WorldVersion))?.WorldVersion;

    /// <summary>Mirrors TerrainComp.Save: a GZip-compressed ZPackage.</summary>
    public static byte[] EncodeTerrain(ZoneEdit e)
    {
        using MemoryStream raw = new();
        using (BinaryWriter w = new(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(1);
            w.Write(1);                 // operations; the game only compares it to its previous value
            w.Write(e.ZoneX * 64f);     // last operation point and radius: used to refresh grass
            w.Write(0f);
            w.Write(e.ZoneZ * 64f);
            w.Write(64f);
            w.Write(EditStore.Cells);
            for (int i = 0; i < EditStore.Cells; i++)
            {
                w.Write(e.Modified[i]);
                if (e.Modified[i])
                {
                    w.Write(e.Level[i]);
                    w.Write(e.Smooth[i]);
                }
            }
            w.Write(EditStore.Cells);
            for (int i = 0; i < EditStore.Cells; i++)
            {
                w.Write(e.PaintModified[i]);
                if (e.PaintModified[i])
                {
                    for (int c = 0; c < 4; c++)
                    {
                        w.Write(e.Paint[i * 4 + c]);
                    }
                }
            }
        }
        using MemoryStream compressed = new();
        using (GZipStream gz = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(raw.GetBuffer(), 0, (int)raw.Length);
        }
        return compressed.ToArray();
    }

    /// <summary>
    /// Template objects' bytes read from their chunk files on this thread so far: a test seam, a save reads its
    /// template once however many zones get new ground.
    /// </summary>
    [field: ThreadStatic]
    internal static int TemplateReads { get; private set; }

    /// <summary>The template terrain object's own bytes (Start..End), read from its chunk file alone.</summary>
    /// <param name="world">The world the template is of.</param>
    /// <param name="src">Where the template object is.</param>
    private static byte[] TemplateBytes(WorldSave world, ZdoLocation src)
    {
        using FileStream fs = File.OpenRead(Path.Combine(world.Directory, src.File.FileName));
        byte[] own = new byte[src.End - src.Start];
        fs.Seek(src.Start, SeekOrigin.Begin);
        fs.ReadExactly(own);
        TemplateReads++;
        return own;
    }

    /// <summary>A new terrain object: the template object's bytes with this zone's position and data.</summary>
    /// <param name="src">Where the template object is (its offsets, in its chunk file).</param>
    /// <param name="own">The template object's bytes (<see cref="TemplateBytes"/>).</param>
    /// <param name="edit">The zone's edit.</param>
    /// <param name="data">The zone's encoded terrain data.</param>
    /// <exception cref="InvalidOperationException">The template's position is not in the short format.</exception>
    private static byte[] NewTerrainObject(ZdoLocation src, byte[] own, ZoneEdit edit, byte[] data)
    {
        if ((src.Flags & ZdoData.SmallPositionFlag) == 0)
        {
            throw new InvalidOperationException("Unexpected terrain object layout (position is not in the short format).");
        }
        byte[] head = own[..(int)(src.DataStart - src.Start)];
        // Bytes 2..5 are the position as two shorts (x, z) of the zone centre.
        BitConverter.GetBytes((short)(edit.ZoneX * 64)).CopyTo(head, 2);
        BitConverter.GetBytes((short)(edit.ZoneZ * 64)).CopyTo(head, 4);
        using MemoryStream ms = new();
        ms.Write(head);
        ms.Write(BitConverter.GetBytes(data.Length));
        ms.Write(data);
        ms.Write(own, (int)(src.DataEnd - src.Start), (int)(src.End - src.DataEnd));
        return ms.ToArray();
    }
}
