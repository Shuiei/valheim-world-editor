using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The mix of the chosen kinds: their shares of what is placed.
public sealed partial class PlacePanel
{
    /// <summary>The chosen kinds, each with its weight slider and share, and a button to unchoose it.</summary>
    internal StackPanel Mix { get; } = new() { Spacing = 3 };

    /// <summary>How many kinds are chosen, after the SELECTED KINDS heading.</summary>
    private readonly TextBlock _mixCount = new() { FontSize = 10, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// Each chosen kind's share text and slider (none for a single kind), updated in place while a slider moves.
    /// </summary>
    private readonly Dictionary<string, (TextBlock Share, Slider? Slider)> _shares = [];

    /// <summary>Set while the other sliders follow a moved one, so their own change events leave the weights alone.</summary>
    private bool _sharing;

    /// <summary>
    /// The chosen kinds changed: updates End to end and One at a time, the panel and the list, remembers them and makes
    /// a new layout.
    /// </summary>
    private void Chosen(bool fillList = true)
    {
        T.AutoSnap();
        _filling = true;
        PresetBox.SelectedIndex = 0;
        _filling = false;
        Fill();
        if (fillList)
        {
            FillList();
        }
        Remember();
        RenderChips();
        T.NewLayout();
    }

    /// <summary>
    /// Fills the mix: each chosen kind on a row of the same layout, with its share, a button to unchoose it, and a weight
    /// slider (1 to 10) when there are several; "None yet" while nothing is chosen.
    /// </summary>
    private void RenderMix()
    {
        Mix.Children.Clear();
        _shares.Clear();
        var names = T.Chosen.ToList();
        _mixCount.Text = names.Count > 0 ? $"{names.Count}" : "";
        if (names.Count == 0)
        {
            Mix.Children.Add(new TextBlock { Text = Lang.T("place.hint.noneYet"), FontSize = 12, Foreground = Ui.Muted, Margin = new Thickness(0, 4) });
            return;
        }
        float total = names.Sum(T.WeightOf);
        foreach (string? n in names)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,80,40,Auto"), ColumnSpacing = 4, MinHeight = 30 };
            g.Children.Add(new TextBlock { Text = n, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var pct = new TextBlock { FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Text = $"{T.WeightOf(n) / total * 100:0}%" };
            Slider? w = null;
            if (names.Count > 1)
            {
                // The slider is the kind's share in %, the equal share in its middle (it ends at twice that, each of the
                // others keeping at least 1%); a share past its end shows at the end, the real one in the text.
                w = new Slider { Minimum = 1, Maximum = MaxShare(names.Count), TickFrequency = 1, IsSnapToTickEnabled = true, Value = Math.Round(T.WeightOf(n) / total * 100), VerticalAlignment = VerticalAlignment.Center };
                ToolTip.SetTip(w, Lang.T("place.shareOf", n, Tips.Of("place.weight")));
                // Rows stay (rebuilding them would drop the slider being dragged); the others follow.
                w.ValueChanged += (_, e) =>
                {
                    if (_sharing)
                    {
                        return;
                    }
                    Share(n, (int)Math.Round(e.NewValue));
                    Remember();
                    T.NewLayout();
                };
                g.Children.Add(Ui.Col(w, 1));
            }
            _shares[n] = (pct, w);
            g.Children.Add(Ui.Col(pct, 2));
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(x, $"{Tips.Of("place.unchoose")} ({n})");
            x.Click += (_, _) => { T.Chosen.Remove(n); Chosen(); };
            g.Children.Add(Ui.Col(x, 3));
            Mix.Children.Add(g);
        }
    }

    /// <summary>
    /// A kind's slider moved to <paramref name="percent"/>: the other kinds share the rest in their current
    /// proportions (whole percents, each at least 1, summing to 100), and their sliders and shares follow.
    /// </summary>
    private void Share(string kind, int percent)
    {
        var others = _shares.Keys.Where(k => k != kind).ToList();
        int rest = 100 - percent;
        float before = others.Sum(T.WeightOf);
        var raw = others.Select(k => before > 0 ? rest * T.WeightOf(k) / before : (float)rest / others.Count).ToList();
        var whole = raw.Select(r => Math.Max(1, (int)MathF.Floor(r))).ToList();
        // What rounding down left over goes to the largest remainders.
        foreach (int i in Enumerable.Range(0, others.Count).OrderByDescending(i => raw[i] - MathF.Floor(raw[i])).Take(Math.Max(0, rest - whole.Sum())))
        {
            whole[i]++;
        }
        T.Weights[kind] = percent;
        for (int i = 0; i < others.Count; i++)
        {
            T.Weights[others[i]] = whole[i];
        }
        _sharing = true;
        foreach (string k in others)
        {
            if (_shares[k].Slider is { } s)
            {
                s.Value = T.WeightOf(k);
            }
        }
        _sharing = false;
        ShowShares();
    }

    /// <summary>
    /// The end of the share sliders with this many kinds: twice the equal share, so the equal share is in the middle
    /// (99% for 2 kinds, 67% for 3, 50% for 4), leaving the others at least 1% each.
    /// </summary>
    internal static int MaxShare(int kinds) => Math.Min(100 - (kinds - 1), (int)Math.Round(200.0 / kinds));

    /// <summary>Writes each chosen kind's share of the weights on its row.</summary>
    private void ShowShares()
    {
        float total = _shares.Keys.Sum(T.WeightOf);
        foreach (var (n, row) in _shares)
        {
            row.Share.Text = $"{T.WeightOf(n) / total * 100:0}%";
        }
    }
}
