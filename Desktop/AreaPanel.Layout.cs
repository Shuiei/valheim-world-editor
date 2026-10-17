using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>The import rows, shown once a picture is read: its facts, the lowest and highest heights, and Put or Cancel.</summary>
    /// <returns>The rows, hidden until a picture is read.</returns>
    private StackPanel BuildPictureRows() => new()
    {
        Spacing = 4,
        IsVisible = false,
        Children =
        {
            PictureInfo,
            Row(Lang.T("area.label.lowest"), LowestBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
            Row(Lang.T("area.label.highest"), HighestBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PutButton, CancelPictureButton } },
        },
    };

    /// <summary>The panel's card: its rows in order, each shown only for the actions it belongs to.</summary>
    /// <param name="softV">The soft edge's value text, shown next to its slider.</param>
    /// <param name="kinds">The object kind toggles.</param>
    /// <returns>The card, to be shown over the view.</returns>
    private Border BuildCard(TextBlock softV, WrapPanel kinds)
    {
        return Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 310,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("rail.label.area"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { BoxButton, PolyButton, ClearButton } },
                    Info,
                    Row(Lang.T("path.label.soft"), SoftSlider, softV),
                    Row(Lang.T("path.label.action"), ActionBox),
                    For(Row(Lang.T("common.height"), HeightBox, AverageButton), Act.Flatten),
                    For(Row(Lang.T("path.label.amount"), AmountBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }), Act.Raise, Act.Lower),
                    For(Row(Lang.T("common.paint"), PaintBox), Act.Paint),
                    For(NoLimitBox, Act.Flatten, Act.Raise, Act.Lower),
                    For(VolumeText, Act.Flatten, Act.Raise, Act.Lower),
                    For(Help(Lang.T("area.hint.smooth")), Act.Smooth),
                    For(Help(Lang.T("area.hint.natural")), Act.Natural),
                    For(Help(Lang.T("area.hint.restore")), Act.Restore),
                    For(Help(Lang.T("area.hint.erode")), Act.Erode),
                    For(kinds, Act.Remove, Act.Select, Act.Replace, Act.Regrow, Act.Backup),
                    For(Row(Lang.T("common.replace"), FromBox), Act.Replace),
                    For(Row(Lang.T("area.label.with"), ToBox, ToPickButton), Act.Replace),
                    For(Help(Lang.T("area.hint.regrow")), Act.Regrow),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { CopyButton, CutButton, PasteButton } }, Act.Copy),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SaveBlueprintButton, LibraryButton } }, Act.Copy),
                    For(ClipInfo, Act.Copy),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ExportButton, ImportButton } }, Act.Heightmap),
                    For(_pictureBox, Act.Heightmap),
                    For(Help(Lang.T("area.hint.heightmap")), Act.Heightmap),
                    For(Row(Lang.T("area.label.backup"), BackupBox), Act.Backup),
                    For(BackupGroundBox, Act.Backup),
                    For(BackupObjectsBox, Act.Backup),
                    For(BackupInfo, Act.Backup),
                    For(KeepBuildingsBox, Act.Reset),
                    For(ResetGroundBox, Act.Reset),
                    For(Help(Lang.T("area.hint.reset")), Act.Reset),
                    For(UnresetButton, Act.Reset),
                    ApplyButton,
                },
            },
        });
    }

    /// <summary>A labelled row: the label, the input and an optional control after it.</summary>
    /// <param name="label">The label's text.</param>
    /// <param name="input">The input, in the middle column.</param>
    /// <param name="after">A control after the input, or null.</param>
    /// <returns>The row.</returns>
    private static Grid Row(string label, Control input, Control? after = null) => Ui.Row(label, input, after, "70,*,Auto");

    /// <summary>Puts a control in the card, shown only for the given actions.</summary>
    /// <param name="c">The control.</param>
    /// <param name="acts">The actions it is shown for.</param>
    /// <returns>The control, for the card's list.</returns>
    private Control For(Control c, params Act[] acts)
    {
        _rows[c] = acts;
        return c;
    }

    /// <summary>A line of help text, muted and wrapped.</summary>
    /// <param name="t">The text.</param>
    /// <returns>The text block.</returns>
    private static TextBlock Help(string t) => new() { Text = t, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>Shows only the rows of the current action, and names the action on Apply and in the tips.</summary>
    private void ShowRows()
    {
        var act = Current;
        foreach (var (row, acts) in _rows)
        {
            row.IsVisible = acts.Contains(act);
        }
        ApplyButton.IsVisible = act is not (Act.Heightmap or Act.Copy);
        // The action's own explanation, on the list and on its button.
        string what = Tips.Of($"areaAct.{act}");
        ToolTip.SetTip(ActionBox, $"{Tips.Of("area.action")} {what}");
        ToolTip.SetTip(ApplyButton, Lang.T("area.applyTip", what));
        ApplyButton.Content = act switch
        {
            Act.Flatten => Lang.T("area.apply.flatten"),
            Act.Raise => Lang.T("area.apply.raise"),
            Act.Lower => Lang.T("area.apply.lower"),
            Act.Smooth => Lang.T("area.apply.smooth"),
            Act.Natural => Lang.T("area.apply.natural"),
            Act.Restore => Lang.T("area.apply.restore"),
            Act.Erode => Lang.T("area.apply.erode"),
            Act.Paint => Lang.T("area.apply.paint"),
            Act.Remove => Lang.T("area.apply.remove"),
            Act.Select => Lang.T("area.apply.select"),
            Act.Replace => Lang.T("area.apply.replace"),
            Act.Reset => Lang.T("area.apply.reset"),
            Act.Regrow => Lang.T("area.apply.regrow"),
            Act.Backup => Lang.T("area.apply.backup"),
            _ => "",
        };
        Refresh();
    }
}
