using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>F1 and the panels' help buttons: the Help window at the documentation of what is in use.</summary>
public class MainWindowHelpTests
{
    [AvaloniaFact]
    public void F1OnTheStartPageOpensTheHelpsFirstPageEvenWhileTyping()
    {
        var w = new MainWindow(load: false);
        w.Show();
        try
        {
            w.ShowStart();
            Assert.Equal(HelpTopics.StartPage, w.HelpTopic());
            w.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(HelpTarget.Home, HelpWindow.Current?.Shown);
            // With a modifier it is not the help's key.
            HelpWindow.Current!.Close();
            w.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
            Assert.Null(HelpWindow.Current);
        }
        finally
        {
            HelpWindow.Current?.Close();
            w.Close();
        }
    }

    [AvaloniaFact]
    public void InTheEditorF1FollowsTheToolTheBrushAndTheInspector()
    {
        var (w, _, _) = ShownEditor.Open();
        try
        {
            w.Tools.ChooseMode(ToolMode.Path);
            Assert.Equal(new HelpTarget("path.md"), w.HelpTopic());
            w.Tools.Choose(BrushTool.PaintDirt);
            Assert.Equal(new HelpTarget("terrain.md", "paint-brushes"), w.HelpTopic());
            w.Tools.ChooseMode(ToolMode.View);
            Assert.Equal(HelpTopics.Tool(ToolMode.View), w.HelpTopic());
            w.Inspector.Card.IsVisible = true;
            Assert.Equal(HelpTopics.Inspector, w.HelpTopic());
            w.Inspector.Card.IsVisible = false;
            w.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None);
            Assert.Equal(HelpTopics.Tool(ToolMode.View), HelpWindow.Current?.Shown);
        }
        finally
        {
            HelpWindow.Current?.Close();
            w.Close();
        }
    }

    [AvaloniaFact]
    public void EveryPanelsHelpButtonOpensItsTopic()
    {
        var (w, _, _) = ShownEditor.Open();
        try
        {
            var buttons = w.GetLogicalDescendants().OfType<Button>().Where(b => Tips.KeyOf(b) == "docs.topic").ToList();
            // The tools' cards, the brush card, the Mask, View, History, Inspector and help cards, Blueprints and Build.
            Assert.True(buttons.Count >= 18, $"{buttons.Count} help buttons");
            var all = HelpTopics.All.ToHashSet();
            var shown = new HashSet<HelpTarget>();
            foreach (var b in buttons)
            {
                b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var at = HelpWindow.Current!.Shown;
                Assert.Contains(at, all);
                shown.Add(at);
            }
            Assert.Contains(HelpTopics.Mask, shown);
            Assert.Contains(HelpTopics.History, shown);
            Assert.Contains(new HelpTarget("place.md"), shown);
        }
        finally
        {
            HelpWindow.Current?.Close();
            w.Close();
        }
    }
}
