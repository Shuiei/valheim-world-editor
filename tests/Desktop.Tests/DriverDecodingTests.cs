using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The driver's command decoding, step by step: a command's argument split at its bar, the modifier names, the numbers,
/// the list and button texts the commands look controls up by, and the refusal of a command it does not know.
/// </summary>
public class DriverDecodingTests
{
    [Fact]
    public void BarSplitsTheArgumentAtItsFirstBar()
    {
        Assert.Equal(("hint", "text"), Driver.Bar(["type", "hint|text"], "type <hint>|<text>"));
        Assert.Equal(("a", "b|c"), Driver.Bar(["type", "a|b|c"], "type <a>|<b>"));
    }

    [Fact]
    public void BarRefusesAnArgumentWithoutABar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Driver.Bar(["type", "nobar"], "type <hint>|<text>"));
        Assert.Equal("usage: type <hint>|<text>", ex.Message);
        Assert.Throws<InvalidOperationException>(() => Driver.Bar(["type"], "type <hint>|<text>"));
    }

    [Fact]
    public void ModsReadsShiftCtrlAndAltAndIgnoresTheRest()
    {
        Assert.Equal(KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt, Driver.Mods(["shift", "ctrl", "alt", "other"]));
        Assert.Equal(KeyModifiers.None, Driver.Mods([]));
    }

    [Fact]
    public void CallReadsItsNumbersInTheInvariantCulture()
    {
        var call = new Driver.Call(null!, ["stroke"], ["2.5"]);
        Assert.Equal(2.5f, call.F(0));
    }

    [Fact]
    public async Task RunRefusesACommandItDoesNotKnow()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Driver.Run(null!, "nosuch x"));
        Assert.Equal("unknown command nosuch", ex.Message);
    }

    [AvaloniaFact]
    public void IndexOfFindsAnEntryByItsTextInAnyCase()
    {
        var box = new ComboBox();
        box.Items.Add("Alpha");
        box.Items.Add("Beta");
        Assert.Equal(1, Driver.IndexOf(box, "beta"));
        Assert.Equal(-1, Driver.IndexOf(box, "gamma"));
    }

    [AvaloniaFact]
    public void TextOfReadsAButtonsTrimmedWords()
    {
        Assert.Equal("Save", Driver.TextOf(new Button { Content = " Save " }));
    }
}
