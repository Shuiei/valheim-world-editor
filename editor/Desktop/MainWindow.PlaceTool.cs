using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Gives the Place tool what it reads from the window: the brush, the scene, the mask, names and model boxes.
    /// </summary>
    private void SetUpPlaceTool()
    {
        PlaceTool.Brush = Tools.Brush;
        PlaceTool.Scene = () => View.Scene;
        PlaceTool.Mask = () => Session?.MaskNow();
        PlaceTool.NameOf = NameOfPrefab;
        PlaceTool.ModelBox = ModelBox;
    }

    /// <summary>
    /// Wires the Place panel's messages and its preset dialogs.
    /// </summary>
    private void WirePlacePanel()
    {
        PlacePanel.Message += Msg;
        PlacePanel.AskName = () => Dialogs.AskText(this, Lang.T("place.ask.savePreset.title"), Lang.T("place.ask.savePreset.text"));
        PlacePanel.Confirm = text => Dialogs.Ask(this, Lang.T("place.ask.deletePreset.title"), text, Lang.T("common.delete"));
    }

    /// <summary>
    /// The Help card's Record frame rates switch: frame rates written to the log file while on.
    /// </summary>
    private CheckBox RecordBox()
    {
        var record = new CheckBox { Content = Lang.T("help.label.record"), FontSize = 12.5 };
        record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) { _perf.Flush(); } };
        ToolTip.SetTip(record, Lang.T("view.perfFile", Tips.Of("view.perf"), Ui.Tilde(PerfLog.FilePath)));
        return record;
    }

    /// <summary>
    /// The column of tools on the left: the rail, the tool's options and every tool's panel, each scrolling when the
    /// window is too short; the panels start hidden until their tool is chosen.
    /// </summary>
    private StackPanel BuildToolColumn()
    {
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Ui.Gap,
            // Under the top bar, above the status bar.
            Margin = new Thickness(10, 70, 10, 58),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ModToolPanel.Card, DungeonPanel.Card, LandformPanel.Card, ScriptPanel.Card, Blueprints.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card, PlacePanel.Card, PlacePanel.Chooser, BuildingBar.Card, MaskPanel.Card },
        };
        // Every panel of the column scrolls when the window is too short for it (a bar only then).
        foreach (var card in tools.Children.OfType<Border>())
        {
            if (card.Child is Control inner and not ScrollViewer)
            {
                card.Child = null;
                card.Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = inner };
            }
        }
        SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ModToolPanel.Card.IsVisible = DungeonPanel.Card.IsVisible = LandformPanel.Card.IsVisible = ScriptPanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = PlacePanel.Card.IsVisible = false;
        Tools.Options.VerticalAlignment = VerticalAlignment.Top;
        Tools.Rail.VerticalAlignment = VerticalAlignment.Top;
        return tools;
    }

    /// <summary>
    /// Wires the Landform panel to the 3D view's preview and clicks, to the brushes' stamps (its Picture kind's
    /// pictures) and to No limit.
    /// </summary>
    private void WireShapePanels()
    {
        LandformPanel.Changed += ShowLandform;
        View.LandformClicked += OnLandformClicked;
        LandformPanel.Picture.Fill(Tools.StampList);
        Tools.StampsChanged += () => LandformPanel.Picture.Fill(Tools.StampList);
        LandformPanel.Picture.LoadAsked += async () => await LoadStampAsync(forLandform: true);
        LandformPanel.Picture.ForgetAsked += stamp =>
        {
            if (!Tools.ForgetStamp(stamp))
            {
                Msg("brush.msg.notLoaded");
            }
        };
        MaskPanel.NoLimitChanged += LandformPanel.ShowNoLimit;
    }

    /// <summary>
    /// The Landform panel changed, or another tool was chosen: with the Landform tool on, the 3D view's outline and
    /// preview follow the kind and its settings; otherwise there is none.
    /// </summary>
    private void ShowLandform() => View.Landform = Tools.Mode == ToolMode.Landform ? LandformPanel.Ghost(() => Tools.Brush.Noise) : LandformGhost.None;

    /// <summary>
    /// A click with the Landform tool: puts the kind chosen's landform there (a formula's shape, a mountain or a
    /// picture).
    /// </summary>
    /// <param name="x">Where, east-west.</param>
    /// <param name="z">Where, north-south.</param>
    private void OnLandformClicked(float x, float z)
    {
        switch (LandformPanel.Kind)
        {
            case LandformKind.Mountain:
                _ = PutMountain(x, z);
                break;
            case LandformKind.Picture:
                PutPicture(x, z);
                break;
            default:
                PutShape(x, z);
                break;
        }
    }

    /// <summary>
    /// Wires the Script panel's Run, Stop, messages and its dialog.
    /// </summary>
    private void WireScriptPanel()
    {
        ScriptPanel.RunAsked += () => _ = RunScript();
        ScriptPanel.StopAsked += () => _stopScript?.Invoke();
        ScriptPanel.Message += Msg;
        ScriptPanel.Confirm = text => Dialogs.Ask(this, Lang.T("script.ask.replace.title"), text, Lang.T("script.ask.replace.yes"));
    }
}
