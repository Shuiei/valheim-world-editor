using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// Building's own clicks (the Workshop's Build, Build here in a dungeon): Shift + right click takes a piece away, a
// middle click takes its kind, as quick as the hammer's. A drag with those buttons still turns or slides the view.
public sealed partial class GlView
{
    /// <summary>Building: Shift + right click on an object, asking to take it away (its index into the scene's things).</summary>
    public event Action<int>? RemoveAsked;

    /// <summary>Building: a middle click on an object, asking to build with its kind (its index into the scene's things).</summary>
    public event Action<int>? KindAsked;

    /// <summary>
    /// A release while building, after a right or middle press that did not move (under 5 pixels): Shift + right asks to
    /// take away the object under the pointer, middle to build with its kind. Nothing over no object.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    /// <param name="click">Whether it is a real release (not a lost pointer).</param>
    private void BuildClick(Point at, Size size, KeyModifiers mods, bool click)
    {
        if (!click || Mode != ToolMode.Place || Place?.Tool.Building != true || _pressAt is not Point from || Point.Distance(from, at) >= 5)
        {
            return;
        }
        if (_dragButton == PointerUpdateKind.RightButtonPressed && mods.HasFlag(KeyModifiers.Shift) && ObjectAt(at, size) is int gone)
        {
            RemoveAsked?.Invoke(gone);
        }
        else if (_dragButton == PointerUpdateKind.MiddleButtonPressed && ObjectAt(at, size) is int kind)
        {
            KindAsked?.Invoke(kind);
        }
    }
}
