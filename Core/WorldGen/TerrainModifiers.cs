using System.Numerics;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Runtime terrain modifiers (TerrainModifier components) that the game applies to every zone's
/// heightmap before the saved terrain edits: the flattening around locations, plus a few network
/// prefabs. Their settings are read from the player's own Valheim (GameData; the tests use
/// tests/fixtures/terrain-modifiers.json): the editor keeps no copy, which would go out of date.
/// Placement comes from the world save. The maths mirrors Heightmap.ApplyModifiers.
/// </summary>
public sealed class TerrainModifiers
{
    /// <summary>
    /// One modifier placed in the world (a TerrainModifier's settings at its world position).
    /// </summary>
    /// <param name="Position">World position, metres.</param>
    /// <param name="Level">Whether it levels the ground to its height.</param>
    /// <param name="LevelOffset">Height above <paramref name="Position"/> it levels and smooths to.</param>
    /// <param name="LevelRadius">Radius it levels, metres.</param>
    /// <param name="Square">Whether the levelled area is a square rather than a disc.</param>
    /// <param name="Smooth">Whether it blends the ground towards its height.</param>
    /// <param name="SmoothRadius">Radius it blends, metres.</param>
    /// <param name="SmoothPower">How the blend falls off towards the edge (exponent of the distance as a share of the radius).</param>
    /// <param name="PaintCleared">Whether it paints the ground cleared.</param>
    /// <param name="PaintRadius">Radius it paints, metres.</param>
    /// <param name="SortOrder">The game's order among modifiers of the same kind.</param>
    /// <param name="Player">A player-made modifier: applied after the others, its levelling held to the ground ±8 m
    /// and its smoothing to the levelled ground ±1 m.</param>
    /// <param name="CreationTime">The object's creation time (0 for locations), which orders modifiers of the same
    /// sort order.</param>
    /// <param name="Disc">One of the editor's own ground discs for No limit ground (Uplift).</param>
    public sealed record Modifier(
        Vector3 Position, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower,
        bool PaintCleared, float PaintRadius, int SortOrder, bool Player, long CreationTime, bool Disc = false)
    {
        /// <summary>
        /// The largest radius of what it does (TerrainModifier.GetRadius).
        /// </summary>
        public float Radius => Math.Max(Level ? LevelRadius : 0f, Math.Max(Smooth ? SmoothRadius : 0f, PaintCleared ? PaintRadius : 0f));
    }

    /// <summary>
    /// A modifier as a prefab has it, before it is placed: the same settings as <see cref="Modifier"/>.
    /// </summary>
    /// <param name="Offset">Position in the prefab's own frame.</param>
    /// <param name="Level">Whether it levels.</param>
    /// <param name="LevelOffset">Height offset it levels to.</param>
    /// <param name="LevelRadius">Radius it levels.</param>
    /// <param name="Square">Square levelled area.</param>
    /// <param name="Smooth">Whether it smooths.</param>
    /// <param name="SmoothRadius">Radius it smooths.</param>
    /// <param name="SmoothPower">Smoothing falloff exponent.</param>
    /// <param name="PaintCleared">Whether it paints cleared ground.</param>
    /// <param name="PaintRadius">Radius it paints.</param>
    /// <param name="SortOrder">The game's sort order.</param>
    /// <param name="Player">A player-made modifier.</param>
    private sealed record Template(Vector3 Offset, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower, bool PaintCleared, float PaintRadius, int SortOrder, bool Player);

    /// <summary>
    /// Prefabs and locations with modifiers by name hash. None until Use: no world opens before then (GameData).
    /// </summary>
    private static volatile Dictionary<int, (string Name, bool Location, List<Template> Mods)> _prefabs = [];

    /// <summary>
    /// Whether the game's modifiers are known (Use was given them).
    /// </summary>
    public static bool Loaded { get; private set; }

    /// <summary>
    /// The modifiers of the game's prefabs, as terrain-modifiers.json has them (name -&gt; location flag
    /// and modifiers): from the game (GameData) or the tests' copy.
    /// </summary>
    public static void Use(string json)
    {
        _prefabs = Parse(json);
        Loaded = true;
    }

    /// <summary>
    /// Hashes of the network prefabs (not locations) that carry modifiers, so the save reader keeps their objects.
    /// </summary>
    public static IEnumerable<int> NetworkPrefabHashes => _prefabs.Where(p => !p.Value.Location).Select(p => p.Key);

    /// <summary>
    /// Whether the prefab is a mark players' tools leave on the ground (raise, digg, path...): a network prefab whose
    /// modifiers are all player-made. Other modifier prefabs (rock4_coast, HeathRockPillar...) are things to see.
    /// </summary>
    /// <param name="prefab">The prefab hash.</param>
    public static bool IsGroundMark(int prefab) =>
        _prefabs.TryGetValue(prefab, out var p) && !p.Location && p.Mods.Count > 0 && p.Mods.TrueForAll(m => m.Player);

