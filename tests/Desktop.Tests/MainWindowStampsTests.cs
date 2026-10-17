using System.Text;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a script's message says when it has printed something: the printed text comes first, in both languages, and
/// the message about its end follows in each language.
/// </summary>
public sealed class MainWindowStampsTests
{
    /// <summary>The printed text is put before the message in the language shown and in English.</summary>
    [Fact]
    public void ThePrintedTextComesBeforeTheMessageInBothLanguages()
    {
        var printed = new StringBuilder("line one\n");

        var said = (Said)PrivateMethods.Call(typeof(MainWindow), "Printed", null, printed, new Said("fini", "done"))!;

        Assert.Equal(new Said("line one\nfini", "line one\ndone"), said);
    }

    /// <summary>With nothing printed, the message is given as it is.</summary>
    [Fact]
    public void WithNothingPrintedTheMessageIsGivenAsItIs()
    {
        var said = (Said)PrivateMethods.Call(typeof(MainWindow), "Printed", null, new StringBuilder(), new Said("fini", "done"))!;

        Assert.Equal(new Said("fini", "done"), said);
    }
}
