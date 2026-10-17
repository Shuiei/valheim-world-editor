
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Loads a world from a WorldEditorBridge snapshot (gzip-compressed, snapshot version 1).</summary>
    /// <param name="gzipped">The snapshot as <c>LiveBridge.Snapshot</c> returns it.</param>
    /// <param name="source">What to show as the world's <c>Directory</c>.</param>
    /// <exception cref="InvalidDataException">
    /// Not a bridge snapshot, or a version this editor does not read.
    /// </exception>
    public static WorldSave LoadLive(byte[] gzipped, string source)
    {
        byte[] raw = CheckedReads.Gunzip(gzipped, MaxSnapshot);
        WorldSave save = new() { Directory = source, SaveNumber = 0, IsLive = true, LiveBytes = raw };
        using ValheimReader pkg = new(new MemoryStream(raw));
        if (pkg.ReadInt() != 0x42455756)
        {
            throw new InvalidDataException("not a WorldEditorBridge snapshot");
        }
        int version = pkg.ReadInt();
        if (version != 1)
        {
            throw new InvalidDataException($"snapshot version {version} is not supported (update the editor)");
        }
        save.Name = pkg.ReadString();
        save.SeedName = pkg.ReadString();
        save.Seed = pkg.ReadInt();
        save.WorldGenVersion = pkg.ReadInt();
        double netTime = BitConverter.Int64BitsToDouble(pkg.ReadLong());
        save.Zones = ZoneDb.FromPackage(pkg.ReadBytes(pkg.ReadInt()), netTime);
        ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0 };
        save.ReadObjects(pkg, file);
        save.Chunks.Add(file);
        save.ChunkCount = 1;
        foreach (ObjectRef o in save.ObjectRefs)
        {
            o.LiveId = (pkg.ReadLong(), pkg.ReadUInt());
        }
        return save;
    }

    /// <summary>The objects of one chunk file, read alone: the fuzz tests' way in.</summary>
    internal static WorldSave ReadChunk(Stream stream)
    {
        WorldSave save = new() { Directory = "", SaveNumber = 0 };
        ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0 };
        using ValheimReader pkg = new(stream);
        save.ReadObjects(pkg, file);
        save.Chunks.Add(file);
        return save;
    }

    /// <summary>Reads every object of one chunk file.</summary>
    private void LoadChunk(string path, ChunkFile file)
    {
        using ValheimReader pkg = new(new BufferedStream(File.OpenRead(path), 1 << 20));
        ReadObjects(pkg, file);
    }

    /// <summary>
    /// A chunk's objects: its world version, the object count, then each object; records the file's length.
    /// </summary>
    private void ReadObjects(ValheimReader pkg, ChunkFile file)
    {
        file.WorldVersion = pkg.ReadShort();
        // The smallest object: flags (2), a small position (4), its prefab (4).
        file.Count = pkg.ReadCount(10);
        for (int i = 0; i < file.Count; i++)
        {
            ReadZdo(pkg, file.WorldVersion, file);
            ObjectCount++;
        }
        file.Length = pkg.Position;
    }
}
