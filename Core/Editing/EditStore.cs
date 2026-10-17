using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// The editor's working copy of every zone's terrain modifications. It starts from the world file
/// and receives the browser's edits; nothing here is written to disk (saving is a separate step).
/// Its members take the store's lock (History aside), and zones are handed out as copies.
/// </summary>
public sealed partial class EditStore
{
    /// <summary>Vertices per zone side (64 m, one per metre, both edges included).</summary>
    public const int Grid = 65;

    /// <summary>Vertices per zone.</summary>
    public const int Cells = Grid * Grid;

    /// <summary>The game clamps level edits to ±this (metres; TerrainComp).</summary>
    public const float MaxLevel = 8f;

    /// <summary>The game clamps smoothing edits to ±this (metres; TerrainComp).</summary>
    public const float MaxSmooth = 1f;

    /// <summary>
    /// No limit: how far the editor moves ground beyond that (the world's ground stays well within
    /// what the game draws: about -400 to +1000 m).
    /// </summary>
    public const float MaxLift = 400f;

    private readonly Lock _lock = new();

    /// <summary>The working copy of each zone with terrain data, by zone.</summary>
    private readonly Dictionary<(int, int), ZoneEdit> _zones = [];

    /// <summary>Bumped on every change so cached map detail can be rebuilt.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Bumped when the objects to leave out or the added ones change (not the ground): for what is worked out from
    /// the objects alone, kept through ground strokes.
    /// </summary>
    public int ObjectsVersion { get; private set; }

    /// <summary>Starts from the world's terrain, with nothing pending.</summary>
    public EditStore(WorldSave world)
    {
        Load(world);
    }

    /// <summary>
    /// The page's undo history (its own JSON), kept here so it survives reloads of the page and moves of
    /// the work area. It describes the pending changes, so it goes with them (ResetFrom).
    /// </summary>
    public string? History { get; set; }

    /// <summary>
    /// Changes each time the pending changes are reset: history sent for an earlier generation (a page
    /// closing after a save, for example) is not kept.
    /// </summary>
    public int HistoryGeneration { get; private set; }
}

/// <summary>
/// One zone's terrain modifications, as the game keeps them (TerrainComp): per vertex of the 65 × 65 grid,
/// a height change and a paint, plus the editor's own lift beyond the game's limits.
/// </summary>
public sealed partial class ZoneEdit(int zoneX, int zoneZ)
{
    /// <summary>The zone's column (64 m zones, 0 at the world's centre).</summary>
    public int ZoneX { get; } = zoneX;

    /// <summary>The zone's row (64 m zones, 0 at the world's centre).</summary>
    public int ZoneZ { get; } = zoneZ;

    /// <summary>True when the world file already has terrain data for this zone.</summary>
    public bool ExistsInWorld { get; set; }

    /// <summary>True when the editor changed this zone since the world was loaded (or since it was last applied).</summary>
    public bool Changed { get; set; }

    /// <summary>Per vertex: its height is changed (Level and Smooth count only where it is).</summary>
    public bool[] Modified { get; init; } = new bool[EditStore.Cells];

    /// <summary>Per vertex: the level change (metres, ±8).</summary>
    public float[] Level { get; init; } = new float[EditStore.Cells];

    /// <summary>Per vertex: the smoothing change (metres, ±1).</summary>
    public float[] Smooth { get; init; } = new float[EditStore.Cells];

    /// <summary>Per vertex: its paint is changed.</summary>
    public bool[] PaintModified { get; init; } = new bool[EditStore.Cells];

    /// <summary>RGBA per vertex: r dirt, g cultivated, b paved, a vegetation allowed.</summary>
    public float[] Paint { get; init; } = new float[EditStore.Cells * 4];

    /// <summary>
    /// Height beyond the game's ±8 m (the editor's No limit): not saved as such, but turned into
    /// ground discs the game counts as generated ground (Uplift) when saving or applying.
    /// </summary>
    public float[] Lift { get; init; } = new float[EditStore.Cells];

