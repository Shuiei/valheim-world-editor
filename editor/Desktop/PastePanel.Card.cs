using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The paste panel's layout: the card with its rows, the repeat and transform buttons, and the hint.
/// </summary>
public sealed partial class PastePanel
{
    /// <summary>Lays out the panel's card, with its controls in their rows.</summary>
    /// <returns>The card, ready to add to the window.</returns>
    private Border BuildCard()
    {
        // The rows: label, input and, for lengths, the unit after it.
        static Control Row(string label, Control input, bool metres = false) => Ui.Row(label, input,
            metres ? new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center } : null,
            "70,*,Auto");
        return Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 290,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("paste.label.paste"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Info, GroundBox, ObjectsBox, ClearSiteBox,
                    Row(Lang.T("common.height"), OffsetBox, metres: true),
                    new TextBlock { Text = Lang.T("paste.head.repeat"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 4, 0, 0) },
                    Row(Lang.T("paste.label.copies"), CopiesBox),
                    Row(Lang.T("paste.label.along"), AlongBox),
                    Row(Lang.T("paste.label.gap"), GapBox, metres: true),
                    new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Children = { TurnButton, MirrorButton, DoneButton } },
                    new TextBlock
                    {
                        Text = Lang.T("paste.hint.click"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
    }
}
