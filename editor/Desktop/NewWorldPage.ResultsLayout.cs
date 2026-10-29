using Avalonia;
using Avalonia.Controls;

namespace TerrainEditor.Desktop;

/// <summary>
/// The layout of the found seeds: their height and their spacing in the window.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The found seeds' list beside the search: the height the window leaves, and as many seeds in a row as fit.
    /// </summary>
    /// <param name="results">The scroll view of the found seeds.</param>
    /// <param name="column">The column of the search cards.</param>
    /// <param name="left">The left side, scrolled.</param>
    private void WireResults(ScrollViewer results, StackPanel column, ScrollViewer left)
    {
        void FitResults()
        {
            double rest = column.Bounds.Height - results.Height;
            double height = Math.Max(MinResults, left.Bounds.Height - rest);
            if (column.Bounds.Height > 0 && Math.Abs(height - results.Height) > 1)
            {
                results.Height = height;
            }
        }
        // As many seeds in a row as fit, the same space between them and at both sides.
        void SpaceResults()
        {
            double width = results.Viewport.Width, tile = Found.Children.OfType<Button>().FirstOrDefault()?.Bounds.Width ?? 0;
            if (width <= 0 || tile <= 0)
            {
                return;
            }
            int n = Math.Max(1, (int)((width - MinGap) / (tile + MinGap)));
            double gap = Math.Max(0, Math.Floor((width - n * tile) / (n + 1)));
            if (Math.Abs(gap - Found.ItemSpacing) > 0.5)
            {
                Found.ItemSpacing = gap;
                Found.Margin = new Thickness(gap, 0, 0, 0);
            }
        }
        left.PropertyChanged += (_, e) => { if (e.Property == Visual.BoundsProperty) { FitResults(); } };
        column.PropertyChanged += (_, e) => { if (e.Property == Visual.BoundsProperty) { FitResults(); } };
        results.PropertyChanged += (_, e) => { if (e.Property == ScrollViewer.ViewportProperty) { SpaceResults(); } };
        Found.LayoutUpdated += (_, _) => SpaceResults();
    }
}
