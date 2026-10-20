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
    /// added object (its chance, its swap and data, its place on the ground when it snaps). The draws an object's own
    /// components make as it wakes are not counted, so a draw can differ from the game's where one makes some.
    /// </summary>
    /// <param name="prefab">The location's prefab.</param>
    /// <param name="contents">What the file changes.</param>
    /// <param name="position">Where the location stands (its spot on the ground).</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="zoneSeed">The zone's seed.</param>
    /// <param name="terrain">The world's terrain (the ground an added object snaps to).</param>
    /// <returns>Each object with where it stands.</returns>
    private static List<(Vector3 At, byte[] Zdo)> Built(LocationPrefabs.Prefab prefab, LocationContents contents, Vector3 position, Quaternion rotation, int zoneSeed, TerrainService terrain)
    {
        var raised = position with { Y = position.Y + contents.Offset };
        var objects = new List<(Vector3 At, byte[] Zdo)>();
        Rnd.RandomState was = Rnd.State;
        try
        {
            var made = LocationPrefabs.Made(prefab, raised, rotation, zoneSeed, out var rolled);
            Rnd.State = rolled;
            foreach (var m in made)
            {
                Add(objects, contents, m.Prefab, m.Position, m.Rotation, Vector3.One, null);
            }
            Rnd.InitState(zoneSeed);
            Vector3 scale = DrawScale(contents);
            foreach (var o in contents.Objects)
            {
                if (o.Chance < 1f && Rnd.Value > o.Chance)
                {
                    continue;
                }
                var at = raised + Vector3.Transform(o.Position, rotation);
                if (o.Snap)
                {
                    at.Y = terrain.Generator.GetHeight(new ValheimGen.Vector2(at.X, at.Z));
                }
                Add(objects, contents, o.Prefab, at, Quaternion.Normalize(rotation * BlueprintFormats.FromEuler(o.Euler)), scale * o.Scale, o.Data);
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return objects;
    }

    /// <summary>
    /// One object as the mod makes it (Create.SharedBPO): its prefab swapped (one draw when there are several choices;
    /// none for "remove"), then given the file's data for "all" and for its prefab (a draw each when there are several
    /// choices), its own data over them.
    /// </summary>
    private static void Add(List<(Vector3 At, byte[] Zdo)> objects, LocationContents contents, string prefab, Vector3 at, Quaternion rotation, Vector3 scale, IReadOnlyList<ContentField>? own)
    {
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
        int hash = StableHash.Of(name);
        if (Terrain.PrefabCatalog.Get(hash) is not { } info)
        {
            return;
        }
        // The mod writes the scale only for an object whose network view keeps it (DataHelper.Init).
        byte[] zdo = ZdoBuilder.Blank(hash, info.Flags, at, BlueprintFormats.ToEuler(rotation), info.SyncScale ? scale : Vector3.One);
        objects.Add((at, WithFields(zdo, fields)));
    }

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
