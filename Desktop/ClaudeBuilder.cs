using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// Floors, walls and roofs over a rectangle, laid out piece by piece from the game's pieces (their
/// bounds and snap points, PieceCatalog) for Claude's build_floor, build_walls and build_roof. Pure: the
/// pieces to put down, in world coordinates (x east, z north, y up), on the 2 m grid of wood pieces.
/// </summary>
public static partial class ClaudeBuilder
{
    /// <summary>A door in a wall.</summary>
    /// <param name="Side">The rectangle's side: north, south, east or west (any case).</param>
    /// <param name="At">Where along that side, in world metres (x on north and south, z on east and west); the door
    /// goes in the 2 m of wall nearest it.</param>
    public sealed record Door(string Side, float At);

    /// <summary>The most pieces one layout puts down: a larger one is refused (built in parts instead).</summary>
    public const int MaxPieces = 5000;

    /// <summary>Throws <see cref="ArgumentException"/> when a layout would be more than <see cref="MaxPieces"/> pieces.</summary>
    /// <param name="count">How many pieces it would be.</param>
    private static void Limit(double count)
    {
        if (!(count <= MaxPieces))
        {
            throw new ArgumentException($"That would be {count:0} pieces, more than {MaxPieces} in one go: build it in parts.");
        }
    }

    /// <summary>How many 2 m pieces <see cref="Across"/> puts across a span (at least one).</summary>
    private static float Count(float from, float to) => MathF.Max(1, MathF.Ceiling(MathF.Abs(to - from) / 2 - 0.01f));

    /// <summary>The floor tile for a material; anything but wood or stone throws <see cref="ArgumentException"/>.</summary>
    private static string FloorPrefab(string material) => material.ToLowerInvariant() switch
    {
        "stone" => "stone_floor_2x2",
        "wood" => "wood_floor",
        _ => throw new ArgumentException("A floor is wood or stone."),
    };

    /// <summary>The wall piece and its row height for a material; anything but wood or stone throws <see cref="ArgumentException"/>.</summary>
    private static (string Prefab, float Row) WallKind(string material) => material.ToLowerInvariant() switch
    {
        "stone" => ("stone_wall_2x1", 1f),
        "wood" => ("woodwall", 2f),
        _ => throw new ArgumentException("Walls are wood or stone."),
    };

    /// <summary>How many rows of row metres make up height (whole rows, at least one).</summary>
    private static float Rows(float height, float row) => MathF.Max(1, MathF.Round(height / row));

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <see cref="Floor"/> over the rectangle would be more than
    /// <see cref="MaxPieces"/> tiles: needs no height, so a caller checks it before working one out from the ground.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    public static void CheckFloor(float x0, float z0, float x1, float z1) => Limit((double)Count(x0, x1) * Count(z0, z1));

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <see cref="Walls"/> on the rectangle would be more than
    /// <see cref="MaxPieces"/> pieces (or the material is not wood or stone): needs no bottom, so a caller checks it
    /// before working one out from the ground.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="height">How tall, in metres.</param>
    /// <param name="material">"wood" or "stone" (any case).</param>
    public static void CheckWalls(float x0, float z0, float x1, float z1, float height, string material) =>
        Limit((double)Rows(height, WallKind(material).Row) * 2 * (Count(x0, x1) + Count(z0, z1)));

    /// <summary>A piece's bounds and snap points; throws <see cref="ArgumentException"/> for a prefab that is not a piece.</summary>
    private static PieceCatalog.Info Info(string prefab) =>
        PieceCatalog.Get(StableHash.Of(prefab)) ?? throw new ArgumentException($"{prefab} is not a piece the editor knows.");

    /// <summary>
    /// Pieces of 2 m across a span: their middles, centred on the span (a span not a multiple of 2 m
    /// overhangs a little on both ends).
    /// </summary>
    private static List<float> Across(float from, float to)
    {
        float mid = (from + to) / 2;
        int n = (int)Count(from, to);
        return [.. Enumerable.Range(0, n).Select(i => mid - n + 1 + 2 * i)];
    }

    /// <summary>A piece put exactly there (not snapped), yaw in degrees.</summary>
    private static ClaudeTools.PieceSpec P(string prefab, float x, float y, float z, float yaw) =>
        new() { Prefab = prefab, X = x, Y = y, Z = z, Yaw = yaw, Snap = false };

    /// <summary>Floor tiles over the rectangle, their tops at top.</summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="top">The height of the floor's top, in metres.</param>
    /// <param name="material">"wood" or "stone" (any case); anything else throws <see cref="ArgumentException"/>.</param>
    /// <exception cref="ArgumentException">Also when it would be more than <see cref="MaxPieces"/> tiles.</exception>
    public static List<ClaudeTools.PieceSpec> Floor(float x0, float z0, float x1, float z1, float top, string material)
    {
        string prefab = FloorPrefab(material);
        CheckFloor(x0, z0, x1, z1);
        float y = top - Info(prefab).MaxY;
        return [.. (from x in Across(x0, x1) from z in Across(z0, z1) select P(prefab, x, y, z, 0))];
    }
}
