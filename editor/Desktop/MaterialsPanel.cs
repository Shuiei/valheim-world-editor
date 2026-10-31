using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Workshop's Materials panel, at the right: what the building on the plot costs in game, every material with its
/// amount (most first), the crafting stations that must be near, the pieces with no known recipe (by mod) and those
/// only the editor can place. Made again after each change.
/// </summary>
public sealed class MaterialsPanel
{
    /// <summary>The floating card, hidden until shown; the window places it at the right.</summary>
    public Control Card { get; }

    /// <summary>The list, made again by <see cref="Show"/>.</summary>
    internal StackPanel Rows { get; } = new() { Spacing = 3 };

    /// <summary>How many building pieces stand on the plot, beside the title.</summary>
    internal TextBlock Count { get; } = new() { FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

    /// <summary>Raised when the card is closed with its close button.</summary>
    public event Action? Closed;

    /// <summary>The card, empty until shown.</summary>
    public MaterialsPanel()
    {
        var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right }.Classed("ghost").Tip("card.close");
        close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
        var title = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 0, 0, 6),
            Children = { Ui.Title(Lang.T("materials.title"), HelpTopics.Workshop), Ui.Col(Count, 1), Ui.Col(close, 2) },
        };
        DockPanel.SetDock(title, Dock.Top);
        // The list scrolls within the room the window leaves it (above the Build dock), a long one included.
        Card = Ui.Card(new DockPanel
        {
            Width = 260,
            Children = { title, new ScrollViewer { Content = Rows } },
        });
        Card.IsVisible = false;
        Show(new Dictionary<string, int>());
    }

    /// <summary>
    /// Lists what the building costs: its materials, the stations, the pieces with no known recipe and those the hammer
    /// skips.
    /// </summary>
    /// <param name="kinds">How many of each kind of building piece stand on the plot (prefab name → count).</param>
    public void Show(IReadOnlyDictionary<string, int> kinds)
    {
        Rows.Children.Clear();
        int pieces = kinds.Values.Sum();
        Count.Text = pieces == 0 ? "" : Lang.T("workshop.bar.pieces", pieces);
        if (pieces == 0)
        {
            Rows.Children.Add(Ui.Hint(Lang.T("materials.empty")));
            return;
        }
        var cost = Homestead.Cost(kinds);
        if (cost.Materials.Count > 0)
        {
            Rows.Children.Add(Ui.Heading(Lang.T("materials.head.materials"), 2));
            foreach (var (_, name, amount) in cost.Materials)
            {
                Rows.Children.Add(Ui.Counted(name, amount));
            }
        }
        if (cost.Stations.Count > 0)
        {
            Rows.Children.Add(Ui.Heading(Lang.T("materials.head.stations")));
            foreach (string station in cost.Stations)
            {
                Rows.Children.Add(new TextBlock { Text = station, TextWrapping = TextWrapping.Wrap });
            }
        }
        // What the hammer skips: build pieces whose recipe is not known (a mod's, before a live game told it), and the
        // rest (statues, ships, creatures), which only the editor places.
        var skipped = kinds.Where(k => !Homestead.HammerPlaces(k.Key)).ToList();
        var noRecipe = skipped.Where(k => NoRecipe(k.Key)).ToList();
        if (noRecipe.Count > 0)
        {
            Rows.Children.Add(Ui.Heading(Lang.T("materials.head.noRecipe")));
            foreach (var g in noRecipe.GroupBy(u => BuildPanel.ModGroup(u.Key)).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (g.Key.Length > 0)
                {
                    Rows.Children.Add(new TextBlock { Text = g.Key, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 2, 0, 0) });
                }
                AddCounted(g);
            }
        }
        var editorOnly = skipped.Except(noRecipe).ToList();
        if (editorOnly.Count > 0)
        {
            Rows.Children.Add(Ui.Heading(Lang.T("materials.head.editorOnly")));
            AddCounted(editorOnly);
            Rows.Children.Add(Ui.Hint(Lang.T("materials.editorOnlyHint")));
        }
    }

    /// <summary>Lists pieces by English name with how many there are, most first.</summary>
    private void AddCounted(IEnumerable<KeyValuePair<string, int>> pieces)
    {
        foreach (var (prefab, n) in pieces.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            Rows.Children.Add(Ui.Counted(PieceCost.PieceName(prefab), n));
        }
    }

    /// <summary>A build piece (in a build tool's menu) whose cost the editor does not know.</summary>
    internal static bool NoRecipe(string prefab) =>
        PieceCatalog.Get(StableHash.Of(prefab))?.Tool != null && PieceCost.Get(prefab) is not { Resources.Length: > 0 };
}
