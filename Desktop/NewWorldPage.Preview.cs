using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The loading sign, the search result marked for the seed shown, and the shown preview.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The loading sign over the map (null: none), and the search result for that seed marked.
    /// </summary>
    private void SetLoading(string? seed)
    {
        bool valid = seed is { Length: > 0 and <= 10 };
        Loading.IsVisible = valid;
        if (valid)
        {
            LoadingText.Text = Lang.T("newWorld.msg.lookingAt", seed);
            MarkFound(seed);
        }
    }

    /// <summary>
    /// The search result for this seed framed (the one chosen or shown), the others not.
    /// </summary>
    private void MarkFound(string? seed)
    {
        foreach (var b in Found.Children.OfType<Button>())
        {
            bool chosen = Equals(b.Tag, seed);
            // The same thickness for all, so the chosen one is not wider than the others.
            b.BorderBrush = chosen ? Ui.Accent : Brushes.Transparent;
            b.BorderThickness = new Thickness(2);
        }
    }

    /// <summary>
    /// Shows a made preview: the map (its view kept for the same seed), the lists and the marks; its dungeons are
    /// looked for unless known.
    /// </summary>
    private void Show(SeedPreview.Preview p)
    {
        bool same = Shown?.Seed == p.Seed;
        Shown = p;
        MarkFound(p.Seed);
        Map.Show(p, keepView: same);
        DungeonMarks = _dungeonsSeen.GetValueOrDefault(p.Seed);
        ShowInfo(p);
        PlaceMarks();
        PlaceDots();
        // Looked for at once, out of sight (so a switch turned on later shows them at once, or sooner),
        // with the loading sign when a kind is switched on already.
        if (DungeonMarks == null)
        {
            FindDungeons(p);
            if (ShowDungeons.Values.Any(t => t.IsChecked == true))
            {
                WaitForDungeons(p);
            }
        }
    }
}
