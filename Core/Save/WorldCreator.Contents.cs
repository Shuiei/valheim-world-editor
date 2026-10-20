using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimGen;
using Rnd = ValheimGen.UnityEngine.Random;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Save;

// A location whose contents Expand World Data's file changes, built as the mod spawns it.
public static partial class WorldCreator
{
    /// <summary>
    /// The objects of a location as the game makes them with Expand World Data's file (its SpawnLocation patches): raised
    /// by the file's offset; the location's own objects (rolled as the game rolls them) each swapped and given data as
    /// the file says, drawing on from the rolls; then, from the zone's seed again, the added objects' scale, a blueprint
    /// location's parts (<see cref="AddParts"/>), and each added object (its chance, its swap and data, its place on the
    /// ground when it snaps). Each object makes the draws its scripts make as it wakes (<see cref="Terrain.WakeDraws"/>), a
    /// building piece's as the game's WearNTear does (<see cref="Wake"/>).
    /// </summary>
    /// <param name="prefab">The location's prefab; null for a blueprint location (it has none).</param>
    /// <param name="contents">What the file changes.</param>
    /// <param name="at">Where and how it is placed.</param>
    /// <param name="terrain">The world's terrain (the ground an added object snaps to).</param>
    /// <returns>Each object with where it stands.</returns>
    private static List<(Vector3 At, byte[] Zdo)> Built(LocationPrefabs.Prefab? prefab, LocationContents contents, Placing at, TerrainService terrain)
    {
        var raised = at.Position with { Y = at.Position.Y + contents.Offset };
        var objects = new List<(Vector3 At, byte[] Zdo)>();
        Rnd.RandomState was = Rnd.State;
        try
        {
            if (prefab != null)
            {
                AddOwn(objects, contents, prefab, at with { Position = raised }, terrain);
            }
            Rnd.InitState(at.ZoneSeed);
            Vector3 scale = DrawScale(contents);
            if (contents.Blueprint is { } blueprint)
            {
                AddParts(objects, contents, new Waker(at.Damage, terrain), contents.Blueprints![blueprint], new(blueprint, raised, at.Rotation, scale));
            }
            AddObjects(objects, contents, at with { Position = raised }, scale, terrain);
        }
        finally
        {
            Rnd.State = was;
        }
        return objects;
    }

    /// <summary>
    /// The location's own objects, rolled from the zone's seed as the game rolls them, each swapped and given data as
    /// the file says (drawing on from the rolls); one with a SnapToGround, not swapped, on the ground it snaps to.
    /// </summary>
    private static void AddOwn(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, LocationPrefabs.Prefab prefab, Placing at, TerrainService terrain)
    {
        var made = LocationPrefabs.Made(prefab, at.Position, at.Rotation, at.ZoneSeed, out var rolled);
        Rnd.State = rolled;
        var ground = new SnapGround(terrain, prefab.Hash, at.Position, at.Rotation);
        var waker = new Waker(at.Damage, terrain);
        foreach (var m in made)
        {
            Add(objects, contents, waker, new(m.Prefab, OnGround(m, contents, ground), m.Rotation, Vector3.One), null);
        }
    }

    /// <summary>
    /// Where one of the location's own objects ends up: on the ground at its offset when it has a SnapToGround and the
    /// file does not swap it (another prefab may not snap), else where the location puts it.
    /// </summary>
    private static Vector3 OnGround(Dungeons.Made m, LocationContents contents, SnapGround ground) =>
        m.Snap is { } offset && !contents.Swaps.ContainsKey(m.Prefab) ? m.Position with { Y = ground.HeightAt(m.Position) + offset } : m.Position;

    /// <summary>
    /// The objects the file adds (LocationSpawning.CustomObjects): each by its chance, at its place from the location's
    /// (on the ground when it snaps), turned with it and scaled by the drawn scale times its own.
    /// </summary>
    private static void AddObjects(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, Placing at, Vector3 scale, TerrainService terrain)
    {
        var waker = new Waker(at.Damage, terrain);
        foreach (var o in contents.Objects)
        {
            if (o.Chance < 1f && Rnd.Value > o.Chance)
            {
                continue;
            }
            var spot = at.Position + Vector3.Transform(o.Position, at.Rotation);
            if (o.Snap)
            {
                spot.Y = SnapHeight(at.Ground, terrain, spot);
            }
            Add(objects, contents, waker, new(o.Prefab, spot, Quaternion.Normalize(at.Rotation * BlueprintFormats.FromEuler(o.Euler)), scale * o.Scale), o.Data);
        }
    }

