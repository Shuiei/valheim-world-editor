using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The window is closing: unsaved changes (or an unsaved Workshop building) keep it open and ask first; quitting
    /// anyway closes it again. Otherwise the tunnel to a live game is closed.
    /// </summary>
    /// <param name="e">The closing, cancelled to ask.</param>
    private async Task OnClosingAsync(WindowClosingEventArgs e)
    {
        var pending = World?.Pending ?? Session?.Pending ?? (0, 0, 0, 0);
        if (InWorkshop && !_closeAnyway)
        {
            if (WorkshopDirty)
            {
                e.Cancel = true;
                if (await Ask(Lang.T("save.ask.quitBuilding.title"), Lang.T("save.ask.quitBuilding.text"), Lang.T("save.ask.quit.yes"), Lang.T("common.keepBuilding")))
                {
                    _closeAnyway = true;
                    Close();
                }
            }
            return;
        }
        if (_closeAnyway || pending is (0, 0, 0, 0))
        {
            Tunnel.Close();
            return;
        }
        e.Cancel = true;
        string question = World?.IsLive == true ? Lang.T("save.ask.quitLive.text", Describe(pending)) : Lang.T("save.ask.quit.text", Describe(pending));
        if (await Ask(Lang.T("save.ask.quit.title"), question, Lang.T("save.ask.quit.yes"), Lang.T("save.ask.quit.no")))
        {
            _closeAnyway = true;
            Close();
        }
    }

    /// <summary>
    /// The panels on the right: under the top bar, one at a time, the View panel shown first.
    /// </summary>
    private void LayOutRightPanels()
    {
        foreach (var right in new[] { ViewPanelCard, History.Card, Inspector.Card, HelpCard })
        {
            right.Margin = new Thickness(10, 70, 10, 58);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Top;
        }
        ViewButton.Classes.Add("on");
    }
}
