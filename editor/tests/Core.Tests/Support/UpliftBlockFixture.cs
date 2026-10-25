using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// The one-zone block the uplift point tests use, with the arrays and targets they fill in: the zone at (0, 0), whose
/// middle point is <see cref="Middle"/>.
/// </summary>
internal static class UpliftBlockFixture
{
    /// <summary>The middle point of the zone at (0, 0).</summary>
    public const int Middle = 32 * 64 + 32;

    /// <summary>A block of the one zone at (0, 0).</summary>
    public static Uplift.Block SingleZone() => new([(0, 0)]);

    /// <summary>An array with the same value at every point of the block.</summary>
    public static bool[] Flags(Uplift.Block block, bool value) => [.. Enumerable.Repeat(value, block.N)];

    /// <summary>
    /// Targets asking for the same ground at every point, with no lift, the world's objects standing on the ground asked
    /// for, and every point open to edits.
    /// </summary>
    public static Uplift.BlockTargets Targets(Uplift.Block block, float want, float[]? wantArray = null) => new(
        wantArray ?? [.. Enumerable.Repeat(want, block.N)],
        new float[block.N],
        wantArray ?? [.. Enumerable.Repeat(want, block.N)],
        [],
        Flags(block, true),
        []);
}
