using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// One slot's button: its item's name and stack when it holds one, outlined when it is the picked slot.
    /// </summary>
    /// <param name="x">The slot's column.</param>
    /// <param name="y">The slot's row.</param>
    /// <returns>The button, a click on which picks the slot or moves to it.</returns>
    private Button SlotCell(int x, int y)
    {
        var at = (x, y);
        var it = Items!.FirstOrDefault(i => i.X == x && i.Y == y);
        var cell = new Button
        {
            Margin = new Thickness(1),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Background = it != null ? Ui.Panel2 : Ui.Bg,
            BorderThickness = new Thickness(_slot == at ? 2 : 1),
            BorderBrush = _slot == at ? new SolidColorBrush(Ui.AccentColor) : Ui.Line,
            Content = it == null ? null : new Grid
            {
                Children =
                {
                    new TextBlock { Text = SlotName(it.Name), FontSize = 9, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 },
                    new TextBlock { Text = it.Stack > 1 ? it.Stack.ToString(CultureInfo.InvariantCulture) : "", FontSize = 11, FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom },
                },
            },
        }.Tip("inspect.slot");
        if (it != null)
        {
            ToolTip.SetTip(cell, $"{it.Name} × {it.Stack} (slot {x + 1}, {y + 1})");
        }
        cell.Click += (_, _) => SlotClicked(at);
        return cell;
    }

    /// <summary>
    /// An item's name as a slot shows it: an invisible break between the words of a name written as one
    /// ("Iron|Scrap", "Trophy|Deer"), so a narrow slot wraps it there rather than in the middle of a word.
    /// </summary>
    /// <param name="name">The item's name.</param>
    internal static string SlotName(string name)
    {
        var b = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && ((char.IsUpper(name[i]) && char.IsLower(name[i - 1])) || char.IsDigit(name[i]) != char.IsDigit(name[i - 1]) || name[i - 1] == '_'))
            {
                b.Append('\u200B');
            }
            b.Append(name[i]);
        }
        return b.ToString();
    }

    /// <summary>
    /// A click on a slot: with Move on and another slot picked, the item moves there (or the two swap); the slot
    /// clicked becomes the picked one.
    /// </summary>
    /// <param name="at">The slot clicked.</param>
    private void SlotClicked((int X, int Y) at)
    {
        if (_moving && _slot is var (fx, fy) && _slot != at)
        {
            SwapSlots(Items!, (fx, fy), at);
            Dirty();
        }
        _moving = false;
        _slot = at;
        RenderItems();
    }

    /// <summary>
    /// Moves the item in one slot to the other, and the item in the other to the first (where there are items).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="from">The slot the move starts from.</param>
    /// <param name="to">The slot it goes to.</param>
    internal static void SwapSlots(List<ItemRow> items, (int X, int Y) from, (int X, int Y) to)
    {
        var fromItem = items.FirstOrDefault(i => i.X == from.X && i.Y == from.Y);
        var toItem = items.FirstOrDefault(i => i.X == to.X && i.Y == to.Y);
        if (fromItem != null)
        {
            (fromItem.X, fromItem.Y) = to;
        }
        if (toItem != null)
        {
            (toItem.X, toItem.Y) = from;
        }
    }
}
