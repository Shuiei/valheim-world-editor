
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Reads the .chunks index, then every chunk file it lists.</summary>
    /// <exception cref="FileNotFoundException">A chunk file the index lists is missing.</exception>
    private void LoadChunks()
    {
        string indexPath = Path.Combine(Directory, $"_main.{SaveNumber}.chunks");
        List<(ushort Chunk, byte Size, uint Version, int Count)> chunks = [];
        using (ValheimReader index = new(File.OpenRead(indexPath)))
        {
            SaveVersion = index.ReadShort();
            index.ReadInt();
            // Each: chunk (2), size (1), version (4), object count (4).
            int count = index.ReadCount(11);
            for (int i = 0; i < count; i++)
            {
                chunks.Add((index.ReadUShort(), index.ReadByte(), index.ReadUInt(), index.ReadInt()));
            }
        }
        foreach (var (chunk, size, version, numZdos) in chunks)
        {
            ChunkFile file = new() { Chunk = chunk, Size = size, Version = version, IndexCount = numZdos };
            string path = Path.Combine(Directory, file.FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Chunk file listed in the index is missing", path);
            }
            LoadChunk(path, file);
            Chunks.Add(file);
            ChunkCount++;
        }
    }

    /// <summary>True when the world came from the live bridge plugin instead of save files.</summary>
    public bool IsLive { get; private set; }

    /// <summary>The decompressed live snapshot; object byte ranges (ObjectRef.Start .. End) point into it.</summary>
    public byte[]? LiveBytes { get; private set; }

    /// <summary>
    /// Live: the bytes a chunk file's object offsets point into (the snapshot unless MergeLive made the file: each read
    /// of objects from the game is its own little file, holding its bytes, and let go with the last object read from it).
    /// </summary>
    public byte[] LiveSource(ChunkFile file) => file.Bytes ?? LiveBytes!;

    /// <summary>
    /// Live: objects the game no longer has (removed in the game). Left out of every list; their ids stay taken, and
    /// their bytes stay readable (history).
    /// </summary>
    public HashSet<int> Vanished { get; } = [];

    /// <summary>
    /// Live: held while the world's lists change (MergeLive) and while other threads read them (an area loading, the
    /// map's search and statistics): what the game changed comes in between.
    /// </summary>
    public object Sync { get; } = new();

    /// <summary>Live: the holds taken (<see cref="Hold"/>) and not let go yet; guarded by <see cref="Sync"/>.</summary>
    private int _holds;
}
