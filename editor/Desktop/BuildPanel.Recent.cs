using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Terrain;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>How many pieces picked last the dock's foot keeps.</summary>
    internal const int MaxRecent = 12;

    /// <summary>The pieces picked last, newest first (kept between runs).</summary>
    private readonly List<string> _recent = [];

    /// <summary>The pieces picked last, newest first.</summary>
    internal IReadOnlyList<string> Recent => _recent;

    /// <summary>A piece picked by hand (its tile, or one picked last): chosen, and first among those picked last.</summary>
    /// <param name="prefab">The piece's prefab name.</param>
    internal void Pick(string prefab)
    {
        Choose(prefab);
        _recent.Remove(prefab);
        _recent.Insert(0, prefab);
        if (_recent.Count > MaxRecent)
        {
            _recent.RemoveRange(MaxRecent, _recent.Count - MaxRecent);
        }
        _prefs.Set("build.recent", _recent.ToArray());
        RenderRecent();
    }

    /// <summary>
    /// The pieces picked last, as small tiles (those the game no longer has, a mod taken away, left out); a word when
    /// there are none yet.
    /// </summary>
    private void RenderRecent()
    {
        RecentRow.Children.Clear();
        string? chosen = Tool.Chosen.Count == 1 ? Tool.Chosen[0] : null;
        var known = Pieces.Select(p => p.Prefab).ToHashSet(StringComparer.Ordinal);
        foreach (string prefab in _recent.Where(known.Contains))
        {
            var b = new Button { Tag = prefab, Padding = new Thickness(2), Content = PictureImage(prefab, 34) };
            ToolTip.SetTip(b, $"{PieceCost.PieceName(prefab)} ({prefab})");
            b.Classes.Set("on", prefab == chosen);
            b.Click += (_, _) => Pick(prefab);
            RecentRow.Children.Add(b);
        }
        if (RecentRow.Children.Count == 0)
        {
            RecentRow.Children.Add(new TextBlock { Text = Lang.T("build.noRecent"), FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        }
    }
}
