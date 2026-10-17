using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The formula's tokens: numbers, names and operators read in order, and a piece that is none of them refused.
/// </summary>
public class FormulaTokenizeTests
{
    [Fact]
    public void NumbersNamesAndOperatorsComeInOrder()
    {
        var toks = Formula.Tokenize("x*2.5 + .5e1");
        Assert.Equal(5, toks.Count);
        Assert.Equal("x", toks[0].Id);
        Assert.Equal("*", toks[1].Op);
        Assert.Equal(2.5, toks[2].N);
        Assert.Equal("+", toks[3].Op);
        Assert.Equal(5.0, toks[4].N);
    }

    [Fact]
    public void TwoCharacterOperatorsAreOneToken()
    {
        var toks = Formula.Tokenize("a<=b&&c!=d||e");
        Assert.Equal(new[] { "<=", "&&", "!=", "||" }, toks.Where(t => t.Op != null).Select(t => t.Op).ToArray());
    }

    [Fact]
    public void AnEmptyFormulaHasNoTokens()
    {
        Assert.Empty(Formula.Tokenize(""));
    }

    [Fact]
    public void APieceThatIsNoTokenIsRefused()
    {
        Assert.Throws<FormatException>(() => Formula.Tokenize("1 # 2"));
    }
}
