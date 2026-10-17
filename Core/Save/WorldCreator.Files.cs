using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>
    /// The game's terrain compiler object of zone 0, 0 with nothing edited: its position in the short form, the
    /// prefab's object flags, and the TCData the writer encodes for an untouched zone.
    /// </summary>
    internal static byte[] EmptyTerrainObject()
    {
        ushort flags = Terrain.PrefabCatalog.Get(TerrainCompiler)?.Flags ?? 0x0D00;
        var z = ZdoData.Parse(ZdoBuilder.Blank(TerrainCompiler, flags, Vector3.Zero, Vector3.Zero, 0f));
        z.SmallPosition = true;
        z.SetBytes(StableHash.Of("TCData"), WorldWriter.EncodeTerrain(new ZoneEdit(0, 0)));
        return z.Serialize();
    }

    /// <summary>
    /// World.SaveWorldMetaData: a length, then the package (version, name, seed name, seed, unique id, generator
    /// version, "has a database", no starting global keys, and the trailing count the game writes after them).
    /// </summary>
    private static void WriteMetadata(string path, string name, string seedName, int seed, long uid)
    {
        using MemoryStream pkg = new();
        using (BinaryWriter p = new(pkg, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            p.Write(SaveFileVersion);
            p.Write(name);
            p.Write(seedName);
            p.Write(seed);
            p.Write(uid);
            p.Write(WorldGenVersion);
            p.Write(true);
            p.Write(0);
            p.Write(0);
        }
        WorldWriter.WriteAllBytesDurable(path, [.. BitConverter.GetBytes((int)pkg.Length), .. pkg.ToArray()]);
    }

    /// <summary>One chunk file: version, object count, then the objects' save-format bytes back to back; flushed to disk.</summary>
    private static void WriteChunk(string path, List<byte[]> objects)
    {
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        using BinaryWriter w = new(fs);
        w.Write((short)SaveFileVersion);
        w.Write(objects.Count);
        foreach (byte[] o in objects)
        {
            w.Write(o);
        }
        w.Flush();
        fs.Flush(flushToDisk: true);
    }

    /// <summary>
    /// RandEventSystem's save with no event running (timer, event name, time, position), then the list.
    /// </summary>
    private static byte[] Tail()
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(0f);
            w.Write("");
            w.Write(0f);
            w.Write(0f); w.Write(0f); w.Write(0f);
            w.Write(EmptyListBlock);
        }
        return ms.ToArray();
    }
}
