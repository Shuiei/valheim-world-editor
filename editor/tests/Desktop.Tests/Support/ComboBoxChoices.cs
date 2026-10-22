using Avalonia.Controls;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Reads and chooses the lines of a list box filled with text (the Dungeon panel's dungeon list).
/// </summary>
internal static class ComboBoxChoices
{
    /// <summary>
    /// The box's lines, in its order.
    /// </summary>
    internal static List<string> Lines(ComboBox box) => [.. (IEnumerable<string>)box.ItemsSource!];

    /// <summary>
    /// Chooses the first line holding this text (none chosen when no line does).
    /// </summary>
    internal static void Choose(ComboBox box, string part) =>
        box.SelectedIndex = Lines(box).FindIndex(n => n.Contains(part, StringComparison.Ordinal));
}
