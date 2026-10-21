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
            Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ModToolPanel.Card, DungeonPanel.Card, ShapePanel.Card, MountainPanel.Card, ScriptPanel.Card, Blueprints.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card, PlacePanel.Card, PlacePanel.Chooser, BuildPanel.Card, MaskPanel.Card },
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
        SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ModToolPanel.Card.IsVisible = DungeonPanel.Card.IsVisible = ShapePanel.Card.IsVisible = MountainPanel.Card.IsVisible = ScriptPanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = PlacePanel.Card.IsVisible = false;
        Tools.Options.VerticalAlignment = VerticalAlignment.Top;
        Tools.Rail.VerticalAlignment = VerticalAlignment.Top;
        return tools;
    }

    /// <summary>
    /// Wires the Shape and Mountain panels to the 3D view's preview and clicks, and the Path panel's cave to the rail.
    /// </summary>
    private void WireShapePanels()
    {
        ShapePanel.Changed += () => View.ShapeRadius = ShapePanel.Radius;
        MountainPanel.Changed += OnMountainChanged;
        View.ShapeClicked += OnShapeClicked;
        Tools.CaveChosen += () =>
        {
            PathPanel.ChooseCave();
            Tools.MarkCave(true);
        };
        PathPanel.ActionChanged += a => Tools.MarkCave(a == PathTool.Action.Cave);
    }

    /// <summary>
    /// The Mountain panel changed: with the Mountain tool on, the preview follows.
    /// </summary>
    private void OnMountainChanged()
    {
        if (Tools.Mode == ToolMode.Mountain)
        {
            View.ShapeRadius = MountainPanel.Spec.Reach;
            View.MountainPreview = Mountain.Shape(MountainPanel.Spec);
        }
    }

    /// <summary>
    /// A click with the Shape or Mountain tool: puts the shape, or the mountain, there.
    /// </summary>
    /// <param name="x">Where, east-west.</param>
    /// <param name="z">Where, north-south.</param>
    private void OnShapeClicked(float x, float z)
    {
        if (Tools.Mode == ToolMode.Mountain)
        {
            _ = PutMountain(x, z);
        }
        else
        {
            PutShape(x, z);
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