    /// <summary>
    /// The modifiers touching each zone, in the order the game applies them.
    /// </summary>
    private readonly Dictionary<(int, int), List<Modifier>> _byZone = [];

    /// <summary>
    /// How many modifiers were placed in all.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// How many placed locations carry modifiers.
    /// </summary>
    public int LocationsWithModifiers { get; }

    /// <summary>
    /// The modifiers of a world's placed locations and objects.
    /// </summary>
    public TerrainModifiers(WorldSave world) : this(world.Placed)
    {
    }

    /// <summary>
    /// From any set of placed objects (Uplift: the world's with ground discs added or taken away).
    /// </summary>
    public TerrainModifiers(IEnumerable<PlacedObject> placed)
    {
        List<Modifier> all = [];
        foreach (PlacedObject p in placed)
        {
            int key = p.Location != 0 ? p.Location : p.Prefab;
            if (!_prefabs.TryGetValue(key, out var prefab) || prefab.Location != (p.Location != 0))
            {
                continue;
            }
            if (prefab.Location)
            {
                LocationsWithModifiers++;
            }
            // Quaternion.Euler in Unity applies z, then x, then y: the same as yaw/pitch/roll here.
            var rot = Quaternion.CreateFromYawPitchRoll(Deg(p.Rotation.Y), Deg(p.Rotation.X), Deg(p.Rotation.Z));
            foreach (Template t in prefab.Mods)
            {
                Vector3 pos = p.Position + Vector3.Transform(t.Offset, rot);
                all.Add(new Modifier(pos, t.Level, t.LevelOffset, t.LevelRadius, t.Square, t.Smooth, t.SmoothRadius, t.SmoothPower, t.PaintCleared, t.PaintRadius, t.SortOrder, t.Player, prefab.Location ? 0 : p.TimeCreated,
                    prefab.Location && Editing.Uplift.IsDisc(p.Location)));
            }
        }
        // TerrainModifier.SortByModifiers.
        all.Sort((a, b) =>
        {
            if (a.Player != b.Player)
            {
                return a.Player.CompareTo(b.Player);
            }

            if (a.SortOrder != b.SortOrder)
            {
                return a.SortOrder.CompareTo(b.SortOrder);
            }

            if (a.CreationTime != b.CreationTime)
            {
                return a.CreationTime.CompareTo(b.CreationTime);
            }

            return a.Position.LengthSquared().CompareTo(b.Position.LengthSquared());
        });
        foreach (Modifier m in all)
        {
            // Heightmap.TerrainVSModifier: radius + 4 m against the zone square.
            float r = m.Radius + 4f;
            int zx0 = (int)Math.Floor((m.Position.X - r + 32f) / 64f), zx1 = (int)Math.Floor((m.Position.X + r + 32f) / 64f);
            int zz0 = (int)Math.Floor((m.Position.Z - r + 32f) / 64f), zz1 = (int)Math.Floor((m.Position.Z + r + 32f) / 64f);
            for (int zz = zz0; zz <= zz1; zz++)
            {
                for (int zx = zx0; zx <= zx1; zx++)
                {
                    if (Overlaps(m, zx, zz))
                    {
                        (_byZone.TryGetValue((zx, zz), out var list) ? list : _byZone[(zx, zz)] = []).Add(m);
                    }
                }
            }
        }
        Count = all.Count;
    }

    /// <summary>
    /// The modifiers that touch zone (zx, zz), in the order the game applies them; empty when none.
    /// </summary>
    public IReadOnlyList<Modifier> InZone(int zx, int zz) => _byZone.TryGetValue((zx, zz), out var list) ? list : Array.Empty<Modifier>();

    /// <summary>
    /// Degrees to radians.
    /// </summary>
    private static float Deg(float d) => d * (MathF.PI / 180f);

    /// <summary>
    /// Heightmap.TerrainVSModifier: whether the modifier's radius + 4 m reaches the zone's square.
    /// </summary>
    private static bool Overlaps(Modifier m, int zx, int zz)
    {
        float num = m.Radius + 4f;
        float cx = zx * 64f, cz = zz * 64f, half = 32f;
        return !(m.Position.X + num < cx - half || m.Position.X - num > cx + half || m.Position.Z + num < cz - half || m.Position.Z - num > cz + half);
    }

