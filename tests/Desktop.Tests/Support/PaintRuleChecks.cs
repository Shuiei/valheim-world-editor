using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The paint rules' answers on the five test points of the mask tests: dirt, cultivated, paved, painted black, and never
/// painted, in that order.
/// </summary>
internal static class PaintRuleChecks
{
    /// <summary>
    /// Asserts which of the five test points each paint rule lets through.
    /// </summary>
    /// <param name="passes">Whether each of the five points passes under a paint rule, in point order.</param>
    internal static void AssertFivePointsPassEachRule(Func<Mask.PaintRule, bool[]> passes)
    {
        Assert.Equal(new[] { true, true, true, true, true }, passes(Mask.PaintRule.Any));
        Assert.Equal(new[] { false, false, false, true, true }, passes(Mask.PaintRule.Unpainted));
        Assert.Equal(new[] { true, true, true, false, false }, passes(Mask.PaintRule.Painted));
        Assert.Equal(new[] { true, false, false, false, false }, passes(Mask.PaintRule.Dirt));
        Assert.Equal(new[] { false, true, false, false, false }, passes(Mask.PaintRule.Cultivated));
        Assert.Equal(new[] { false, false, true, false, false }, passes(Mask.PaintRule.Paved));
    }
}
