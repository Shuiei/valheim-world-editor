using System.Globalization;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A formula token as it is written in messages: its operator or bracket, else its name, else its number.
/// </summary>
public class FormulaTokenTextTests
{
    [Fact]
    public void AnOperatorIsWrittenAsItself()
    {
        Assert.Equal("*", new Formula.Tok(Op: "*").ToString());
    }

    [Fact]
    public void ANameIsWrittenAsItself()
    {
        Assert.Equal("pi", new Formula.Tok(Id: "pi").ToString());
    }

    [Fact]
    public void ANumberIsWrittenInInvariantCulture()
    {
        Assert.Equal(2.5.ToString(CultureInfo.InvariantCulture), new Formula.Tok(N: 2.5).ToString());
        Assert.Equal("2.5", new Formula.Tok(N: 2.5).ToString());
    }
}
