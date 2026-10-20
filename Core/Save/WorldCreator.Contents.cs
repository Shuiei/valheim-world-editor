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
    /// the file says, drawing on from the rolls; then, from the zone's seed again, the added objects' scale, and each
    /// added object (its chance, its swap and data, its place on the ground when it snaps). Each building piece wakes
    /// as the game's WearNTear does: its random initial damage when the location gives one, then its two timers; the
    /// draws other components make as they wake are not counted, so a later draw can differ from the game's after one.
    /// </summary>
    /// <param name="prefab">The location's prefab.</param>
    /// <param name="contents">What the file changes.</param>
    /// <param name="at">Where and how it is placed.</param>
    /// <param name="terrain">The world's terrain (the ground an added object snaps to).</param>
    /// <returns>Each object with where it stands.</returns>
    private static List<(Vector3 At, byte[] Zdo)> Built(LocationPrefabs.Prefab prefab, LocationContents contents, Placing at, TerrainService terrain)
    {
        var (position, rotation, zoneSeed, damage) = at;
        var raised = position with { Y = position.Y + contents.Offset };
        var objects = new List<(Vector3 At, byte[] Zdo)>();
        Rnd.RandomState was = Rnd.State;
        try
        {
            var made = LocationPrefabs.Made(prefab, raised, rotation, zoneSeed, out var rolled);
            Rnd.State = rolled;
            foreach (var m in made)
            {
                Add(objects, contents, damage, new(m.Prefab, m.Position, m.Rotation, Vector3.One), null);
            }
            Rnd.InitState(zoneSeed);
            Vector3 scale = DrawScale(contents);
            foreach (var o in contents.Objects)
            {
                if (o.Chance < 1f && Rnd.Value > o.Chance)
                {
                    continue;
                }
                var spot = raised + Vector3.Transform(o.Position, rotation);
                if (o.Snap)
                {
                    spot.Y = GroundHeight(terrain, spot);
                }
                Add(objects, contents, damage, new(o.Prefab, spot, Quaternion.Normalize(rotation * BlueprintFormats.FromEuler(o.Euler)), scale * o.Scale), o.Data);
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return objects;
    }

    /// <summary>How a location is placed: its spot, its turn, the zone's seed and the damage its pieces wake with.</summary>
    /// <param name="Position">Its spot on the ground.</param>
    /// <param name="Rotation">Its turn.</param>
    /// <param name="ZoneSeed">The zone's seed.</param>
    /// <param name="Damage">The random initial damage of its pieces.</param>
    internal readonly record struct Placing(Vector3 Position, Quaternion Rotation, int ZoneSeed, Damage Damage);

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

    /// <summary>One object to make: its prefab, place, turn and scale.</summary>
    /// <param name="Prefab">Its prefab (before any swap).</param>
    /// <param name="At">Where it stands.</param>
    /// <param name="Rotation">Its turn.</param>
    /// <param name="Scale">Its scale on each axis.</param>
    private readonly record struct Made(string Prefab, Vector3 At, Quaternion Rotation, Vector3 Scale);

    /// <summary>
    /// One object as the mod makes it (Create.SharedBPO): its prefab swapped (one draw when there are several choices;
    /// none for "remove"), then given the file's data for "all" and for its prefab (a draw each when there are several
    /// choices), its own data over them; then it wakes (<see cref="Wake"/>).
    /// </summary>
    private static void Add(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, Damage damage, Made made, IReadOnlyList<ContentField>? own)
    {
        var (prefab, at, rotation, scale) = made;
        string name = contents.Swaps.TryGetValue(prefab, out var swaps) ? Pick(swaps) ?? "" : prefab;
        if (name == "")
        {
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
        if (damage == Damage.RandomOverData)
        {
            fields.RemoveAll(IsHealth);
        }
        int hash = StableHash.Of(name);
        if (Terrain.PrefabCatalog.Get(hash) is not { } info)
        {
            return;
        }
        Wake(info.WearHealth, fields, damage);
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
    /// Nothing for other objects (a full health of 0).
    /// </summary>
    private static void Wake(float full, List<ContentField> fields, Damage damage)
    {
        if (full <= 0f)
        {
            return;
        }
        if (damage != Damage.None && AtFull(fields, full))
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