    /// <summary>
    /// The ground shown at cell <paramref name="i"/> over its generated height: the edit (level and smoothing, held to the
    /// game's ±8 m) where the cell is modified, then the No limit lift.
    /// </summary>
    public float HeightAt(int i, float baseHeight) =>
        baseHeight + (Modified[i] ? Math.Clamp(Level[i] + Smooth[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0) + Lift[i];

    /// <summary>Whether any vertex has a lift.</summary>
    public bool HasLift => Lift.Any(l => l != 0f);

    /// <summary>How many vertices have their height changed.</summary>
    public int HeightCount => Modified.Count(m => m);

    /// <summary>How many vertices have their paint changed.</summary>
    public int PaintCount => PaintModified.Count(m => m);

    /// <summary>Nothing changed: no height, paint or lift.</summary>
    public bool IsEmpty => !Modified.Any(m => m) && !PaintModified.Any(m => m) && !HasLift;

    /// <summary>A deep copy (the arrays copied).</summary>
    public ZoneEdit Clone() => new(ZoneX, ZoneZ)
    {
        ExistsInWorld = ExistsInWorld,
        Changed = Changed,
        Modified = (bool[])Modified.Clone(),
        Level = (float[])Level.Clone(),
        Smooth = (float[])Smooth.Clone(),
        PaintModified = (bool[])PaintModified.Clone(),
        Paint = (float[])Paint.Clone(),
        Lift = (float[])Lift.Clone()
    };
}

/// <summary>A new object placed in the editor.</summary>
/// <param name="Id">The negative id the browser gave it.</param>
/// <param name="Prefab">Its prefab hash.</param>
/// <param name="Position">World position (metres).</param>
/// <param name="Rotation">Unity Euler rotation (degrees).</param>
/// <param name="Scale">Uniform scale (0 = as the template object).</param>
/// <param name="SourceId">The object it comes from (else the first object of the prefab).</param>
/// <param name="Fresh">
/// A new independent object (only the builder and the scale are taken from the source, the scale unless
/// Scale gives one); otherwise, for a moved object,
/// all of the source's data (chest contents, health, builder...) is kept.
/// </param>
/// <param name="Raw">
/// The object's complete data in the save format (an object edited in the inspector, or restored from a
/// backup); it is written with this position, rotation and scale instead of copying a source.
/// </param>
/// <param name="Data">Values set on it however it is made (a copy of a model, blank, or Raw), last; see ObjectField.</param>
public sealed record NewObject(int Id, int Prefab, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale, int? SourceId = null, bool Fresh = true, byte[]? Raw = null,
    IReadOnlyList<ObjectField>? Data = null);

/// <summary>
/// A value of an object's data. As text (blueprints, the clipboard): "section;key;value in base64" ("-" for
/// none).
/// </summary>
/// <param name="Section">floats, vec3, quats, ints, longs, strings or bytes.</param>
/// <param name="Key">The stable hash of its name.</param>
/// <param name="Value">As ZdoData.Set takes it (text; base64 for bytes), null to remove it.</param>
public sealed partial record ObjectField(string Section, int Key, string? Value)
{
    /// <summary>The data key ("creator") of an object's builder, a player id: the one constant for it.</summary>
    public static readonly int CreatorKey = StableHash.Of("creator");

    /// <summary>No builder: the game takes the object for part of a ruin (generated dungeons).</summary>
    public static readonly ObjectField NoBuilder = new("longs", CreatorKey, null);

    /// <summary>The field as text: "section;key;value in base64" ("-" for none).</summary>
    public override string ToString() => $"{Section};{Key.ToString(System.Globalization.CultureInfo.InvariantCulture)};"
        + (Value == null ? "-" : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value)));
}

/// <summary>Give a zone back to the world generator.</summary>
/// <param name="X">The zone's column.</param>
/// <param name="Z">The zone's row.</param>
/// <param name="KeepBuildings">Keeps player-built pieces.</param>
/// <param name="Ground">Also removes the terrain edits.</param>
public sealed record ZoneReset(int X, int Z, bool KeepBuildings, bool Ground);
