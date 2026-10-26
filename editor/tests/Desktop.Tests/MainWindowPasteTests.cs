using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>What the status bar says a paste did (MainWindow.Paste.cs).</summary>
public class MainWindowPasteTests
{
    // The ground changed is said as what it is: the copy's own ground, or (Ground shape and paint off) only the site
    // dug out for it; it said "with the ground" for both.
    [Theory]
    [InlineData(1, true, 40, 3, 0, "Pasted with the ground, 3 object(s).")]
    [InlineData(1, false, 40, 54, 2, "Pasted, its site dug out, 54 object(s), 2 tree(s), rock(s) and the like cleared from the site.")]
    [InlineData(2, false, 0, 2, 0, "Pasted 2 copies, 2 object(s).")]
    public void APasteSaysWhatItDidToTheGround(int copies, bool ground, int touched, int added, int cleared, string said) =>
        Assert.Equal(said, MainWindow.PastedSaid(copies, ground, touched, added, cleared).English);
}
