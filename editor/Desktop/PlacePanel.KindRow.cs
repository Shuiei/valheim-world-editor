using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// One kind's row in the list, and a category's header.
public sealed partial class PlacePanel
{
    /// <summary>One kind's row in its category: its checkbox, its favourite star, and what a spawn point or a nest spawns.</summary>
    /// <param name="n">The kind.</param>
    /// <param name="header">Redraws the category's header (its chosen count) when the kind is ticked or unticked.</param>
    private Control KindRow(string n, Action header)
    {
        var box = new CheckBox { Content = Plain(n), Tag = n, IsChecked = T.Chosen.Contains(n), FontSize = 12 }.Tip("place.kind");
        box.IsCheckedChanged += (_, _) =>
        {
            if (box.IsChecked == true)
            {
                // A kind no longer offered since the list was filled (the mods' catalogue changed): not chosen.
                if (NotOffered(n))
                {
                    box.IsChecked = false;
                    return;
                }
                if (!T.Chosen.Contains(n))
                {
                    T.Chosen.Add(n);
                }
            }
            else
            {
                T.Chosen.Remove(n);
            }

            header();
            Chosen(fillList: false);
        };
        bool fav = Memory.Favourites.Contains(n);
        var star = new Button { Content = fav ? "★" : "☆", FontSize = 12, Padding = new Thickness(4, 0), Background = Brushes.Transparent };
        ToolTip.SetTip(star, Lang.T(fav ? "place.favRemove" : "place.favAdd"));
        star.Click += (_, _) => { Memory.ToggleFavourite(n); FillList(); RenderChips(); };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Ui.Col(star, 1) } };
        // Spawn points and nests: what they spawn, under their name.
        return PrefabCatalog.SpawnOf(n) is { } spawn
            ? new StackPanel { Children = { row, new TextBlock { Text = spawn.Summary(), FontSize = 10.5, Foreground = Ui.Muted, Margin = new Thickness(26, -4, 0, 2), TextWrapping = TextWrapping.Wrap } } }
            : row;
    }

    /// <summary>A category's header: its name, and how many of its kinds are chosen out of its kinds.</summary>
    private Grid CategoryHeader(ObjectKind k, List<string> names) => new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Children =
        {
            new TextBlock { Text = ObjectKinds.Label(k).ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted },
            Ui.Col(new TextBlock { Text = names.Count(T.Chosen.Contains) is int on && on > 0 ? $"{on} / {names.Count}" : $"{names.Count}", FontSize = 10.5, Foreground = names.Any(T.Chosen.Contains) ? Ui.Accent : Ui.Muted, Margin = new Thickness(8, 0, 0, 0) }, 1),
        },
    };
}
