using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Every function and comparison of the Shape tool's formula language, and what a missing bracket is said to come
/// before.
/// </summary>
public class FormulaFunctionsTests
{
    /// <summary>
    /// Compiles the formula and evaluates it at x = 2.
    /// </summary>
    private static double Eval(string src)
    {
        var env = new Formula.Env();
        env.Vars["x"] = 2;
        return Formula.Compile(src, ["x"])(env);
    }

    [Theory]
    [InlineData("sin(0)", 0)]
    [InlineData("cos(0)", 1)]
    [InlineData("tan(0)", 0)]
    [InlineData("abs(-3)", 3)]
    [InlineData("exp(0)", 1)]
    [InlineData("log(1)", 0)]
    [InlineData("floor(2.7)", 2)]
    [InlineData("ceil(2.2)", 3)]
    [InlineData("min(4, x, 3)", 2)]
    [InlineData("pow(2, 3)", 8)]
    [InlineData("atan2(0, 1)", 0)]
    [InlineData("sign(-5)", -1)]
    [InlineData("bell(0)", 1)]
    [InlineData("pi", Math.PI)]
    [InlineData("x <= 2", 1)]
    [InlineData("x >= 3", 0)]
    [InlineData("x == 2", 1)]
    [InlineData("x != 2", 0)]
    [InlineData("x < 1", 0)]
    public void EveryFunctionAndComparisonWorks(string src, double want)
    {
        Assert.Equal(want, Eval(src), 9);
    }

    [Fact]
    public void MinAndMaxOfNothingAreInfinite()
    {
        Assert.Equal(double.PositiveInfinity, Eval("min()"));
        Assert.Equal(double.NegativeInfinity, Eval("max()"));
    }

    [Theory]
    [InlineData("(1 2", "“)” expected before “2”")]
    [InlineData("(1 x", "“)” expected before “x”")]
    [InlineData("(1 (", "“)” expected before “(”")]
    public void AMissingBracketSaysWhatItCameBefore(string src, string message)
    {
        Assert.Equal(message, Assert.Throws<FormatException>(() => Formula.Compile(src, ["x"])).Message);
    }
}
