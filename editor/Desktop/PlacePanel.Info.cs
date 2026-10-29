using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// What the panel says: the info and fit text, the mode name and the status messages.
public sealed partial class PlacePanel
{
    /// <summary>What the preview shows and the keys that go with it.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };

    /// <summary>What the preview shows, and the keys that go with it.</summary>
    private void ShowInfo()
    {
        var t = T;
        ShowFit();
        _undoRow.IsVisible = t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
        int n = _input.PreviewNow.Count;
        if (t.Chosen.Count == 0)
        {
            Info.Text = Lang.T("place.info.none");
        }
        else if (t.Mode == PlaceTool.Modes.Brush)
        {
            Info.Text = string.Join(" ", new[] { SnapNote(t.Snapped), Lang.T("place.info.brush", n) }.Where(p => p.Length > 0));
        }
        else
        {
            bool turned = t.ShapeTurn != 0;
            Info.Text = t.Mode switch
            {
                PlaceTool.Modes.Line when t.EndToEnd && t.SnapGap > 0.05f && n > 0 => Lang.T("place.info.lineGap", n, t.SnapGap),
                PlaceTool.Modes.Line => Lang.T("place.info.line", n),
                PlaceTool.Modes.Zone when turned => Lang.T("place.info.zoneTurned", n, t.ShapeTurn),
                PlaceTool.Modes.Zone => Lang.T("place.info.zone", n),
                _ when turned => Lang.T("place.info.gridTurned", n, t.ShapeTurn),
                _ => Lang.T("place.info.grid", n),
            };
        }
    }

    /// <summary>The fit hint: only when a chosen kind is wider than the spacing.</summary>
    private void ShowFit()
    {
        string? hint = T.FitHint();
        FitText.Text = hint ?? "";
        _fitRow.IsVisible = hint != null;
    }

    /// <summary>Brush mode's hint: how the scatter works, or (One at a time) that a click places one.</summary>
    private readonly TextBlock _brushHint;

    /// <summary>Rows and groups that <see cref="Sync"/> shows or hides by mode, shape and what is chosen.</summary>
    private readonly Control _elevRow, _pieceBox, _attachRow, _brushRows, _scatterRows, _patchRow, _facingRows, _lineShapeBox, _lineBox, _snapRows, _freeLineRows, _loopRow, _layersRow, _gridRow, _shapeRow, _undoRow;

    private readonly TextBlock _modeHead = Head("");

    /// <summary>Line mode: how to draw the chosen shape.</summary>
    private readonly TextBlock _lineHintText = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>Switches the mode.</summary>
    private void SetMode(PlaceTool.Modes m)
    {
        T.Mode = m;
        Sync();
        T.Notify();
    }

    /// <summary>A mode's name, on its button.</summary>
    /// <param name="m">The mode.</param>
    private static string ModeName(PlaceTool.Modes m)
    {
        return m switch
        {
            PlaceTool.Modes.Line => Lang.T("place.mode.line"),
            PlaceTool.Modes.Grid => Lang.T("place.mode.grid"),
            PlaceTool.Modes.Zone => Lang.T("place.mode.zone"),
            _ => Lang.T("place.mode.brush"),
        };
    }

    /// <summary>Raises <see cref="Message"/>.</summary>
    private void Say(Said s) => Message?.Invoke(s);

    /// <summary>A line for the status bar, in the user's language and in English (for Claude).</summary>
    public event Action<Said>? Message;

    /// <summary>The brush line's note on what the piece under the cursor snaps to, or "" when it snaps to nothing.</summary>
    /// <param name="snapped">How and to what the piece snaps, if it does.</param>
    internal static string SnapNote((PlaceTool.SnapWays Way, string Name)? snapped)
    {
        return snapped switch
        {
            null => "",
            { Way: PlaceTool.SnapWays.OnTop } s => Lang.T("place.info.snappedOnTop", s.Name),
            { Way: PlaceTool.SnapWays.Beside } s => Lang.T("place.info.snappedBeside", s.Name),
            { } s => Lang.T("place.info.snappedTo", s.Name),
        };
    }
}
