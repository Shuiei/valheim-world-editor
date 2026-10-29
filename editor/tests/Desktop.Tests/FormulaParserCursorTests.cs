using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The parser's cursor (Formula.Parser.Peek and Eat): Peek shows the next token without reading it, and Eat reads it only
/// when it is the operator asked for.
/// </summary>
public class FormulaParserCursorTests
{
    /// <summary>A parser over the tokens of a formula.</summary>
    private static Formula.Parser P(string src) => new(Formula.Tokenize(src), ["x"]);

    [Fact]
    public void PeekShowsTheNextTokenWithoutReadingIt()
    {
        var p = P("+1");

        Assert.Equal("+", p.Peek()!.Op);
        Assert.Equal("+", p.Peek()!.Op);
    }

    [Fact]
    public void PeekAtTheEndIsNothing()
    {
        var p = P("+");
        Assert.True(p.Eat("+"));

        Assert.Null(p.Peek());
    }

    [Fact]
    public void EatReadsTheOperatorAskedFor()
    {
        var p = P("+1");

        Assert.True(p.Eat("+"));
        Assert.False(p.Eat("+"));
    }

    [Fact]
    public void EatLeavesADifferentTokenUnread()
    {
        var p = P("+1");

        Assert.False(p.Eat("-"));
        Assert.True(p.Eat("+"));
    }
}
