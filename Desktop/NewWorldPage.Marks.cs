using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The shown seed's landmarks over the map, placed and moved as the view moves.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The landmarks placed over the map, with their controls, so they can be moved when the view does.
    /// </summary>
    private readonly List<(SeedPreview.Landmark Mark, Control Control)> _marks = [];

    /// <summary>
    /// The shown seed's landmarks over the map (the kinds switched on), each with what it is and how far: made when the
    /// seed or the switches change, only moved when the view does.
    /// </summary>
    internal void PlaceMarks()
    {
        Marks.Children.Clear();
        _marks.Clear();
        if (Shown?.Landmarks is not { } marks)
        {
            return;
        }
        var (x, z) = Shown.Stats.Start;
        bool Showing(SeedPreview.Landmark m) => (m.Kind switch
        {
            "start" => ShowStart,
            "boss" => ShowBosses,
            _ => m.Name switch { "The Bog Witch" => ShowBogWitch, "Hildir" => ShowHildir, _ => ShowHaldor },
        }).IsChecked == true;
        // The start last: on top.
        foreach (var m in marks.OrderBy(m => m.Kind == "start").Where(Showing))
        {
            var c = Mark(m.Kind, m.Name);
            float d = MathF.Sqrt((m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z));
            string what = m.Kind == "boss" ? Lang.T("newWorld.altar", LandmarkName(m.Name)) : LandmarkName(m.Name);
            string tip = m.Kind == "start" ? Lang.T("newWorld.startAt", m.X, m.Z) : Lang.T("newWorld.markAt", what, m.X, m.Z, Km(d));
            if (m.OneOf)
            {
                tip += "\n" + Lang.T("newWorld.oneOf", marks.Count(o => o.Name == m.Name));
            }
            ToolTip.SetTip(c, tip);
            Marks.Children.Add(c);
            _marks.Add((m, c));
        }
        MoveMarks();
    }

    /// <summary>
    /// The marks where the view puts them (those out of it hidden).
    /// </summary>
    private void MoveMarks()
    {
        foreach (var (m, c) in _marks)
        {
            var at = Map.ScreenOf(m.X, m.Z);
            bool inView = at.X >= -10 && at.Y >= -10 && at.X <= Map.Bounds.Width + 10 && at.Y <= Map.Bounds.Height + 10;
            c.IsVisible = inView;
            if (inView)
            {
                Canvas.SetLeft(c, at.X - c.Width / 2);
                Canvas.SetTop(c, at.Y - c.Height / 2);
            }
        }
    }
}
