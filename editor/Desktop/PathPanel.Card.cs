using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool panel's layout: the card with its rows, each shown only for the actions that use it.
/// </summary>
public sealed partial class PathPanel
{
    /// <summary>Builds the card with every control, the rows of each action, and the hint.</summary>
    /// <returns>The card, ready to add to the window.</returns>
    private Border BuildCard()
    {
        Control R(string label, NumericUpDown box, params PathTool.Action[] for_)
        {
            var r = Ui.Row(label, box, M());
            _rows[r] = for_;
            return r;
        }
        return Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    Ui.Title(Lang.T("rail.label.path"), HelpTopics.Tool(ToolMode.Path)),
                    Ui.Row(Lang.T("path.label.action"), ActionBox),
                    _actionHelp,
                    Ui.Row(Lang.T("path.label.width"), WidthSlider, _widthShown),
                    Ui.Row(Lang.T("path.label.soft"), SoftSlider, _softShown),
                    R(Lang.T("common.height"), HeightBox, PathTool.Action.Flatten),
                    R(Lang.T("path.label.amount"), AmountBox, PathTool.Action.Raise, PathTool.Action.Lower),
                    R(Lang.T("path.label.start"), StartBox, PathTool.Action.Ramp),
                    R(Lang.T("path.label.end"), EndBox, PathTool.Action.Ramp),
                    R(Lang.T("path.label.depth"), DepthBox, PathTool.Action.River),
                    CurveBox,
                    NaturalBox,
                    NaturalRows,
                    RestRow,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ClearButton } },
                    Info,
                    new TextBlock
                    {
                        Text = Lang.T("path.hint.draw"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
    }
}
