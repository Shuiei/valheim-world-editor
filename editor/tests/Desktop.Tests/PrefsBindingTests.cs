using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The controls bound to the prefs: a switch set from the prefs and remembered when changed, and a list remembered by its
/// text, not its place.
/// </summary>
public sealed class PrefsBindingTests : PrefsFolder
{
    private static readonly string[] Expected1 = ["three"];
    [AvaloniaFact]
    public void ASwitchIsSetFromThePrefsAndRememberedWhenChanged()
    {
        var p = Prefs.Load(File1);
        var a = new CheckBox { IsChecked = true };
        p.Bind(a, "a");
        Assert.True(a.IsChecked);
        Assert.False(p.Has("a"));
        a.IsChecked = false;
        Assert.False(p.Get("a", true));
        var b = new CheckBox { IsChecked = true };
        p.Bind(b, "a");
        Assert.False(b.IsChecked);
    }

    [AvaloniaFact]
    public void AListIsRememberedByItsTextNotItsPlace()
    {
        var p = Prefs.Load(File1);
        var box = new ComboBox { ItemsSource = new[] { "one", "two", "three" }, SelectedIndex = 0 };
        p.Bind(box, "k");
        box.SelectedIndex = 2;
        Assert.Equal("three", p.Get("k", ""));
        // Another order, a longer list: still "three".
        var other = new ComboBox { ItemsSource = new List<string> { "zero", "three", "one" }, SelectedIndex = 0 };
        p.Bind(other, "k");
        Assert.Equal(1, other.SelectedIndex);
        // No longer in the list: left as it is.
        var gone = new ComboBox { ItemsSource = new[] { "a", "b" }, SelectedIndex = 1 };
        Assert.False(p.Restore(gone, "k"));
        Assert.Equal(1, gone.SelectedIndex);
        // An empty list, no list, nothing remembered.
        Assert.False(p.Restore(new ComboBox(), "k"));
        Assert.False(p.Restore(new ComboBox { ItemsSource = Expected1 }, "nothing"));
        // Cleared: nothing remembered for it.
        other.SelectedIndex = -1;
        Assert.Equal("three", p.Get("k", ""));
    }

    // An entry renamed since the choice was kept (Select's drag on the ground was "A zone on the ground"): the choice
    // comes back as the entry named now, for that setting only.
    [AvaloniaFact]
    public void AChoiceKeptUnderAnEntrysOldNameChoosesTheEntryAsNamedNow()
    {
        var p = Prefs.Load(File1);
        p.Set("select.zone", "A zone on the ground (all above it)");
        var box = new ComboBox { ItemsSource = new[] { "A box on the screen (what is seen)", "An outline on the ground (all above it)" }, SelectedIndex = 0 };
        Assert.True(p.Restore(box, "select.zone"));
        Assert.Equal(1, box.SelectedIndex);
        p.Set("other", "A zone on the ground (all above it)");
        box.SelectedIndex = 0;
        Assert.False(p.Restore(box, "other"));
        Assert.Equal(0, box.SelectedIndex);
    }
}
