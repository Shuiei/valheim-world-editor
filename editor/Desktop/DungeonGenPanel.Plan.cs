using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The plan of the level shown: its rooms from above, and the doors.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>
    /// The level's rooms seen from above: the way through brighter, the boss red, treasure gold, the key
    /// chest's room green, doorways as dots (the boss's gate magenta, hidden ones cyan).
    /// </summary>
    private void DrawPlan()
    {
        _plan.Children.Clear();
        if (_result?.Map is not { Count: > 0 } map)
        {
            return;
        }
        float x0 = map.Min(r => r.X0), x1 = map.Max(r => r.X1), z0 = map.Min(r => r.Z0), z1 = map.Max(r => r.Z1);
        double scale = Math.Min((_plan.Width - 8) / Math.Max(1, x1 - x0), (_plan.Height - 8) / Math.Max(1, z1 - z0));
        double X(float x) => 4 + (x - x0) * scale;
        double Y(float z) => _plan.Height - 4 - (z - z0) * scale;
        foreach (var r in map.Where(r => r.Level == _level))
        {
            var fill = r.Corridor ? Color.Parse("#3a3f4a") : r.Key ? Color.Parse("#2f7d4a") : r.Name == "Arena" ? Color.Parse("#8a2b2b")
                : r.Name is "Treasury" or "Hidden cache" ? Color.Parse("#9a8424") : r.Main ? Color.Parse("#7a5c3a") : Color.Parse("#5a4632");
            // A shaped room: its parts, each without an outline (they would cut it up), then its outline is its parts' edges.
            foreach (var (px0, pz0, px1, pz1) in r.Floor)
            {
                var rect = new Rectangle
                {
                    Width = Math.Max(1, (px1 - px0) * scale),
                    Height = Math.Max(1, (pz1 - pz0) * scale),
                    Fill = new SolidColorBrush(fill),
                    Stroke = r.Corridor ? null : new SolidColorBrush(Color.Parse("#b8b2a8")),
                    StrokeThickness = r.Parts == null ? 0.6 : 0.3,
                };
                ToolTip.SetTip(rect, r.Name);
                Canvas.SetLeft(rect, X(px0));
                Canvas.SetTop(rect, Y(pz1));
                _plan.Children.Add(rect);
            }
        }
        foreach (var d in _result.Doors!.Where(d => d.Level == _level))
        {
            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(d.Kind switch { "Key" => Colors.Magenta, "Secret" => Colors.Cyan, "Open" => Color.Parse("#7bc47f"), _ => Color.Parse("#e0a64b") }),
            };
            Canvas.SetLeft(dot, X(d.X) - 2);
            Canvas.SetTop(dot, Y(d.Z) - 2);
            _plan.Children.Add(dot);
        }
    }
}
