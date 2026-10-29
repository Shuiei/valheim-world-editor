using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The CSV scanner one character or one line at a time: the row each step completes, and the rows left when the text
/// or a line ends inside a quoted field.
/// </summary>
public class LocalizationCsvScannerTests
{
    /// <summary>
    /// Feeds the text one character at a time and returns the rows the characters complete, in order.
    /// </summary>
    private static List<List<string>> FeedAll(GameCatalogues.Localization.CsvScanner scanner, string text)
    {
        var rows = new List<List<string>>();
        foreach (char c in text)
        {
            List<string>? done = scanner.Feed(c);
            if (done != null)
            {
                rows.Add(done);
            }
        }
        return rows;
    }

    [Fact]
    public void ACharacterInsideAFieldCompletesNoRowYet()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();

        Assert.Null(scanner.Feed('a'));
    }

    [Fact]
    public void EndingALineAfterAnUnquotedFieldGivesTheFieldAsARow()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "a,b");

        Assert.Equal(["a", "b"], scanner.EndLine());
    }

    [Fact]
    public void ATrailingCommaBeforeTheEndOfALineAddsAnEmptyField()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "a,");

        Assert.Equal(["a", ""], scanner.EndLine());
    }

    [Fact]
    public void AQuotedFieldClosedAtTheEndOfALineIsAddedToItsRow()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "\"a\"");

        Assert.Equal(["a"], scanner.EndLine());
    }

    [Fact]
    public void ALineEndingInsideQuotesCompletesNoRowAndTheRowGoesOnWithTheNextLine()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "\"a");

        Assert.Null(scanner.EndLine());
        FeedAll(scanner, "\nb\"");

        Assert.Equal(["a\nb"], scanner.EndLine());
    }

    [Fact]
    public void AnEmptyScannerEndsALineAsARowOfNoFields()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();

        Assert.Empty(scanner.EndLine()!);
    }

    [Fact]
    public void TheTextEndingInsideQuotesReturnsTheUnfinishedRow()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "\"x");

        Assert.Equal(["x"], scanner.EndText());
    }

    [Fact]
    public void TheTextEndingOutsideQuotesReturnsNoRow()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();
        FeedAll(scanner, "a");

        Assert.Null(scanner.EndText());
    }

    [Fact]
    public void ACharacterAfterALoneCarriageReturnReturnsTheRowBeforeIt()
    {
        var scanner = new GameCatalogues.Localization.CsvScanner();

        Assert.Equal([["a"]], FeedAll(scanner, "a\rb"));
        Assert.Equal(["b"], scanner.EndLine());
    }
}
