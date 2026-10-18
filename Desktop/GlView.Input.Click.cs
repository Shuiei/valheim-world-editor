using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// What a release counts as a click (the left button, a pointer that moved less than 5 pixels from the press), and the
// keys that make a click add to the selection or select a range.
public sealed partial class GlView
{
    /// <summary>
    /// Whether a release at <paramref name="at"/> is a click for the View tool: the left button, no slide, and the
    /// pointer moved less than 5 pixels from the press.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <returns>True when the release is a click that picks.</returns>
    private bool IsClickInView(Point at) => Mode == ToolMode.View && !Panning && IsSmallLeftPress(at);

    /// <summary>Whether the left button was pressed and the pointer is less than 5 pixels from that press.</summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    private bool IsSmallLeftPress(Point at) =>
        _dragButton == PointerUpdateKind.LeftButtonPressed && _pressAt is Point from && Point.Distance(from, at) < 5;

    /// <summary>Whether Shift or Ctrl is held: a pick adds the object to the selection, or takes it out.</summary>
    /// <param name="mods">The keys held.</param>
    private static bool AddsToSelection(KeyModifiers mods) => mods.HasFlag(KeyModifiers.Shift) || Ctrl(mods);

    /// <summary>Whether Shift without Ctrl is held: a pick selects the row from the last object picked to this one.</summary>
    /// <param name="mods">The keys held.</param>
    private static bool IsRangeSelect(KeyModifiers mods) => mods.HasFlag(KeyModifiers.Shift) && !Ctrl(mods);

    /// <summary>
    /// A click in the View tool: picks the object under the pointer; a plain double click on one asks for the Select
    /// tool, to move it with the handles.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    private void ViewClick(Point at, Size size, KeyModifiers mods)
    {
        bool add = AddsToSelection(mods);
        if (Pick(at, size, add, IsRangeSelect(mods)) != null && _pressClicks == 2 && !add)
        {
            SelectToolAsked?.Invoke();
        }
    }
}
