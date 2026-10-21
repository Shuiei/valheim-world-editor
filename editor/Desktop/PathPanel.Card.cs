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
    /// <summary>Builds the Natural look's bump rows (the brush's noise amplitude and size).</summary>
    /// <param name="brush">The brush whose noise the rows set.</param>
    /// <returns>The rows, shown only while Natural look is on.</returns>
    private static StackPanel BuildNaturalRows(Brush brush)
    {
        var ampV = new TextBlock();
        var sizeV = new TextBlock();
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Row(Lang.T("brush.label.bumps"), Slide(0.2, 4, 0.1, brush.NoiseAmp, ampV, "m", v => brush.NoiseAmp = v).Tip("natural.amp"), ampV),
                Ui.Row(Lang.T("brush.label.bumpSize"), Slide(4, 60, 1, brush.NoiseSize, sizeV, "m", v => brush.NoiseSize = v).Tip("natural.size"), sizeV),
            },
        };
    }

    /// <summary>Builds the card with every control, the rows of each action, and the hint.</summary>
    /// <returns>The card, ready to add to the window.</returns>
    private Border BuildCard()
    {
        Control Only(Control c, params PathTool.Action[] for_)
        {
            _rows[c] = for_;
            return c;
        }
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
                    new TextBlock { Text = Lang.T("rail.label.path"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Ui.Row(Lang.T("path.label.action"), ActionBox),
                    Ui.Row(Lang.T("path.label.width"), WidthSlider, _widthShown),
                    Ui.Row(Lang.T("path.label.soft"), SoftSlider, _softShown),
                    R(Lang.T("common.height"), HeightBox, PathTool.Action.Flatten),
                    R(Lang.T("path.label.amount"), AmountBox, PathTool.Action.Raise, PathTool.Action.Lower),
                    R(Lang.T("path.label.start"), StartBox, PathTool.Action.Ramp),
                    R(Lang.T("path.label.end"), EndBox, PathTool.Action.Ramp),
                    R(Lang.T("path.label.depth"), DepthBox, PathTool.Action.River),
                    Only(Ui.Row(Lang.T("common.preset"), CavePresetBox, CaveRandomButton), PathTool.Action.Cave),
                    Only(CaveHelp, PathTool.Action.Cave),
                    R(Lang.T("path.label.depth"), CaveDepthBox, PathTool.Action.Cave),
                    R(Lang.T("cave.label.headroom"), HeadroomBox, PathTool.Action.Cave),
                    Only(Ui.Row(Lang.T("cave.label.roof"), RockBox), PathTool.Action.Cave),
                    CurveBox,
                    NaturalBox,
                    _naturalRows,
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
