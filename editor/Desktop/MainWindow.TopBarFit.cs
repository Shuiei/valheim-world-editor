using Avalonia.Controls;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The least the top bar's names keep, in pixels, before the buttons drop their words.
    /// </summary>
    private const double NamesLeast = 120;

    /// <summary>
    /// Whether the top bar's word buttons (Map, Reload, History, View) show only their icons: the window is too narrow
    /// for their words. Their tips still say what they do.
    /// </summary>
    internal bool TopBarCompact { get; private set; }

    /// <summary>
    /// How much wider the word buttons were with their words, when they last dropped them.
    /// </summary>
    private double _topBarWords;

    /// <summary>
    /// The words of the top bar's buttons that can do without them (the icon stays).
    /// </summary>
    private IEnumerable<TextBlock> TopBarWords() =>
        new[] { MapButton, ReloadButton, HistoryButton, ViewButton }.Select(b => b.Content).OfType<Panel>().SelectMany(p => p.Children.OfType<TextBlock>());

    /// <summary>
    /// After each layout: the word buttons drop their words when the names would have less than
    /// <see cref="NamesLeast"/> left, and take them back once there is room for them again.
    /// </summary>
    /// <param name="barWidth">The bar's width.</param>
    /// <param name="others">The width of everything in the bar but the names (buttons, area arrows, gaps).</param>
    private void FitTopBar(double barWidth, double others)
    {
        double left = barWidth - others;
        if (!TopBarCompact && left < NamesLeast)
        {
            _topBarWords = TopBarWords().Where(t => t.IsEffectivelyVisible).Sum(t => t.DesiredSize.Width + 6);
            SetTopBarCompact(true);
        }
        else if (TopBarCompact && left - _topBarWords >= NamesLeast)
        {
            SetTopBarCompact(false);
        }
        else if (TopBarCompact && TopBarWords().Any(t => t.IsVisible))
        {
            // A button given new words meanwhile (the Workshop's Start): icons only still.
            SetTopBarCompact(true);
        }
    }

    /// <summary>
    /// Shows or hides the word buttons' words.
    /// </summary>
    /// <param name="compact">True: icons only.</param>
    internal void SetTopBarCompact(bool compact)
    {
        TopBarCompact = compact;
        foreach (var t in TopBarWords())
        {
            t.IsVisible = !compact;
        }
    }
}
