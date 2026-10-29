using System.Numerics;

namespace TerrainEditor.Terrain;

/// <summary>
/// Runtime terrain modifiers (TerrainModifier components) that the game applies to every zone's
/// heightmap before the saved terrain edits: the flattening around locations, plus a few network
/// prefabs. Their settings are read from the player's own Valheim (GameData; the tests use
/// editor/tests/fixtures/terrain-modifiers.json): the editor keeps no copy, which would go out of date.
/// Placement comes from the world save. The maths mirrors Heightmap.ApplyModifiers.
/// </summary>
public sealed partial class TerrainModifiers
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
    /// <param name="PaintType">What it paints.</param>
    /// <param name="PaintStrength">How far it blends towards its paint (1: all the way at its middle).</param>
    /// <param name="PaintHeightCheck">Whether it leaves the ground above it unpainted.</param>
    /// <param name="SortOrder">The game's order among modifiers of the same kind.</param>
    /// <param name="Player">A player-made modifier: applied after the others, its levelling held to the ground ±8 m
    /// and its smoothing to the levelled ground ±1 m.</param>
    /// <param name="CreationTime">The object's creation time (0 for locations), which orders modifiers of the same
    /// sort order.</param>
    /// <param name="Disc">One of the editor's own ground discs for No limit ground (Uplift).</param>
    public sealed record Modifier(
        Vector3 Position, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower,
        bool PaintCleared, float PaintRadius, Paint PaintType, float PaintStrength, bool PaintHeightCheck, int SortOrder, bool Player, long CreationTime,
        bool Disc = false)
    {
        /// <summary>
        /// The largest radius of what it does (TerrainModifier.GetRadius).
        /// </summary>
        public float Radius => Math.Max(Level ? LevelRadius : 0f, Math.Max(Smooth ? SmoothRadius : 0f, PaintCleared ? PaintRadius : 0f));
    }

    /// <summary>
    /// What a modifier paints (TerrainModifier.PaintType, in the game's order).
    /// </summary>
    public enum Paint
    {
        /// <summary>Dirt (the paint mask's red).</summary>
        Dirt,

        /// <summary>Cultivated ground (green).</summary>
        Cultivate,

        /// <summary>Paved ground (blue).</summary>
        Paved,

        /// <summary>The biome's own ground: no dirt, cultivated or paved.</summary>
        Reset,

        /// <summary>No paint and no vegetation (all four channels to 0).</summary>
        ClearVegetation,

        /// <summary>Deep snow (red, green and blue all full).</summary>
        DeepSnow,
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
    /// <param name="PaintType">What it paints.</param>
    /// <param name="PaintStrength">How far it blends towards its paint.</param>
    /// <param name="PaintHeightCheck">Whether it leaves the ground above it unpainted.</param>
    /// <param name="SortOrder">The game's sort order.</param>
    /// <param name="Player">A player-made modifier.</param>
    internal sealed record Template(Vector3 Offset, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower, bool PaintCleared, float PaintRadius,
        Paint PaintType, float PaintStrength, bool PaintHeightCheck, int SortOrder, bool Player);

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
    /// The modifiers that touch zone (zx, zz), in the order the game applies them; empty when none.
    /// </summary>
    public IReadOnlyList<Modifier> InZone(int zx, int zz) => _byZone.TryGetValue((zx, zz), out var list) ? list : Array.Empty<Modifier>();

    /// <summary>Whether one of the editor's ground discs (saved No limit ground) touches zone (zx, zz).</summary>
    public bool HasDiscs(int zx, int zz) => InZone(zx, zz).Any(m => m.Disc);

    /// <summary>
    /// Applies the zone's modifiers to its 65x65 base heights (in place), and their cleared paint to its base paint
    /// mask when one is given. Returns the heights the saved edits are clamped against (TerrainComp.ApplyToHeightmap's
    /// baseHeights).
    /// </summary>
    /// <param name="zx">Zone x.</param>
    /// <param name="zz">Zone z.</param>
    /// <param name="heights">The zone's generated heights, changed in place.</param>
    /// <param name="mask">The zone's generated paint mask (65x65 RGBA), painted in place; null: heights only.</param>
    /// <param name="discs">False: the editor's ground discs are left out (the ground as the world generated it).</param>
    /// <returns>The heights before the first player-made modifier; a copy of the result when there is none.</returns>
    public float[] Apply(int zx, int zz, float[] heights, float[]? mask = null, bool discs = true)
    {
        float[]? baseHeights = null, levelOnly = null;
        foreach (Modifier m in InZone(zx, zz).Where(m => discs || !m.Disc))
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
            // A ground disc's dirt is never seen: Uplift paints the ground under it back with saved paint.
            if (mask != null && m.PaintCleared && !m.Disc)
            {
                PaintCleared(zx, zz, heights, mask, m);
            }
        }
        return baseHeights ?? (float[])heights.Clone();
    }

    /// <summary>
    /// Metres per zone side.
    /// </summary>
    private const int Width = 64;

    /// <summary>
    /// UnityEngine.Vector2.Distance in single precision.
    /// </summary>
    private static float Distance(int ax, int ay, int bx, int by)
    {
        float fx = ax - bx, fy = ay - by;
        return (float)Math.Sqrt(fx * fx + fy * fy);
    }
}
