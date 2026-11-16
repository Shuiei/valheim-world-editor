using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>Builds the panel.</summary>
    /// <param name="view">The view: its selection, scene and shown kinds.</param>
    /// <param name="session">The open edit session, null when no world is open.</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    /// <param name="brush">The brush whose bumps, pattern and rest angle Naturalize and Erode use (the sessions' brush).</param>
    public AreaPanel(GlView view, Func<EditSession?> session, Func<int, string?> nameOf, Brush brush)
    {
        _view = view;
        _session = session;
        _nameOf = nameOf;
        Area = view.Area;
        ActionBox = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 500 };
        var groups = BuildGroupButtons();
        var softV = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        SoftSlider = new Slider { Minimum = 0, Maximum = 20, SmallChange = 0.5, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = Area.Soft };
        softV.Text = $"{Area.Soft:0.#} m";
        SoftSlider.ValueChanged += (_, e) => { Area.Soft = (float)e.NewValue; softV.Text = $"{e.NewValue:0.#} m"; Refresh(); };
        BoxButton.Click += (_, _) => { Area.Box = true; Area.Clear(); ShowShape(); };
        PolyButton.Click += (_, _) => { Area.Box = false; Area.Clear(); ShowShape(); };
        ClearButton.Click += (_, _) => { Area.Clear(); Say(Said.Of("area.msg.cleared")); };
        // While the list is filled for another group, its selection is not a choice (ShowGroup says when it is).
        ActionBox.SelectionChanged += (_, _) => { if (!_filling && ActionBox.SelectedIndex >= 0) { ActionChanged(); } };
        BackupBox.SelectionChanged += OnBackupChosen;
        HeightBox.ValueChanged += (_, _) => Refresh();
        AmountBox.ValueChanged += (_, _) => Refresh();
        AverageButton.Click += (_, _) => UseAverageHeight();
        AverageButton.Tip("area.avg");
        ActionBox.Tip("area.action");
        BoxButton.Tip("area.box");
        PolyButton.Tip("area.poly");
        ClearButton.Tip("area.clear");
        SoftSlider.Tip("area.soft");
        HeightBox.Tip("area.height");
        AmountBox.Tip("area.amount");
        PaintBox.Tip("area.paint");
        OnlyBox.Tip("area.only");
        KeepBuildingsBox.Tip("area.keepBuildings");
        ResetGroundBox.Tip("area.resetGround");
        UnresetButton.Tip("area.unreset");
        UnresetAllButton.Tip("area.unresetAll");
        BackupBox.Tip("area.backup");
        BackupGroundBox.Tip("area.backupGround");
        BackupObjectsBox.Tip("area.backupObjects");
        CopyButton.Tip("area.copy");
        CutButton.Tip("area.cut");
        PasteButton.Tip("area.paste");
        SaveBlueprintButton.Tip("area.saveBlueprint");
        LibraryButton.Tip("area.library");
        LowestBox.Tip("area.lowest");
        HighestBox.Tip("area.highest");
        PutButton.Tip("area.put");
        CancelPictureButton.Tip("area.cancelPicture");
        var kinds = BuildKindToggles();
        ApplyButton.Click += async (_, _) => await Apply();
        CopyButton.Click += (_, _) => CopyAsked?.Invoke();
        CutButton.Click += (_, _) => CutAsked?.Invoke();
        PasteButton.Click += (_, _) => PasteAsked?.Invoke();
        SaveBlueprintButton.Click += (_, _) => SaveBlueprintAsked?.Invoke();
        LibraryButton.Click += (_, _) => LibraryAsked?.Invoke();
        ClipInfo.Text = Lang.T("area.hint.clip");
        ExportButton.Click += (_, _) => Say(ExportHeightmap() ?? Said.Empty);
        ExportButton.Tip("area.export");
        ImportButton.Click += async (_, _) => { if (await PickPicture() is string path) { LoadPicture(path); } };
        ImportButton.Tip("area.import");
        CancelPictureButton.Click += (_, _) => { _picture = null; _pictureBox.IsVisible = false; };
        PutButton.Click += (_, _) => PutPicture();
        UnresetButton.Click += (_, _) => CancelReset();
        UnresetAllButton.Click += (_, _) => CancelAllResets();
        Area.Changed += AreaChanged;
        Area.Changed += DropPicture;
        _pictureBox = BuildPictureRows();
        NewPatternButton = BrushRows.NewPattern(brush, Say);
        NaturalRows = BrushRows.Natural(brush, NewPatternButton);
        RestRow = BrushRows.Rest(brush, out var rest);
        RestSlider = rest;
        // Once the list has its tip: the row's label tells the same.
        _actionRow = Row(Lang.T("path.label.action"), ActionBox);
        Card = BuildCard(softV, groups, kinds);
        ShowShape();
        ShowGroup(Group.Ground);
    }

    /// <summary>The group buttons: one per group, side by side, each showing its group's actions.</summary>
    /// <returns>The row holding the buttons.</returns>
    private Grid BuildGroupButtons()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Groups.Select(_ => "*"))), ColumnSpacing = 4 };
        foreach (var g in Groups)
        {
            var b = new Button { Content = GroupLabel(g), FontSize = 12, Padding = new Thickness(4, 4), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            b.Tip(GroupTip(g));
            b.Click += (_, _) => ShowGroup(g);
            GroupButtons[g] = b;
            row.Children.Add(Ui.Col(b, row.Children.Count));
        }
        return row;
    }

    /// <summary>A group button's tip key.</summary>
    /// <param name="g">The group.</param>
    private static string GroupTip(Group g)
    {
        return g switch
        {
            Group.Ground => "area.group.ground",
            Group.Objects => "area.group.objects",
            Group.Recover => "area.group.recover",
            _ => "area.group.clipboard",
        };
    }

    /// <summary>The Average button: puts the average ground height inside the selection into the height box.</summary>
    private void UseAverageHeight()
    {
        if (_session()?.Ground is { } g && Area.Average(g) is float avg)
        {
            HeightBox.Value = (decimal)MathF.Round(avg, 1);
        }
    }

    /// <summary>The object kind toggles: one per kind, in a wrapping row, each adding or removing its kind from the selection's kinds.</summary>
    /// <returns>The row holding the toggles.</returns>
    private WrapPanel BuildKindToggles()
    {
        var kinds = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        foreach (var k in ObjectKinds.All)
        {
            var b = new ToggleButton { Content = ObjectKinds.Label(k), IsChecked = Area.Kinds.Contains(k), FontSize = 11, Padding = new Thickness(6, 2) };
            ToolTip.SetTip(b, $"{Tips.Kind(k)} {Tips.Of("area.kind")}");
            b.IsCheckedChanged += (_, _) =>
            {
                if (b.IsChecked == true)
                {
                    Area.Kinds.Add(k);
                }
                else
                {
                    Area.Kinds.Remove(k);
                }
                // The Only list offers the objects of the chosen kinds.
                Refresh();
            };
            KindButtons[k] = b;
            kinds.Children.Add(b);
        }
        return kinds;
    }
}
