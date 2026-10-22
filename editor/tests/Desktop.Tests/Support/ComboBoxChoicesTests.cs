using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.ComboBoxChoices;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The list box helpers, tested once here so the tests using them can rely on them.
/// </summary>
public sealed class ComboBoxChoicesTests
{
    [AvaloniaFact]
    public void ChooseTakesTheFirstLineHoldingTheTextAndNoneWhenNoLineDoes()
    {
        var box = new ComboBox { ItemsSource = new List<string> { "Cave, 3 room(s)", "Cave, 1 room(s)", "Crypt, 1 room(s)" } };
        Assert.Equal(["Cave, 3 room(s)", "Cave, 1 room(s)", "Crypt, 1 room(s)"], Lines(box));
        Choose(box, "1 room(s)");
        Assert.Equal(1, box.SelectedIndex);
        Choose(box, "0 room(s)");
        Assert.Equal(-1, box.SelectedIndex);
    }
}
