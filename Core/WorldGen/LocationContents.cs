using NVector3 = System.Numerics.Vector3;

namespace ValheimGen;

/// <summary>
/// What Expand World Data's location file changes about the objects a location kind spawns (its LocationExtraInfo):
/// objects added to it, its own objects swapped for others or removed, data given to them and to the location itself,
/// a random scale of the added objects, a raised spawn point and a turn to a random cardinal direction. A kind the
/// editor cannot build the game's way (run-time commands, a time-based seed, data computed when spawned...) says why
/// in <see cref="LeftToGame"/>: its zone is then left to the game.
/// </summary>
public sealed record LocationContents
{
    /// <summary>No change: the game's contents as they are.</summary>
    public static readonly LocationContents None = new();

    /// <summary>The objects added (the file's "objects" rows), in order.</summary>
    public IReadOnlyList<ContentObject> Objects { get; init; } = [];

    /// <summary>
    /// The swaps of the location's objects (objectSwap, then locationObjectSwap per object): by prefab, what it may
    /// become with the weight of each (summing to 1); an empty name removes the object.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Weighted<string>>> Swaps { get; init; } = new Dictionary<string, IReadOnlyList<Weighted<string>>>();

    /// <summary>
    /// The data given to the location's objects (objectData, then locationObjectData per key): by prefab or "all", the
    /// data each may get with its weight (summing to 1); null data gives nothing.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> Data { get; init; } = new Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>>();

    /// <summary>The data of the location itself (its proxy); null for none.</summary>
    public IReadOnlyList<ContentField>? ProxyData { get; init; }

    /// <summary>The smallest scale of the added objects on each axis, when it is drawn (it differs from the largest); else null.</summary>
    public NVector3? ScaleMin { get; init; }

    /// <summary>The largest scale of the added objects on each axis, with <see cref="ScaleMin"/>.</summary>
    public NVector3 ScaleMax { get; init; } = NVector3.One;

    /// <summary>One draw for the three axes (else one an axis).</summary>
    public bool ScaleUniform { get; init; } = true;

    /// <summary>Metres the objects are raised above the location's spot ("offset", else "groundOffset").</summary>
    public float Offset { get; init; }

    /// <summary>The location turned to a random cardinal direction, drawn from its seed, in place of its usual turn.</summary>
    public bool RandomCardinal { get; init; }

    /// <summary>The ground of its zone levelled to its spot ("levelArea"), when it is; else null.</summary>
    public LevelArea? Level { get; init; }

    /// <summary>The ground of its zone painted round its spot ("paint"), when it is; else null.</summary>
    public PaintArea? Paint { get; init; }

    /// <summary>Why the game must make the location's zone itself; null when the editor can build it.</summary>
    public string? LeftToGame { get; init; }
}

/// <summary>
/// Ground Expand World Data levels to a location's spot before it spawns (its Terrain.Level): fully within the radius,
/// less and less over the border beyond it.
/// </summary>
/// <param name="Radius">Metres levelled fully.</param>
/// <param name="Border">Metres of blend beyond.</param>
public sealed record LevelArea(float Radius, float Border);

/// <summary>
/// Ground Expand World Data paints round a location's spot before it spawns (its Terrain.Paint): this colour of the
/// paint mask (r dirt, g cultivated, b paved, a vegetation allowed) fully within the radius, blended over the border.
/// </summary>
/// <param name="Radius">Metres painted fully.</param>
/// <param name="Border">Metres of blend beyond.</param>
/// <param name="Color">The paint's r, g and b.</param>
/// <param name="Alpha">The paint's a.</param>
public sealed record PaintArea(float Radius, float Border, NVector3 Color, float Alpha);

/// <summary>An object a location's file adds ("objects": prefab, position, turn, scale, chance, data).</summary>
/// <param name="Prefab">The prefab (or a swap's source name).</param>
/// <param name="Position">Its place in the location's frame (x, y, z), metres.</param>
/// <param name="Euler">Its turn in the location's frame, Unity Euler angles in degrees.</param>
/// <param name="Scale">Its own scale on each axis.</param>
/// <param name="Data">Its own data; null for none.</param>
/// <param name="Chance">The chance (0..1) it is made (drawn only below 1).</param>
/// <param name="Snap">Its height set to the ground's.</param>
public sealed record ContentObject(string Prefab, NVector3 Position, NVector3 Euler, NVector3 Scale, IReadOnlyList<ContentField>? Data, float Chance, bool Snap);

/// <summary>One value of an object's data.</summary>
/// <param name="Section">floats, vec3, quats, ints, longs, strings or bytes.</param>
/// <param name="Key">The key's hash.</param>
/// <param name="Value">The value as text (numbers; "x y z" for vec3; "x y z w" for quats; base64 for bytes).</param>
public sealed record ContentField(string Section, int Key, string Value);

/// <summary>A choice with its weight.</summary>
/// <typeparam name="T">The choice.</typeparam>
/// <param name="Weight">Its weight.</param>
/// <param name="Value">The choice.</param>
public readonly record struct Weighted<T>(float Weight, T Value);
