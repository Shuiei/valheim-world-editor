using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A function call in a formula: its arguments' values go to the function, and the same call gives the same value
/// at every point.
/// </summary>
public class FormulaApplyTests
{
    [Fact]
    public void TheFunctionGetsTheArgumentValuesInOrder()
    {
        var f = Formula.Apply(a => a[0] * 10 + a[1], [env => 2.0, env => 3.0]);
        Assert.Equal(23.0, f(new Formula.Env()));
    }

    [Fact]
    public void RepeatedCallsReadTheEnvironmentAgain()
    {
        var f = Formula.Apply(a => a[0] - a[1], [env => env.Vars["x"], env => 1.0]);
        var env = new Formula.Env();
        env.Vars["x"] = 5;
        Assert.Equal(4.0, f(env));
        env.Vars["x"] = 7;
        Assert.Equal(6.0, f(env));
    }
}