    /// <summary>
    /// A blueprint's parts as the mod spawns them (Create.Blueprint, Create.SharedBPO): each by its chance, at its place
    /// from the blueprint's turned with it, turned and scaled with it, swapped and given data as the location's own
    /// objects are (<see cref="Add"/>, where a blueprint in a part's place spawns its parts in turn).
    /// </summary>
    /// <param name="objects">Where the objects made go.</param>
    /// <param name="contents">What the file changes.</param>
    /// <param name="waker">How its objects wake.</param>
    /// <param name="parts">The blueprint's parts.</param>
    /// <param name="at">The blueprint's place, turn and scale (its name as the prefab).</param>
    private static void AddParts(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, Waker waker, IReadOnlyList<BlueprintPart> parts, Made at)
    {
        foreach (var part in parts)
        {
            if (part.Chance < 1f && Rnd.Value > part.Chance)
            {
                continue;
            }
            Add(objects, contents, waker, new(part.Prefab, at.At + Vector3.Transform(part.Position, at.Rotation), at.Rotation * part.Rotation, at.Scale * part.Scale), part.Data);
        }
    }

    /// <summary>How a location is placed: its spot, its turn, the zone's seed and the damage its pieces wake with.</summary>
    /// <param name="Position">Its spot on the ground.</param>
    /// <param name="Rotation">Its turn.</param>
    /// <param name="ZoneSeed">The zone's seed.</param>
    /// <param name="Damage">The random initial damage of its pieces.</param>
    /// <param name="Ground">Its zone as the file levels it, when it does (what added objects snap to).</param>
    internal readonly record struct Placing(Vector3 Position, Quaternion Rotation, int ZoneSeed, Damage Damage, LevelledZone? Ground = null);

    /// <summary>The random initial damage a location's building pieces wake with (WearNTear.m_randomInitialDamage).</summary>
    internal enum Damage
    {
        /// <summary>None: at full health.</summary>
        None,
        /// <summary>A random health, unless their data gives one.</summary>
        Random,
        /// <summary>A random health even when their data gives one: no object keeps its data's (Expand World Data's "all").</summary>
        RandomOverData,
    }

    /// <summary>How a location's objects wake: the damage its pieces wake with, and the world (a snow-melting fire draws in the Deep North).</summary>
    /// <param name="Damage">The random initial damage of its pieces.</param>
    /// <param name="Terrain">The world's terrain.</param>
    private readonly record struct Waker(Damage Damage, TerrainService Terrain);

    /// <summary>One object to make: its prefab, place, turn and scale.</summary>
    /// <param name="Prefab">Its prefab (before any swap).</param>
    /// <param name="At">Where it stands.</param>
    /// <param name="Rotation">Its turn.</param>
    /// <param name="Scale">Its scale on each axis.</param>
    private readonly record struct Made(string Prefab, Vector3 At, Quaternion Rotation, Vector3 Scale);

    /// <summary>
    /// One object as the mod makes it (Create.SharedBPO): its prefab swapped (one draw when there are several choices;
    /// none for "remove"); a name of no prefab spawns the blueprint of that name in its place, or nothing; then given
    /// the file's data for "all" and for its prefab (a draw each when there are several choices), its own data over
    /// them; then it wakes (<see cref="Terrain.WakeDraws"/>, a building piece as <see cref="Wake"/> says).
    /// </summary>
    private static void Add(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, Waker waker, Made made, IReadOnlyList<ContentField>? own)
    {
        var (prefab, at, rotation, scale) = made;
        string name = contents.Swaps.TryGetValue(prefab, out var swaps) ? Pick(swaps) ?? "" : prefab;
        int hash = StableHash.Of(name);
        if (name == "" || Terrain.PrefabCatalog.Get(hash) is not { } info)
        {
            if (contents.Blueprints?.GetValueOrDefault(name) is { } inner)
            {
                AddParts(objects, contents, waker, inner, made with { Prefab = name });
            }
            return;
        }
        List<ContentField> fields = [];
        foreach (string key in new[] { "all", name })
        {
            if (contents.Data.TryGetValue(key, out var choices) && Pick(choices) is { } data)
            {
                fields.AddRange(data);
            }
        }
        fields.AddRange(own ?? []);
        // "all": no object keeps a health of its data (the mod's Create.IgnoreHealth).
        if (waker.Damage == Damage.RandomOverData)
        {
            fields.RemoveAll(IsHealth);
        }
        Terrain.WakeDraws.Wake(name, new(() => Wake(info.WearHealth, fields, waker.Damage), waker.Terrain.BiomeAt(at.X, at.Z) == Heightmap.Biome.DeepNorth, Terrain.WakeDraws.Looted(fields)));
        // The mod writes the scale only for an object whose network view keeps it (DataHelper.Init).
        byte[] zdo = ZdoBuilder.Blank(hash, info.Flags, at, BlueprintFormats.ToEuler(rotation), info.SyncScale ? scale : Vector3.One);
        objects.Add((at, WithFields(zdo, fields)));
    }

