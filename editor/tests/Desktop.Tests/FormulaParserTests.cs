using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The formula parser's levels, one at a time: each level reads its own operators, and what it reads gives the value
/// the rule of that level gives.
/// </summary>
public class FormulaParserTests
{
    private static Formula.Parser P(string src) => new(Formula.Tokenize(src), ["x"]);

    private static double Eval(Func<Formula.Env, double> f, double x = 0)
    {
        var env = new Formula.Env();
        env.Vars["x"] = x;
        return f(env);
    }

    [Fact]
    public void ParseReadsTheWholeFormula()
    {
        Assert.Equal(3, Eval(P("1+2").Parse()));
    }

    [Fact]
    public void ParseRefusesATokenNoRuleUses()
    {
        Assert.Throws<FormatException>(() => P("1 2").Parse());
    }

    [Fact]
    public void PowerRaisesRightToLeft()
    {
        Assert.Equal(512, Eval(P("2^3^2").Power()));
    }

    [Fact]
    public void ProductReadsMultiplyDivideAndRemainderLeftToRight()
    {
        Assert.Equal(2, Eval(P("8/2*3%5").Product()));
    }

    [Fact]
    public void SumReadsAddAndSubtractLeftToRight()
    {
        Assert.Equal(9, Eval(P("10-3+2").Sum()));
    }

    [Fact]
    public void CompareGivesOneWhenTheComparisonHoldsAndZeroWhenNot()
    {
        Assert.Equal(1, Eval(P("2<3").Compare()));
        Assert.Equal(0, Eval(P("3<=2").Compare()));
        Assert.Equal(1, Eval(P("2==2").Compare()));
        Assert.Equal(0, Eval(P("2!=2").Compare()));
    }

    [Fact]
    public void AndGivesTheFirstFalseValueOrTheLastValue()
    {
        Assert.Equal(0, Eval(P("0&&5").And()));
        Assert.Equal(5, Eval(P("2&&5").And()));
    }

    [Fact]
    public void OrGivesTheFirstTrueValueOrTheLastValue()
    {
        Assert.Equal(7, Eval(P("0||7").Or()));
        Assert.Equal(3, Eval(P("3||7").Or()));
    }

    [Fact]
    public void TernaryChoosesItsBranchByTheCondition()
    {
        Assert.Equal(4, Eval(P("1?4:5").Ternary()));
        Assert.Equal(5, Eval(P("0?4:5").Ternary()));
    }

    [Fact]
    public void PrimaryReadsNumbersSignsAndNots()
    {
        Assert.Equal(-3, Eval(P("-3").Primary()));
        Assert.Equal(1, Eval(P("!0").Primary()));
        Assert.Equal(2.5, Eval(P("2.5").Primary()));
    }

    [Fact]
    public void PrimaryReadsABracketedFormulaAndStopsAfterIt()
    {
        var p = P("(1+2)*2");
        Assert.Equal(3, Eval(p.Primary()));
    }

    [Fact]
    public void NameReadsAVariableAndPi()
    {
        Assert.Equal(4, Eval(P("x").Name("x"), 4));
        Assert.Equal(Math.PI, Eval(P("pi").Name("pi")));
    }

    [Fact]
    public void NameRefusesAVariableTheFormulaMayNotUse()
    {
        Assert.Throws<FormatException>(() => P("y").Name("y"));
    }

    [Fact]
    public void CallReadsAFunctionWithItsArguments()
    {
        Assert.Equal(4, Eval(P("max(1,4)").Primary()));
    }

    [Fact]
    public void CallRefusesTheWrongNumberOfArguments()
    {
        Assert.Throws<FormatException>(() => P("pow(2)").Primary());
    }

    [Fact]
    public void NoiseCallTakesExactlyTwoArguments()
    {
        Assert.Throws<FormatException>(() => P("n(1)").Primary());
        Assert.Equal(0, Eval(P("n(1,2)").Primary()));
    }

    [Fact]
    public void ArgumentsReadUpToTheClosingBracket()
    {
        Assert.Equal(2, P("1,2)").Arguments().Count);
        Assert.Empty(P(")").Arguments());
    }
}
