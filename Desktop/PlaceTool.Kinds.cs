using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Choosing the kind of each object from the chosen ones by their weights, and which kinds are building pieces.
public sealed partial class PlaceTool
{
    /// <summary>A kind's weight in the mix (1 when not set).</summary>
    public int WeightOf(string n) => Weights.GetValueOrDefault(n, 1);

    /// <summary>The kind for a random draw t in [0, 1), by weight.</summary>
    public string PickName(IReadOnlyList<string> names, float t)
    {
        float total = names.Sum(WeightOf), x = t * total;
        foreach (string n in names)
        {
            x -= WeightOf(n);
            if (x < 0)
            {
                return n;
            }
        }
        return names[^1];
    }

    /// <summary>A piece's snap points (x, y, z in its own frame), or none for kinds that are not pieces.</summary>
    public static float[][] SnapsOf(string name) => PieceCatalog.Get(StableHash.Of(name))?.Snaps ?? [];

    /// <summary>A building piece the game snaps: one with at least two snap points.</summary>
    public static bool IsPiece(string name) => SnapsOf(name).Length >= 2;

    /// <summary>Whether any chosen kind is a piece the game snaps.</summary>
    public bool PiecesChosen => Chosen.Any(IsPiece);

    /// <summary>
    /// End to end was set by hand: <see cref="AutoSnap"/> leaves it alone. End to end otherwise switches itself on when
    /// every chosen kind is a piece the game snaps (fences, walls…).
    /// </summary>
    public bool EndToEndByHand { get; set; }

    /// <summary>
    /// One at a time was set by hand: <see cref="AutoSnap"/> leaves it alone. Otherwise it switches itself on when
    /// every chosen kind is a piece the game snaps, so pieces go one at a time under the cursor.
    /// </summary>
    public bool OneAtATimeByHand { get; set; }

    /// <summary>The random draws for layouts, scatters and paint strokes.</summary>
    private readonly Random _rand = new();

    /// <summary>Offsets the clumping noise; a new layout picks a new one.</summary>
    private float _clumpSeed = Random.Shared.NextSingle() * 1000;

    /// <summary>The most objects a line, grid or zone places at once.</summary>
    public const int MaxShape = 2000;
}
