using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The Path tool's Apply: the action along the line, in one undo step. The line is kept.
    /// </summary>
    internal void ApplyPath()
    {
        if (Session is not { } s)
        {
            return;
        }
        var path = View.Path;
        if (path.Points.Count < 2)
        {
            Msg("path.msg.noLine");
            return;
        }
        bool clamped = false;
        if (path.Act == PathTool.Action.Cave)
        {
            Msg(ApplyCave(s, path, s.Brush, s.MaskNow()));
            PathPanel.Refresh();
            UpdateSaveBar();
            return;
        }
        var touched = s.EditGround(Lang.T("path.undo", PathTool.Label(path.Act).Shown), g =>
        {
            var (t, rect, c) = path.Apply(g, s.Brush, s.Scene.Water, s.MaskNow());
            clamped = c;
            return (t, rect);
        });
        float total = PathTool.Length(path.Curve());
        Msg(touched.Count == 0 ? Said.Of("path.msg.nothing")
            : clamped ? Said.Of("path.msg.limit")
            : Said.Of("path.msg.applied", PathTool.Label(path.Act), total));
        PathPanel.Refresh();
        UpdateSaveBar();
    }
}
