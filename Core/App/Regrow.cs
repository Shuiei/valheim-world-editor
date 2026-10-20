using System.Text.Json;
using TerrainEditor.Editing;
using ValheimGen;
using Rnd = ValheimGen.UnityEngine.Random;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

/// <summary>
/// "Regrow nature": the trees, rocks, bushes and pickables the game would put in a zone, by its own
/// vegetation rules (ZoneSystem.m_vegetation and the LocationLists, read from the player's Valheim:
/// GameData; the tests use tests/fixtures/vegetation.json) and the same random draws
/// (ZoneSystem.PlaceVegetation, Unity's Random seeded per zone and rule), on the ground as it is now. What
/// the editor cannot know is skipped: the physics checks against objects already standing (IsBlocked,
/// snapping onto rocks) and clear areas around locations; the alt biomes are laid out as the game lays them out
/// (SeedLocations.AltBiomeLayout), their own rules growing in their areas only. The page drops spots
/// taken by objects that are there and keeps those in the selection. A world whose rules carry Expand World Data's
/// vegetation (<see cref="WorldRules.Vegetation"/>) is regrown by those rules instead of the game's.
/// </summary>
public static partial class Regrow
{

    /// <summary>The game's vegetation rules, in its order. None until Use: no world opens before then (GameData).</summary>
    private static volatile List<Rule> _rules = [];

    /// <summary>The rules as GameCatalogues.VegetationJson writes them.</summary>
    public static void Use(string json) => _rules = JsonSerializer.Deserialize<List<Rule>>(json)!;

    /// <summary>How many of the game's vegetation rules are loaded (0 before <see cref="Use"/>).</summary>
    public static int RuleCount => _rules.Count;

    /// <summary>The game's vegetation rules as loaded (what Expand World Data's file is matched against).</summary>
    public static IReadOnlyList<Rule> GameRules => _rules;

    /// <summary>The rules a world is regrown by: its own (Expand World Data's vegetation file), else the game's.</summary>
    private static IReadOnlyList<Rule> RulesFor(TerrainService terrain) => terrain.Generator.Rules.Vegetation ?? _rules;

    /// <summary>
    /// Zones read (biomes, paint mask, ocean depths) on this thread so far: a test seam, each zone is read once per
    /// <see cref="Zones"/> call.
    /// </summary>
    [field: ThreadStatic]
    internal static int ZoneReads { get; private set; }

    /// <summary>One object the game would place: prefab, Unity world position, Euler rotation (degrees), scale.</summary>
    /// <param name="Name">The prefab.</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Y">Height (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="Rx">Rotation about x (degrees).</param>
    /// <param name="Ry">Rotation about y (degrees).</param>
    /// <param name="Rz">Rotation about z (degrees).</param>
    /// <param name="Scale">Uniform scale (the x scale when the object is scaled by axis).</param>
    /// <param name="Axes">The scale on each axis (x, y, z) when Expand World Data scales the object by axis; null otherwise.</param>
    public sealed record Spot(string Name, float X, float Y, float Z, float Rx, float Ry, float Rz, float Scale, float[]? Axes = null)
    {
        /// <summary>The scale on each axis (x, y, z): the axes' when the rule scales by axis, else the uniform scale on all three.</summary>
        public System.Numerics.Vector3 Scale3 => Axes is { Length: 3 } a ? new(a[0], a[1], a[2]) : new(Scale, Scale, Scale);
    }

    /// <summary>
    /// What the game would place in zones x0..x1, z0..z1 on the current ground. Kinds the editor cannot
    /// create (no networked object) are left out by the caller.
    /// </summary>
    /// <param name="terrain">The world's terrain (biomes, paint mask).</param>
    /// <param name="edits">The session's edits, for the ground as it is now.</param>
    /// <param name="seed">The world's seed.</param>
    /// <param name="x0">First zone column.</param>
    /// <param name="z0">First zone row.</param>
    /// <param name="x1">Last zone column (inclusive).</param>
    /// <param name="z1">Last zone row (inclusive).</param>
    public static List<Spot> Zones(TerrainService terrain, EditStore edits, int seed, int x0, int z0, int x1, int z1)
    {
        // The ground one zone around too: groups reach up to 20 m past their zone.
        var (w, h, heights) = HeightGrid.Read(terrain, edits, x0 - 1, z0 - 1, x1 + 1, z1 + 1);
        Ground ground = new(heights, w, h, (x0 - 1) * 64f - 32f, (z0 - 1) * 64f - 32f);
        List<Spot> spots = [];
        // Each zone's biomes, paint mask and ocean depths, made once for the whole call: groups reach into neighbours.
        Dictionary<(int, int), Zone> zones = [];
        IReadOnlyList<Rule> rules = RulesFor(terrain);
        var alts = new AltCheck(terrain);
        // Unity's Random has one state per thread: put back what this thread had.
        Rnd.RandomState saved = Rnd.State;
        try
        {
            for (int zz = z0; zz <= z1; zz++)
            {
                for (int zx = x0; zx <= x1; zx++)
                {
                    PlaceZone(terrain, ground, zones, (rules, alts), seed, zx, zz, spots);
                }
            }
        }
        finally
        {
            Rnd.State = saved;
        }
        return spots;
    }
}
