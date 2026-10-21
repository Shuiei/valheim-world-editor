using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The presses that are not a tool's: the eyedropper's one click, and the slide (Space or Shift + drag).
public sealed partial class GlView
{
    /// <summary>
    /// The eyedropper waits for one click: this click is its, not the tool's (nor is the release). Takes the left
    /// button's press only.
    /// </summary>
    /// <param name="at">Where the press is, on the surface.</param>
    /// <returns>True when the eyedropper took the press.</returns>
    private bool PressEyedropper(Point at)
    {
        if (_dragButton != PointerUpdateKind.LeftButtonPressed || PickObjectOnce is not { } pickOnce)
        {
            return false;
        }
        PickObjectOnce = null;
        _dragFrom = null;
        _pickSwallow = true;
        pickOnce(ObjectAt(at, _surfaceSize));
        Wake();
        return true;
    }

    /// <summary>
    /// Space + left drag and Shift + right drag slide the view, whatever the tool (the web editor's; handy without a
    /// middle button), in the orbit eye only.
    /// </summary>
    /// <param name="e">The press.</param>
    /// <returns>True when the press starts a slide.</returns>
    private bool PressPan(PointerPressedEventArgs e)
    {
        bool space;
        lock (_keys)
        {
            space = _keys.Contains(Key.Space);
        }
        bool slide = _dragButton == PointerUpdateKind.LeftButtonPressed && space || _dragButton == PointerUpdateKind.RightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_eye != EyeMode.Orbit || !slide)
        {
            return false;
        }
        Panning = true;
        Wake();
        return true;
    }
}
