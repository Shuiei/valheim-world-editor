using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's words set on one of the editor's own controls: a switch takes true or false, a button a click, a slider and a
/// number box a number held to the control's range; any other words come back as the reason, and the control is left as it was.
/// </summary>
[Collection("World files")]
public class ClaudeToolsControlSettersTests
{
    [AvaloniaFact]
    public void ASwitchTakesTrueOrFalseWordsWhateverTheirCase()
    {
        var box = new CheckBox();
        var c = new ClaudeTools.Ctl("snap", "switch", "Snap", box);
        Assert.Null(ClaudeTools.SetToggle(c, box, "on"));
        Assert.True(box.IsChecked);
        Assert.Null(ClaudeTools.SetToggle(c, box, "No"));
        Assert.False(box.IsChecked);
    }

    [AvaloniaFact]
    public void ASwitchSaysWhatItTakesWhenGivenOtherWords()
    {
        var box = new CheckBox { IsChecked = false };
        var c = new ClaudeTools.Ctl("snap", "switch", "Snap", box);
        Assert.Equal("snap is a switch: give true or false (or click).", ClaudeTools.SetToggle(c, box, "maybe"));
        Assert.False(box.IsChecked);
    }

    [AvaloniaFact]
    public void AButtonTakesAClickAndSaysSoOtherwise()
    {
        var button = new Button();
        var c = new ClaudeTools.Ctl("go", "button", "Go", button);
        Assert.Null(ClaudeTools.SetButton(c, button, "click"));
        Assert.Equal("go is a button: give click.", ClaudeTools.SetButton(c, button, "press"));
    }

    [AvaloniaFact]
    public void ASliderTakesANumberHeldToItsRange()
    {
        var slider = new Slider { Minimum = 0, Maximum = 10 };
        var c = new ClaudeTools.Ctl("cut", "slider", "Cut", slider);
        Assert.Null(ClaudeTools.SetSlider(c, slider, "4.5"));
        Assert.Equal(4.5, slider.Value);
        Assert.Null(ClaudeTools.SetSlider(c, slider, "42"));
        Assert.Equal(10, slider.Value);
        Assert.Null(ClaudeTools.SetSlider(c, slider, "-3"));
        Assert.Equal(0, slider.Value);
    }

    [AvaloniaFact]
    public void ASliderSaysItsRangeWhenGivenAWord()
    {
        var slider = new Slider { Minimum = 0, Maximum = 10 };
        var c = new ClaudeTools.Ctl("cut", "slider", "Cut", slider);
        Assert.Equal("cut is a slider from 0 to 10: give a number.", ClaudeTools.SetSlider(c, slider, "high"));
    }

    [AvaloniaFact]
    public void ANumberBoxIsHeldToItsRangeEvenForAHugeNumber()
    {
        // 1e30 is past what a decimal holds: it is held to the maximum before it is converted.
        var box = new NumericUpDown { Minimum = 1, Maximum = 9 };
        var c = new ClaudeTools.Ctl("count", "number", "Count", box);
        Assert.Null(ClaudeTools.SetNumber(c, box, "1e30"));
        Assert.Equal(9m, box.Value);
        Assert.Null(ClaudeTools.SetNumber(c, box, "-5"));
        Assert.Equal(1m, box.Value);
        Assert.Null(ClaudeTools.SetNumber(c, box, "3"));
        Assert.Equal(3m, box.Value);
    }

    [AvaloniaFact]
    public void ANumberBoxSaysItsRangeWhenGivenAWord()
    {
        var box = new NumericUpDown { Minimum = 1, Maximum = 9 };
        var c = new ClaudeTools.Ctl("count", "number", "Count", box);
        Assert.Equal("count takes a number from 1 to 9.", ClaudeTools.SetNumber(c, box, "many"));
    }
}
