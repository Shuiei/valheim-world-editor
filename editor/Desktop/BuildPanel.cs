using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// The Build dock (the Build tool in a world, the Workshop, Build here in a dungeon): the hammer's pieces in its tabs
/// (Building, Heavy building, Furniture…), each with a small picture, searchable; the one clicked is what the Place
/// tool puts down, one at a time under the cursor, or end to end along a line, circle or rectangle (Lay). Snapping
/// to the pieces already there, a grid for the rest, and the steps come from the building bar. What the piece costs
/// in game. Its Library (the Workshop): the blueprints (<see cref="BlueprintLibrary"/>, the same list and row buttons
/// as a world's Blueprints panel), and Generate dungeon (a dungeon of building pieces made onto the plot).
/// </summary>
public sealed partial class BuildPanel
{
    /// <summary>The panel itself, hidden until the Workshop opens.</summary>
    public Control Card { get; }

    /// <summary>The game's models, for the pictures (null: each piece's boxes).</summary>
    internal Func<ModelStore?> Models { get; set; } = () => null;

    /// <summary>
    /// The tab numbers of the cultivator's pieces (plants), the serving tray's (feasts) and the mods' (whatever their
    /// tool and category): not hammer categories.
    /// </summary>
    internal const int Plants = 100, Feasts = 101, Mods = 102;

    /// <summary>
    /// The hammer's tabs (Piece.PieceCategory), in its order, then the cultivator's and the serving
    /// tray's pieces (plants, feasts), then the mods' (shown when the game has some). Named by <see cref="TabName"/>.
    /// </summary>
    internal static readonly int[] Tabs = [2, 3, 4, 1, 0, 5, Plants, Feasts, Mods];

    /// <summary>A tab's name, in the language in use.</summary>
    /// <param name="category">The tab (one of <see cref="Tabs"/>).</param>
    internal static string TabName(int category) => category switch
    {
        2 => Lang.T("build.tabs.building"),
        3 => Lang.T("build.tabs.heavyBuilding"),
        4 => Lang.T("build.tabs.furniture"),
        1 => Lang.T("build.tabs.crafting"),
        0 => Lang.T("build.tabs.misc"),
        5 => Lang.T("build.tabs.more"),
        Plants => Lang.T("build.tabs.plants"),
        Feasts => Lang.T("build.tabs.feasts"),
        Mods => Lang.T("build.tabs.mods"),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Not one of the Build panel's tabs."),
    };

    /// <summary>The building bar, whose snapping, grid and steps the pieces are built with (shared with Select and Paste).</summary>
    internal BuildingBar Bar { get; }

    /// <summary>The Place tool's input, whose tool puts the pieces down.</summary>
    private readonly PlaceInput _input;

    /// <summary>The mod whose pieces the Mods tab lists (a <see cref="ModGroup"/>).</summary>
    private string? _mod;

    /// <summary>The tiles' pictures still being drawn, by prefab: set when drawn.</summary>
    private readonly Dictionary<string, List<Image>> _waiting = [];

    /// <summary>At most this many tiles are listed at once (a mod can add thousands of pieces): the search narrows.</summary>
    internal const int MaxTiles = 300;

    /// <summary>The tab shown (a category of <see cref="Tabs"/>).</summary>
    private int _tab = 2;

    /// <summary>The pieces' pictures by prefab, once drawn.</summary>
    private readonly Dictionary<string, Bitmap?> _pictures = [];

    /// <summary>The prefabs whose pictures are being drawn (or were), so each is drawn once.</summary>
    private readonly HashSet<string> _drawing = [];

    /// <summary>What <see cref="Pieces"/> last worked out, and from which data.</summary>
    private static ((int Data, int Manifests) Generation, List<(string Prefab, string Name, int Category)> List) _pieceList = ((-1, -1), []);

    /// <summary>The blueprints' folder (Homestead's).</summary>
    internal Func<string> Folder { get => Library.Folder; set => Library.Folder = value; }

    /// <summary>What a blueprint dragged from the list carries: its file.</summary>
    internal static DataFormat<string> BlueprintFormat => BlueprintLibrary.DragFormat;

    /// <summary>Whether the Library is shown.</summary>
    internal bool LibraryShown => _library.IsVisible;

    /// <summary>Whether the dungeon generator is shown.</summary>
    internal bool DungeonShown => _dungeon.IsVisible;

    /// <summary>The Place tool the panel sets.</summary>
    private PlaceTool Tool => _input.Tool;
}