    /// <summary>The data key of an object's health.</summary>
    private static readonly int HealthKey = StableHash.Of("health");

    /// <summary>
    /// What a building piece (an object with a WearNTear, of this full health) draws as it wakes (WearNTear.Awake):
    /// with random initial damage and a health at full (no health in its data, or its full health), a health
    /// between 10% and 60% of full, kept in its data; then its cover timer and its snow damage timer, in every case.
    /// </summary>
    private static void Wake(float full, List<ContentField> fields, Damage damage)
    {
        if (damage != Damage.None && full > 0f && AtFull(fields, full))
        {
            float rolled = Rnd.Range(0.1f * full, full * 0.6f);
            fields.Add(new ContentField("floats", HealthKey, rolled.ToString("R", CultureInfo.InvariantCulture)));
        }
        // The cover timer (m_updateCoverTimer), then the snow damage timer (m_snowDamageTimer).
        Rnd.Range(0f, 4f);
        Rnd.Range(0f, 1f);
    }

    /// <summary>Whether a value is the object's health.</summary>
    private static bool IsHealth(ContentField f) => f.Section == "floats" && f.Key == HealthKey;

    /// <summary>Whether a piece's data leaves it at full health: no health in it, or its full health.</summary>
    private static bool AtFull(List<ContentField> fields, float full) =>
        fields.LastOrDefault(IsHealth) is not { } given || float.Parse(given.Value, CultureInfo.InvariantCulture).Equals(full);

    /// <summary>The object's bytes with these data values set (later values over earlier ones of the same key).</summary>
    private static byte[] WithFields(byte[] zdo, IReadOnlyList<ContentField>? fields)
    {
        if (fields is not { Count: > 0 })
        {
            return zdo;
        }
        var z = ZdoData.Parse(zdo);
        foreach (var f in fields)
        {
            z.Set(f.Section, f.Key, f.Value);
        }
        return z.Serialize();
    }

    /// <summary>
    /// A weighted choice as the mod draws it (Create.RandomizeSwap / RandomizeData): the only one without a draw, else
    /// one draw taken down by each weight until it reaches 0, the last when it never does.
    /// </summary>
    private static T? Pick<T>(IReadOnlyList<Weighted<T>> choices)
    {
        if (choices.Count == 0)
        {
            // A source with no choice: the mod's empty pick (nothing for a swap: the object is removed).
            return typeof(T) == typeof(string) ? (T)(object)"" : default;
        }
        if (choices.Count == 1)
        {
            return choices[0].Value;
        }
        float roll = Rnd.Value;
        foreach (var c in choices)
        {
            roll = (float)((double)roll - c.Weight);
            if (roll <= 0f)
            {
                return c.Value;
            }
        }
        return choices[^1].Value;
    }

    /// <summary>
    /// The added objects' scale (Helper.RandomValue): one draw for the three axes, else one an axis; none when the file
    /// gives one scale.
    /// </summary>
    private static Vector3 DrawScale(LocationContents c)
    {
        if (c.ScaleMin is not { } min)
        {
            return c.ScaleMax;
        }
        Vector3 max = c.ScaleMax;
        if (c.ScaleUniform)
        {
            float t = Rnd.Range(0f, 1f);
            return new Vector3(Lerp(min.X, max.X, t), Lerp(min.Y, max.Y, t), Lerp(min.Z, max.Z, t));
        }
        float x = Rnd.Range(min.X, max.X), y = Rnd.Range(min.Y, max.Y), z = Rnd.Range(min.Z, max.Z);
        return new Vector3(x, y, z);
    }

    /// <summary>a + (b - a) * t, as the mod writes it (in double, rounded once).</summary>
    private static float Lerp(float a, float b, float t) => (float)(a + ((double)b - a) * t);
}
