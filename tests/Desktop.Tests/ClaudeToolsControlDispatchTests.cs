using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's control setting below Set (ClaudeTools.ControlSetting, ControlSetters, ControlValues): the words a switch
/// takes, the number a slider or number box holds to its range, and what Invoke does with each kind of control.
/// </summary>
[Collection("World files")]
public class ClaudeToolsControlDispatchTests
{
    // BoolOf: a switch's words, either way; anything else is none.
    [Theory]
    [InlineData("true", true)]
    [InlineData("  ON ", true)]
    [InlineData("yes", true)]
    [InlineData("1", true)]
    [InlineData("checked", true)]
    [InlineData("false", false)]
    [InlineData("Off", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    [InlineData("unchecked", false)]
    [InlineData("maybe", null)]
    public void SwitchWordsAreTrueOrFalseOrNone(string word, bool? expected) =>
        Assert.Equal(expected, ClaudeTools.BoolOf(word));

    // NumberOf: a finite number from its text, with surrounding blanks; NaN, the infinities and words are none.
    [Theory]
    [InlineData("2.5", 2.5)]
    [InlineData(" -3e2 ", -300)]
    [InlineData("NaN", null)]
    [InlineData("-Infinity", null)]
    [InlineData("abc", null)]
    public void NumbersAreFiniteOrNone(string text, double? expected) =>
        Assert.Equal(expected, ClaudeTools.NumberOf(text));

    // Invoke: a switch takes its words, and a click changes it before its handlers run, as a pointer's does.
    [AvaloniaFact]
    public void ASwitchTakesItsWordsAndItsClickChangesItFirst()
    {
        var box = new CheckBox();
        Assert.Null(ClaudeTools.Invoke(new("a", "switch", "A", box), "on", false));
        Assert.True(box.IsChecked);
        Assert.Equal("a is a switch: give true or false (or click).", ClaudeTools.Invoke(new("a", "switch", "A", box), "maybe", false));
        var toggle = new CheckBox();
        bool? seen = null;
        toggle.Click += (_, _) => seen = toggle.IsChecked;
        Assert.Null(ClaudeTools.Invoke(new("f", "switch", "F", toggle), "click", false));
        Assert.True(toggle.IsChecked);
        Assert.True(seen);
    }

    // Invoke: a button takes only a click (or nothing), and each one raises its Click.
    [AvaloniaFact]
    public void AButtonOnlyTakesAClick()
    {
        int clicks = 0;
        var button = new Button { Content = "Go" };
        button.Click += (_, _) => clicks++;
        Assert.Equal("e is a button: give click.", ClaudeTools.Invoke(new("e", "button", "Go", button), "true", false));
        Assert.Equal(0, clicks);
        Assert.Null(ClaudeTools.Invoke(new("e", "button", "Go", button), "click", false));
        Assert.Equal(1, clicks);
        Assert.Null(ClaudeTools.Invoke(new("e", "button", "Go", button), "", false));
        Assert.Equal(2, clicks);
    }

    // Invoke: a dropdown takes an option by its text, the start of it, or its number, else says what it offers.
    [AvaloniaFact]
    public void ADropdownTakesAnOptionByTextStartOrNumber()
    {
        var combo = new ComboBox { ItemsSource = new[] { "Fast", "Sharp" } };
        Assert.Null(ClaudeTools.Invoke(new("b", "dropdown", "B", combo), "sharp", false));
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Null(ClaudeTools.Invoke(new("b", "dropdown", "B", combo), "Fa", false));
        Assert.Equal(0, combo.SelectedIndex);
        Assert.Null(ClaudeTools.Invoke(new("b", "dropdown", "B", combo), "1", false));
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("b has no option \"Slow\": Fast, Sharp.", ClaudeTools.Invoke(new("b", "dropdown", "B", combo), "Slow", false));
        Assert.Equal(1, combo.SelectedIndex);
    }

    // Invoke: a slider's value is held to its range; a word is refused with the range named.
    [AvaloniaFact]
    public void ASliderIsHeldToItsRange()
    {
        var slider = new Slider { Minimum = 0, Maximum = 40 };
        Assert.Null(ClaudeTools.Invoke(new("c", "slider", "C", slider), "99", false));
        Assert.Equal(40, slider.Value);
        Assert.Null(ClaudeTools.Invoke(new("c", "slider", "C", slider), "-5", false));
        Assert.Equal(0, slider.Value);
        Assert.Equal("c is a slider from 0 to 40: give a number.", ClaudeTools.Invoke(new("c", "slider", "C", slider), "NaN", false));
        Assert.Equal(0, slider.Value);
    }

    // Invoke: a number box is held to its range too, also for numbers a decimal cannot hold.
    [AvaloniaFact]
    public void ANumberBoxIsHeldToItsRange()
    {
        var number = new NumericUpDown { Minimum = 1, Maximum = 50 };
        Assert.Null(ClaudeTools.Invoke(new("g", "number", "G", number), "1e30", false));
        Assert.Equal(50m, number.Value);
        Assert.Null(ClaudeTools.Invoke(new("g", "number", "G", number), "-1e30", false));
        Assert.Equal(1m, number.Value);
        Assert.Equal("g takes a number from 1 to 50.", ClaudeTools.Invoke(new("g", "number", "G", number), "Infinity", false));
    }

    // Invoke: a text box takes the text; a kind of control Claude does not set says so.
    [AvaloniaFact]
    public void ATextBoxTakesTextAndOtherControlsAreRefused()
    {
        var text = new TextBox();
        Assert.Null(ClaudeTools.Invoke(new("d", "text", "D", text), "Beech", false));
        Assert.Equal("Beech", text.Text);
        Assert.Equal("t cannot be set.", ClaudeTools.Invoke(new("t", "label", "T", new TextBlock()), "x", false));
    }
}
