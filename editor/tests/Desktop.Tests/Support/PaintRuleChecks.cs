using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The paint rules' answers on the five test points of the mask tests: dirt, cultivated, paved, painted black, and never
/// painted, in that order.
/// </summary>
internal static class PaintRuleChecks
{
    private static readonly bool[] Expected6 = [true, true, true, true, true];
    private static readonly bool[] Expected5 = [false, false, false, true, true];
    private static readonly bool[] Expected4 = [true, true, true, false, false];
    private static readonly bool[] Expected3 = [true, false, false, false, false];
    private static readonly bool[] Expected2 = [false, true, false, false, false];
    private static readonly bool[] Expected1 = [false, false, true, false, false];
    /// <summary>
    /// Asserts which of the five test points each paint rule lets through.
    /// </summary>
    /// <param name="passes">Whether each of the five points passes under a paint rule, in point order.</param>
    internal static void AssertFivePointsPassEachRule(Func<Mask.PaintRule, bool[]> passes)
    {
        Assert.Equal(Expected6, passes(Mask.PaintRule.Any));
        Assert.Equal(Expected5, passes(Mask.PaintRule.Unpainted));
        Assert.Equal(Expected4, passes(Mask.PaintRule.Painted));
        Assert.Equal(Expected3, passes(Mask.PaintRule.Dirt));
        Assert.Equal(Expected2, passes(Mask.PaintRule.Cultivated));
        Assert.Equal(Expected1, passes(Mask.PaintRule.Paved));
    }
}
