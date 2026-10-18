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
    public AreaPanel(GlView view, Func<EditSession?> session, Func<int, string?> nameOf)
    {
        _view = view;
        _session = session;
        _nameOf = nameOf;
        Area = view.Area;
        ActionBox = new ComboBox { ItemsSource = Actions.Select(ActionLabel).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 500 };
        var softV = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        SoftSlider = new Slider { Minimum = 0, Maximum = 20, SmallChange = 0.5, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = Area.Soft };
        softV.Text = $"{Area.Soft:0.#} m";
        SoftSlider.ValueChanged += (_, e) => { Area.Soft = (float)e.NewValue; softV.Text = $"{e.NewValue:0.#} m"; Refresh(); };
        BoxButton.Click += (_, _) => { Area.Box = true; Area.Clear(); ShowShape(); };
        PolyButton.Click += (_, _) => { Area.Box = false; Area.Clear(); ShowShape(); };
        ClearButton.Click += (_, _) => { Area.Clear(); Say(Said.Of("area.msg.cleared")); };
        // ShowRows refreshes the panel, the volume with it.
        ActionBox.SelectionChanged += (_, _) => { ShowRows(); if (Current == Act.Backup) { FillBackups(); } };
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
        FromBox.Tip("area.from");
        ToBox.Tip("area.to");
        KeepBuildingsBox.Tip("area.keepBuildings");
        ResetGroundBox.Tip("area.resetGround");
        UnresetButton.Tip("area.unreset");
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
        ToPickButton.Tip("area.pick");
        ToPickButton.Click += (_, _) => PickKindAsked?.Invoke(Lang.T("common.replace"), UsePicked);
        Area.Changed += AreaChanged;
        Area.Changed += DropPicture;
        NoLimitBox.Tip("brush.noLimit");
        NoLimitBox.IsCheckedChanged += (_, _) => NoLimitChanged?.Invoke(NoLimitBox.IsChecked == true);
        _pictureBox = BuildPictureRows();
        Card = BuildCard(softV, kinds);
        ShowShape();
        ShowRows();
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
            };
            KindButtons[k] = b;
            kinds.Children.Add(b);
        }
        return kinds;
    }
}