    /// <summary>
    /// Applies the zone's modifiers to its 65x65 base heights (in place). Returns the heights the
    /// saved edits are clamped against (TerrainComp.ApplyToHeightmap's baseHeights).
    /// </summary>
    /// <returns>The heights before the first player-made modifier; a copy of the result when there is none.</returns>
    public float[] Apply(int zx, int zz, float[] heights)
    {
        float[]? baseHeights = null, levelOnly = null;
        foreach (Modifier m in InZone(zx, zz))
        {
            if (m.Player && baseHeights == null)
            {
                baseHeights = (float[])heights.Clone();
                levelOnly = (float[])heights.Clone();
            }
            Vector3 at = m.Position + new Vector3(0f, m.LevelOffset, 0f);
            if (m.Level)
            {
                LevelTerrain(zx, zz, heights, at, m.LevelRadius, m.Square, baseHeights, levelOnly, m.Player);
            }
            if (m.Smooth)
            {
                SmoothTerrain2(zx, zz, heights, at, m.SmoothRadius, levelOnly, m.SmoothPower, m.Player);
            }
        }
        return baseHeights ?? (float[])heights.Clone();
    }

    /// <summary>
    /// Metres per zone side.
    /// </summary>
    private const int Width = 64;

    /// <summary>
    /// Heightmap.LevelTerrain (WorldToVertexMask, square or round area, sets the modifier's height).
    /// </summary>
    private static void LevelTerrain(int zx, int zz, float[] h, Vector3 worldPos, float radius, bool square, float[]? baseHeights, float[]? levelOnly, bool player)
    {
        float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
        int half = (Width + 1) / 2;
        int x = (int)MathF.Floor(dx / 1f + 0.5f + half);
        int y = (int)MathF.Floor(dz / 1f + 0.5f + half);
        float num = (float)((double)radius / 1.0);
        int num2 = (int)MathF.Ceiling(num);
        int n = Width + 1;
        for (int i = y - num2; i <= y + num2; i++)
        {
            for (int j = x - num2; j <= x + num2; j++)
            {
                if ((square || !(Distance(x, y, j, i) > num)) && j >= 0 && i >= 0 && j < n && i < n)
                {
                    float v = worldPos.Y;
                    if (player)
                    {
                        float b = baseHeights![i * n + j];
                        v = levelOnly![i * n + j] = Math.Clamp(v, (float)((double)b - 8.0), (float)((double)b + 8.0));
                    }
                    h[i * n + j] = v;
                }
            }
        }
    }

    /// <summary>
    /// Heightmap.SmoothTerrain2 (WorldToVertex, round area, blend towards the modifier's height).
    /// </summary>
    private static void SmoothTerrain2(int zx, int zz, float[] h, Vector3 worldPos, float radius, float[]? levelOnly, float power, bool player)
    {
        // No reach, nothing to smooth (the middle point's share would be 0 / 0: a NaN height).
        if (radius <= 0f)
        {
            return;
        }
        float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
        int x = (int)MathF.Floor(dx / 1f + 0.5f) + Width / 2;
        int y = (int)MathF.Floor(dz / 1f + 0.5f) + Width / 2;
        float b = worldPos.Y;
        float num = radius / 1f;
        int num2 = (int)MathF.Ceiling(num);
        int n = Width + 1;
        for (int i = y - num2; i <= y + num2; i++)
        {
            for (int j = x - num2; j <= x + num2; j++)
            {
                float d = Distance(x, y, j, i);
                if (d > num)
                {
                    continue;
                }
                float num5 = d / num;
                if (j >= 0 && i >= 0 && j < n && i < n)
                {
                    num5 = power != 3f ? MathF.Pow(num5, power) : (float)((double)num5 * num5 * num5);
                    float t = (float)(1.0 - num5);
                    float v = ValheimGen.GenMath.Lerp(h[i * n + j], b, t);
                    if (player)
                    {
                        float lo = levelOnly![i * n + j];
                        v = Math.Clamp(v, (float)((double)lo - 1.0), (float)((double)lo + 1.0));
                    }
                    h[i * n + j] = v;
                }
            }
        }
    }

    /// <summary>
    /// UnityEngine.Vector2.Distance in single precision.
    /// </summary>
    private static float Distance(int ax, int ay, int bx, int by)
    {
        float fx = ax - bx, fy = ay - by;
        return (float)Math.Sqrt(fx * fx + fy * fy);
    }

    /// <summary>
    /// Reads terrain-modifiers.json into modifiers by prefab name hash.
    /// </summary>
    private static Dictionary<int, (string, bool, List<Template>)> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Dictionary<int, (string, bool, List<Template>)> result = [];
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            List<Template> mods = [];
            foreach (JsonElement m in p.Value.GetProperty("modifiers").EnumerateArray())
            {
                JsonElement pos = m.GetProperty("pos");
                bool B(string k) => m.GetProperty(k).GetInt32() != 0;
                float F(string k) => m.GetProperty(k).GetSingle();
                mods.Add(new Template(new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle()), B("level"), F("levelOffset"), F("levelRadius"), B("square"),
                    B("smooth"), F("smoothRadius"), F("smoothPower"), B("paintCleared"), F("paintRadius"), m.GetProperty("sortOrder").GetInt32(), B("player")));
            }
            result[StableHash.Of(p.Name)] = (p.Name, p.Value.GetProperty("location").GetBoolean(), mods);
        }
        return result;
    }
}
