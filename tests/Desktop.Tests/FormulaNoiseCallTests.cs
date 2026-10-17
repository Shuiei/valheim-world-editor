using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The noise function of the shape formulas takes exactly two arguments (Formula.Parser.NoiseCall).
/// </summary>
public class FormulaNoiseCallTests
{
    /// <summary>A formula argument that is the same number everywhere.</summary>
    private static Func<Formula.Env, double> Constant(double v) => _ => v;

    [Fact]
    public void NoiseWithOneArgumentIsAFormatError()
    {
        Assert.Throws<FormatException>(() => Formula.Parser.NoiseCall([Constant(1)]));
    }

    [Fact]
    public void NoiseWithThreeArgumentsIsAFormatError()
    {
        Assert.Throws<FormatException>(() => Formula.Parser.NoiseCall([Constant(1), Constant(2), Constant(3)]));
    }
}
