using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// Read-only loader for Valheim's chunked world save (world version &gt;= 40):
/// <list type="bullet">
/// <item><description>_main.&lt;n&gt;.chunks: index of chunk files and how many objects each holds</description></item>
/// <item><description>XX_YY__&lt;size&gt;_&lt;version&gt;.chunk: objects (ZDOs) of one chunk</description></item>
/// </list>
/// Only what the terrain editor needs is kept: every object's prefab and position, and the terrain modification data
/// ("TCData") of each zone's terrain compiler object.
/// </summary>
public sealed partial class WorldSave
{
    /// <summary>Data key of a terrain compiler's saved ground edits.</summary>
    private static readonly int TCDataKey = StableHash.Of("TCData");

    /// <summary>Prefab hash of the proxy object that stands for a placed location.</summary>
    public static readonly int LocationProxyPrefab = StableHash.Of("LocationProxy");

    /// <summary>Int data key on a location proxy: the location's name hash.</summary>
    public static readonly int LocationKey = StableHash.Of("location");

    /// <summary>Long data key of a terrain modifier's creation time.</summary>
    private static readonly int TimeCreatedKey = StableHash.Of("terrainModifierTimeCreated");

    /// <summary>
    /// Player ids and the names stored with them: bed and tombstone owners, ward builders, players (live: the players
    /// that are online).
    /// </summary>
    private static readonly int OwnerKey = StableHash.Of("owner"), OwnerNameKey = StableHash.Of("ownerName");
    /// <summary>String data key of the builder's name stored with <c>creator</c>.</summary>
    private static readonly int CreatorNameKey = StableHash.Of("creatorName");
    /// <summary>Player id and name keys on player objects.</summary>
    private static readonly int PlayerIdKey = StableHash.Of("playerID"), PlayerNameKey = StableHash.Of("playerName");

    /// <summary>Tameable.SetTamed writes it (an int, 1 when tamed).</summary>
    private static readonly int TamedKey = StableHash.Of("tamed");

    /// <summary>Prefabs (besides LocationProxy) whose objects carry terrain modifiers; set before loading.</summary>
    public static HashSet<int> ModifierPrefabs { get; set; } = [];

    /// <summary>
    /// The world's folder, or for a live world a description of its source ("live: " and the bridge address).
    /// </summary>
    public required string Directory { get; init; }

    /// <summary>The rolling save number (the n of _main.n.*) loaded; 0 for a live world.</summary>
    public required int SaveNumber { get; init; }

    /// <summary>The world's name from its metadata.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The seed as typed in the game.</summary>
    public string SeedName { get; private set; } = "";

    /// <summary>The seed number the generator uses (the seed name's stable hash).</summary>
    public int Seed { get; private set; }

    /// <summary>World.m_worldGenVersion (0 for metadata older than version 26).</summary>
    public int WorldGenVersion { get; private set; }

    /// <summary>
    /// The save format at the head of the index (_main.&lt;n&gt;.chunks): the game's when it last saved the world (0 for a
    /// live world). Chunk files the game did not write again keep the format they were written in (see
    /// <see cref="ChunkFile.WorldVersion"/>): a world of format 41 saved by Valheim 1.0.20 has a 42 index and 41 chunk
    /// files until their objects change.
    /// </summary>
    public int SaveVersion { get; internal set; }

    /// <summary>How many objects were read (live: grows as new objects come in).</summary>
    public int ObjectCount { get; private set; }

    /// <summary>How many chunk files were read (1 for a live world).</summary>
    public int ChunkCount { get; private set; }

    /// <summary>Every object with saved ground data (TCData), one entry each: a zone can have two.</summary>
    public List<TerrainZone> TerrainZones { get; } = [];

    /// <summary>The chunk files read, in index order (live: the one snapshot file).</summary>
    public List<ChunkFile> Chunks { get; } = [];
}

/// <summary>An object that can modify terrain while the game runs.</summary>
/// <param name="Prefab">Its prefab hash.</param>
/// <param name="Location">For a location proxy, the location's name hash; else 0.</param>
/// <param name="Position">World position in metres.</param>
/// <param name="Rotation">Euler degrees, as stored.</param>
/// <param name="TimeCreated">Its terrainModifierTimeCreated value, 0 when not stored.</param>
public sealed record PlacedObject(int Prefab, int Location, Vector3 Position, Vector3 Rotation, long TimeCreated);

/// <summary>One chunk file of a save, as its index lists it and as read.</summary>
public sealed class ChunkFile
{
    /// <summary>The base chunk id (see ChunkMath).</summary>
    public ushort Chunk { get; init; }

    /// <summary>How many low bits of x and y the chunk merges (see ChunkMath).</summary>
    public byte Size { get; init; }

    /// <summary>The file's version number, part of its name.</summary>
    public uint Version { get; init; }

    /// <summary>The object count the index lists for the file.</summary>
    public int IndexCount { get; init; }

    /// <summary>The world version written at the start of the file.</summary>
    public int WorldVersion { get; set; }

    /// <summary>The object count read from the file itself.</summary>
    public int Count { get; set; }

    /// <summary>Bytes read from the file, header and objects.</summary>
    public long Length { get; set; }

    /// <summary>Byte range of every object in the file, in file order.</summary>
    public List<(long Start, long End)> Objects { get; } = [];

