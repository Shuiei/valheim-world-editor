using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool panel's refresh: shows the rows of the current action, and refills the boxes from the line.
/// </summary>
public sealed partial class PathPanel
{
    /// <summary>Shows only the rows of the current action, and the bump rows when Natural look is on.</summary>
    internal void ShowRows()
    {
        foreach (var (row, for_) in _rows)
        {
            row.IsVisible = for_.Contains(_path.Act);
        }
        _naturalRows.IsVisible = _path.Natural;
    }

    /// <summary>
    /// The line or a value picked with Alt changed: refills the boxes (the ramp's ends follow the ground at the line's
    /// ends until typed in), shows the line's length and redraws it.
    /// </summary>
    public void Refresh()
    {
        _filling = true;
        var pts = _path.Points;
        // The ramp's ends follow the ground at the line's ends until typed in.
        if (pts.Count > 0 && !_path.RampEdited)
        {
            _path.Start = MathF.Round(_view.GridHeight(pts[0]), 1);
            _path.End = MathF.Round(_view.GridHeight(pts[^1]), 1);
        }
        StartBox.Value = (decimal)_path.Start;
        EndBox.Value = (decimal)_path.End;
        HeightBox.Value = (decimal)_path.Height;
        _filling = false;
        Info.Text = pts.Count > 0 ? Lang.T("path.hint.line", pts.Count, PathTool.Length(_path.Curve())) : Lang.T("path.hint.noLine");
        _view.LassoChanged();
    }
}
