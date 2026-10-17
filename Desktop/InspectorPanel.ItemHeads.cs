using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>The column heads of every item's details: item, stack, quality, durability, X and Y.</summary>
    private static Grid ItemHeads()
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
            ColumnSpacing = 3,
            Children =
            {
                Head(Lang.T("inspect.data.item"), 0), Head(Lang.T("inspect.head.stack"), 1), Head(Lang.T("inspect.head.quality"), 2),
                Head(Lang.T("inspect.head.durability"), 3), Head(Lang.T("inspect.head.x"), 4), Head(Lang.T("inspect.head.y"), 5),
            },
        };
    }

    /// <summary>A column heading.</summary>
    private static Control Head(string t, int col) => Ui.Col(new TextBlock { Text = t, FontSize = 10, Foreground = Ui.Muted }, col);

    /// <summary>Outlines a number box in orange when <paramref name="bad"/> (a slot outside the container).</summary>
    private static NumericUpDown WithBorder(NumericUpDown n, bool bad)
    {
        if (bad)
        {
            n.BorderBrush = Brushes.Orange;
        }
        return n;
    }

    /// <summary>The first free slot, row by row (size unknown: 4 wide, 2 rows: the smallest containers).</summary>
    internal (int X, int Y)? FreeSlot()
    {
        var (w, h) = _grid;
        int gw = w > 0 ? w : 4, gh = h > 0 ? h : Math.Max(2, Items!.Select(i => i.Y + 1).DefaultIfEmpty(0).Max());
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                if (!Items!.Any(i => i.X == x && i.Y == y))
                {
                    return (x, y);
                }
            }
        }
        return null;
    }

    /// <summary>Whether a key typed to add (a name, or a hash as a number) is one a mod's manifest shows only.</summary>
    private static bool ReadOnlyKey(string key) => ModManifests.DataOf(ZdoKeys.NameOf(ZdoKeys.Parse(key)) ?? key)?.ReadOnly == true;

    /// <summary>Whether a mod's manifest asks for the contents to be shown only (its "items" value read-only).</summary>
    private static bool ItemsReadOnly => ModManifests.DataOf("items")?.ReadOnly == true;
}
