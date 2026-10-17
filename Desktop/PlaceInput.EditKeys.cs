using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's cancel and undo keys: Escape cancels a pick or clears a shape, and Backspace removes the last point.
/// </summary>
public sealed partial class PlaceInput
{
    /// <summary>The keys that cancel or undo: Escape cancels a pick or clears a shape, and Backspace removes the last point.</summary>
    /// <param name="key">The key pressed.</param>
    /// <param name="shape">The Place tool is on a shape (not a brush).</param>
    /// <returns>True when the key was one of these.</returns>
    private bool EditKey(Avalonia.Input.Key key, bool shape)
    {
        switch (key)
        {
            case Avalonia.Input.Key.Escape when PickOnce != null:
                PickOnce = null;
                Message?.Invoke(Said.Of("view.msg.pickCancelled"));
                return true;
            case Avalonia.Input.Key.Escape when shape && (Tool.Points.Count > 0 || Tool.GridA != null || Tool.FigA != null):
                Tool.ClearShape();
                return true;
            case Avalonia.Input.Key.Back when Pointed && Tool.Points.Count > 0:
                RemoveLastPoint();
                return true;
            default:
                break;
        }
        return false;
    }
}
