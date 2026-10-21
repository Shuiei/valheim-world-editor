namespace TerrainEditor.Tunnelheim;

/// <summary>
/// Tunnelheim's rock over part of a world, as the mod keeps it in a game: a grid of samples 0.5 m apart, cut into
/// 16 m columns (one record each, <see cref="ColumnRecord"/>) and 16 m chunks. A column is voxelised (it has chunks,
/// its ground is drawn as rock, and the game's ground there is hidden) once a record or an edit reaches it; its seed
/// ground is the ground height at each of its points, from its record or else from the ground the game shows there
/// (<see cref="Ground"/>). Samples outside every chunk read as seed ground.
/// </summary>
/// <remarks>Followed: Tunnelheim 1.0.0 (VoxelWorld's grid, columns and chunks).</remarks>
public sealed partial class RockGrid
{
    /// <summary>The distance between samples, in metres.</summary>
    public const float Step = 0.5f;

    /// <summary>Samples along a chunk's side (and a column's).</summary>
    public const int Samples = 32;

    /// <summary>Samples in a chunk.</summary>
    public const int ChunkSamples = Samples * Samples * Samples;

    /// <summary>A column's (and a chunk's) side, in metres.</summary>
    public const float ColumnMetres = Samples * Step;

    /// <summary>How far from the surface a density is kept, in metres.</summary>
    public const float Band = 4f;

    /// <summary>How deep below its seed ground rock can be dug, in metres.</summary>
    public const float MaxDepth = 64f;

    private readonly Dictionary<(int X, int Z), float[]> _seeds = [];
    private readonly Dictionary<(int X, int Z), List<int>> _columns = [];
    private readonly Dictionary<(int X, int Y, int Z), RockChunk> _chunks = [];

    /// <summary>A grid over ground the given heights describe.</summary>
    /// <param name="ground">The height of the ground the game shows at a point (world x, z in metres): its heightmap with
    /// the edits, as Tunnelheim reads it for columns it has no seeds of.</param>
    /// <param name="rules">What else the edits depend on in the game.</param>
    public RockGrid(Func<float, float, float> ground, RockRules? rules = null)
    {
        Ground = ground;
        Rules = rules ?? RockRules.Default;
    }

    /// <summary>The ground the game shows, for columns without seeds.</summary>
    public Func<float, float, float> Ground { get; }

    /// <summary>What else the edits depend on.</summary>
    public RockRules Rules { get; }

    /// <summary>How far above its seed ground rock may be added, in metres.</summary>
    public float MaxBuildHeight => Rules.MaxBuildHeight;

    /// <summary>Whether a point lies in the Deep North.</summary>
    private bool DeepNorth(float x, float z) => Rules.DeepNorth?.Invoke(x, z) ?? false;

    /// <summary>The voxelised columns.</summary>
    public IEnumerable<(int X, int Z)> Columns => _columns.Keys;

    /// <summary>The chunks there are, each with its column and height index.</summary>
    public IEnumerable<KeyValuePair<(int X, int Y, int Z), RockChunk>> Chunks => _chunks;

    /// <summary>Whether a column is voxelised.</summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    public bool IsColumn(int cx, int cz) => _columns.ContainsKey((cx, cz));

    /// <summary>A chunk, if there is one.</summary>
    /// <param name="cx">The chunk's column index along x.</param>
    /// <param name="cy">Its height index.</param>
    /// <param name="cz">Its column index along z.</param>
    public RockChunk? Chunk(int cx, int cy, int cz) => _chunks.GetValueOrDefault((cx, cy, cz));

    /// <summary>The height indices of a column's chunks, lowest first; empty when the column is not voxelised.</summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    public IReadOnlyList<int> ChunksOf(int cx, int cz) => _columns.TryGetValue((cx, cz), out var ys) ? ys : [];

    /// <summary>A column's seeds, if it has some (it is voxelised, or a record gave them).</summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    public float[]? SeedsOf(int cx, int cz) => _seeds.GetValueOrDefault((cx, cz));

    /// <summary>The column of a grid point.</summary>
    /// <param name="gx">The point's index along x.</param>
    /// <param name="gz">The point's index along z.</param>
    public static (int X, int Z) ColumnOf(int gx, int gz) => (FloorDiv(gx, Samples), FloorDiv(gz, Samples));

    /// <summary>The column of a world point.</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    public static (int X, int Z) ColumnAt(float x, float z) => (RockMath.Floor(x / ColumnMetres), RockMath.Floor(z / ColumnMetres));

    /// <summary>a / b rounded down (towards minus infinity).</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor, positive.</param>
    public static int FloorDiv(int a, int b) => a < 0 ? -((-a + b - 1) / b) : a / b;

    /// <summary>The index of a sample in its chunk.</summary>
    /// <param name="gx">The sample's grid x.</param>
    /// <param name="gy">Its grid y.</param>
    /// <param name="gz">Its grid z.</param>
    public static int IndexIn(int gx, int gy, int gz) =>
        gx - FloorDiv(gx, Samples) * Samples + Samples * (gy - FloorDiv(gy, Samples) * Samples + Samples * (gz - FloorDiv(gz, Samples) * Samples));
}
