using Avalonia.Controls;
using Avalonia.LogicalTree;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Finds the controls of the inspector panel that the tests press or read.
/// </summary>
internal static class InspectorControls
{
    /// <summary>
    /// The contents grid's button for slot (x, y).
    /// </summary>
    internal static Button Slot(InspectorPanel i, int x, int y) =>
        i.ItemsBox.Children.OfType<Grid>().First(g => g.RowDefinitions.Count > 0).Children.OfType<Button>().First(b => Grid.GetColumn(b) == x && Grid.GetRow(b) == y);

    /// <summary>
    /// The picked slot's number box of this width (90: how many, 70: quality).
    /// </summary>
    internal static NumericUpDown SlotNumber(InspectorPanel i, double width) =>
        i.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().First(n => n.Width == width);

    /// <summary>
    /// Adds a value through the Add row, which draws the card again.
    /// </summary>
    internal static void AddValue(InspectorPanel i, string? key, string? value)
    {
        i.AddSection.SelectedIndex = 0;
        i.AddKey.Text = key;
        i.AddValue.Text = value;
        Click(i.AddButton);
    }

    /// <summary>
    /// Puts a stack of this many of an item in the first free slot of the open chest.
    /// </summary>
    internal static void AddStack(SharedInspectorRun r, string name, int stack)
    {
        var (x, y) = r.I.FreeSlot()!.Value;
        r.I.Items!.Add(new InspectorPanel.ItemRow { Name = name, Stack = stack, X = x, Y = y });
    }
}
