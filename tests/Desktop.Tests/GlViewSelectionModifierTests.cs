using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The keys that change a click's selection in the view: Shift adds to the selection, Ctrl (or Cmd) adds too, and Shift
/// alone selects a range from the anchor. Ctrl with Shift is an add, not a range.
/// </summary>
public sealed class GlViewSelectionModifierTests
{
    /// <summary>Calls the add-to-selection check.</summary>
    private static bool AddsToSelection(KeyModifiers mods) =>
        (bool)PrivateMethods.Call(typeof(GlView), "AddsToSelection", null, mods)!;

    /// <summary>Calls the range-select check.</summary>
    private static bool IsRangeSelect(KeyModifiers mods) =>
        (bool)PrivateMethods.Call(typeof(GlView), "IsRangeSelect", null, mods)!;

    /// <summary>Shift adds the clicked object to the selection.</summary>
    [Fact]
    public void ShiftAddsToTheSelection() =>
        Assert.True(AddsToSelection(KeyModifiers.Shift));

    /// <summary>Control adds to the selection, and so does Command on a Mac.</summary>
    [Fact]
    public void ControlOrCommandAddsToTheSelection()
    {
        Assert.True(AddsToSelection(KeyModifiers.Control));
        Assert.True(AddsToSelection(KeyModifiers.Meta));
    }

    /// <summary>A click with no key held neither adds nor selects a range.</summary>
    [Fact]
    public void NoKeyHeldDoesNotAddOrSelectARange()
    {
        Assert.False(AddsToSelection(KeyModifiers.None));
        Assert.False(IsRangeSelect(KeyModifiers.None));
    }

    /// <summary>Shift alone selects the range from the anchor.</summary>
    [Fact]
    public void ShiftAloneSelectsARange()
    {
        Assert.True(IsRangeSelect(KeyModifiers.Shift));
    }

    /// <summary>Control with Shift is an add, not a range.</summary>
    [Fact]
    public void ControlWithShiftIsNotARange()
    {
        Assert.False(IsRangeSelect(KeyModifiers.Shift | KeyModifiers.Control));
        Assert.True(AddsToSelection(KeyModifiers.Shift | KeyModifiers.Control));
    }
}
