using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// No limit: ground beyond the game's ±8 m. The game clamps its saved ground edits (TerrainComp) to
/// the original ground ± 8 m, but not the flattening of locations: every player's game (console too)
/// levels a location's ground before it takes the original ground. So the editor places invisible
/// locations of the game's own (DevGround1: no model, it only levels a disc of 8 m radius to its
/// height and blends 5 m around it) as location proxies, and the game counts their ground as
/// generated. A shape sculpted past the limit (ZoneEdit.Lift) becomes, when saving or applying:
/// discs on a grid whose heights are fitted by working out the ground exactly as the game does
/// (TerrainModifiers), the rest as ordinary ground edits (within ±8 m of the new ground), the discs'
/// dirt painted over with the biome's own ground, and the trees, rocks and bushes where the ground
/// moved far taken away. The discs are worked out again wherever the ground is lifted again.
/// </summary>
public static partial class Uplift
{
    /// <summary>The game location used as a ground disc.</summary>
    public const string DiscName = "DevGround1";

    /// <summary>The disc location's name hash.</summary>
    public static readonly int Disc = StableHash.Of(DiscName);

    /// <summary>Whether a location (name hash) is a ground disc.</summary>
    public static bool IsDisc(int location) => location == Disc;

    /// <summary>DevGround1's TerrainModifier (as the game has it, GameData): level radius, smoothing radius, paint radius (metres).</summary>
    public const float DiscRadius = 8f, DiscReach = 12.96f, PaintRadius = 9.19f;

    /// <summary>The modifier's place in the location (it decides the order the game applies discs in).</summary>
    private static readonly Vector3 DiscOffset = new(-0.0006f, 0f, 0.1006f);

    /// <summary>Grid of disc middles (m), within reach of the ground they make.</summary>
    public const int Spacing = 6;

    /// <summary>A point further than this (metres) from the ground without discs needs one (the edits make up the rest).</summary>
    private const float Need = 5f;

    /// <summary>How many times the disc heights are fitted against the ground the game would make, and the passes over the discs each time.</summary>
    private const int Rounds = 8, Sweeps = 12;
    /// <summary>How much a disc resists moving (for discs with few points).</summary>
    private const double Damping = 0.25;

    /// <summary>Each fitting step moves a disc this share of the way (the game's order shifts as discs move).</summary>
    private const double Step = 0.8;

    /// <summary>Natural objects further than this (metres) from the ground they stood on are taken away.</summary>
    public const float ClearHeight = 2f;

    /// <summary>The object data keys of a location proxy's location and seed.</summary>
    private static readonly int LocationKey = StableHash.Of("location"), SeedKey = StableHash.Of("seed");

    /// <summary>What a save or an apply does to the edits.</summary>
    /// <param name="Zones">The zones' new ground.</param>
    /// <param name="Delete">Objects to take away (old discs, natural objects), by id.</param>
    /// <param name="Add">The new discs.</param>
    /// <param name="Discs">How many discs there are now.</param>
    /// <param name="OldDiscs">How many discs they replace.</param>
    /// <param name="Cleared">How many natural objects are taken away.</param>
    /// <param name="Worst">
    /// The largest difference left between the ground asked for and what the game will make (beyond the
    /// edits' ±8 m).
    /// </param>
    public sealed record Plan(List<ZoneEdit> Zones, List<int> Delete, List<NewObject> Add, int Discs, int OldDiscs, int Cleared, float Worst)
    {
        /// <summary>Lifted zones the game has not generated yet: their lift was dropped.</summary>
        public int Ungenerated { get; init; }

        /// <summary>
        /// The plan in a sentence for the user, in both languages: discs, objects taken away, ground still off, zones
        /// left out.
        /// </summary>
        public Said Describe()
        {
            var discs = Said.Join(", ",
            [
                OldDiscs > 0 ? Said.Of("save.lift.discsReplaced", Discs, OldDiscs) : Said.Of("save.lift.discs", Discs),
                Cleared > 0 ? Said.Of("save.lift.cleared", Cleared) : Said.Empty,
            ]);
            return Said.Join("; ",
            [
                discs,
                Worst > EditStore.MaxLevel + 0.5f ? Said.Of("save.lift.steep", Worst - EditStore.MaxLevel) : Said.Empty,
                Ungenerated > 0 ? Said.Of("save.lift.ungenerated", Ungenerated) : Said.Empty,
            ]);
        }
    }
}