    /// <summary>
    /// Live: the file's bytes, for one WorldSave.MergeLive made of objects read from the game since the snapshot (kept
    /// only as long as something refers to the file); null for the snapshot's and saved files.
    /// </summary>
    internal byte[]? Bytes { get; init; }

    /// <summary>Same naming as ChunkSaveMapping.GetChunkFilename.</summary>
    public string FileName => NameFor(Chunk, Size, Version);

    /// <summary>A chunk file's name: chunk high and low byte in hex, then size and version.</summary>
    public static string NameFor(ushort chunk, byte size, uint version) => $"{chunk >> 8:x2}_{chunk & 0xFF:x2}__{size}_{version}.chunk";
}

/// <summary>
/// Where a terrain object lives in its chunk file, and where its TCData value (int length + bytes) is.
/// </summary>
/// <param name="File">The chunk file.</param>
/// <param name="Start">Where the object starts.</param>
/// <param name="End">Where the object ends (-1 while it is still being read).</param>
/// <param name="DataStart">Where the TCData value starts.</param>
/// <param name="DataEnd">Where the TCData value ends.</param>
/// <param name="Flags">The object's ZDO flags.</param>
public sealed record ZdoLocation(ChunkFile File, long Start, long End, long DataStart, long DataEnd, ushort Flags);

/// <summary>One zone's terrain modifications, decoded from TerrainComp's saved data.</summary>
public sealed class TerrainZone
{
    /// <summary>Where the data sits in the save (set by the loader once the value is read).</summary>
    public ZdoLocation? Source { get; set; }

    /// <summary>A zone's side in metres.</summary>
    public const float ZoneSize = 64f;

    /// <summary>The terrain compiler object's position: the zone's centre.</summary>
    public required Vector3 Center { get; init; }

    /// <summary>The zone's x index.</summary>
    public int ZoneX => (int)MathF.Floor((Center.X + ZoneSize / 2f) / ZoneSize);

    /// <summary>The zone's z index.</summary>
    public int ZoneZ => (int)MathF.Floor((Center.Z + ZoneSize / 2f) / ZoneSize);

    /// <summary>The prefab hash of the terrain compiler object.</summary>
    public int Prefab { get; init; }

    /// <summary>TerrainComp's count of operations applied to the zone.</summary>
    public int Operations { get; init; }

    /// <summary>(Width+1)^2 vertices, row-major along world Z then X.</summary>
    public required bool[] ModifiedHeight { get; init; }

    /// <summary>Level change per vertex, in metres (0 where not modified).</summary>
    public required float[] LevelDelta { get; init; }

    /// <summary>Smoothing change per vertex, in metres (0 where not modified).</summary>
    public required float[] SmoothDelta { get; init; }

    /// <summary>Which points of the paint grid carry paint.</summary>
    public required bool[] ModifiedPaint { get; init; }

    /// <summary>Paint mask per point of the paint grid (RGBA, as saved; zero where not painted).</summary>
    public required Vector4[] Paint { get; init; }

    /// <summary>Vertices per row of the height grid.</summary>
    public int HeightWidth => (int)Math.Round(Math.Sqrt(ModifiedHeight.Length));

    /// <summary>Points per row of the paint grid.</summary>
    public int PaintWidth => (int)Math.Round(Math.Sqrt(ModifiedPaint.Length));

    // The largest terrain data read, unpacked: a zone's is about 110 KB (65 x 65 heights and paints).
    private const int MaxData = 16 << 20;

    // The most height or paint points taken: the game's grid is 65 x 65, so damaged data that claims more is refused
    // before its arrays are made (16 MB of data could otherwise ask for some 270 MB).
    private const int MaxPoints = 257 * 257;

    /// <summary>Mirrors TerrainComp.Load.</summary>
    /// <param name="center">The terrain compiler object's position.</param>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="compressed">The gzip-compressed TCData value.</param>
    public static TerrainZone Decode(Vector3 center, int prefab, byte[] compressed)
    {
        using ValheimReader pkg = new(new MemoryStream(CheckedReads.Gunzip(compressed, MaxData)));
        pkg.ReadInt();
        int operations = pkg.ReadInt();
        pkg.ReadVector3();
        pkg.ReadSingle();
        int heights = Points(pkg.ReadCount(1), "height");
        bool[] modified = new bool[heights];
        float[] level = new float[heights];
        float[] smooth = new float[heights];
        for (int i = 0; i < heights; i++)
        {
            modified[i] = pkg.ReadBool();
            if (modified[i])
            {
                level[i] = pkg.ReadSingle();
                smooth[i] = pkg.ReadSingle();
            }
        }
        int paints = Points(pkg.ReadCount(1), "paint");
        bool[] painted = new bool[paints];
        var paint = new Vector4[paints];
        for (int i = 0; i < paints; i++)
        {
            painted[i] = pkg.ReadBool();
            if (painted[i])
            {
                paint[i] = new Vector4(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
            }
        }
        return new TerrainZone
        {
            Center = center,
            Prefab = prefab,
            Operations = operations,
            ModifiedHeight = modified,
            LevelDelta = level,
            SmoothDelta = smooth,
            ModifiedPaint = painted,
            Paint = paint
        };
    }

    /// <summary>A grid's point count, checked against <see cref="MaxPoints"/>.</summary>
    /// <exception cref="InvalidDataException">The count is larger than any terrain grid.</exception>
    private static int Points(int count, string grid) => count <= MaxPoints ? count : throw new InvalidDataException($"{count} {grid} points in a terrain object (at most {MaxPoints})");
}
