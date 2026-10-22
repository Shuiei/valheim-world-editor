namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The documentation's place for what the window shows (F1): the start page's, the map's, a new world's; in the
    /// 3D editor the Inspector's when it is open, the Workshop's, live mode's while looking around a running game, and
    /// otherwise the tool in use.
    /// </summary>
    internal HelpTarget HelpTopic()
    {
        object? page = _pages.Content;
        if (StartPage != null && page == StartPage.View)
        {
            return HelpTopics.StartPage;
        }
        if (MapShown)
        {
            return HelpTopics.Map;
        }
        if (NewWorld != null && page == NewWorld.View)
        {
            return HelpTopics.NewWorld;
        }
        if (Inspector.Card.IsVisible)
        {
            return HelpTopics.Inspector;
        }
        return Tools.Mode switch
        {
            ToolMode.View or ToolMode.Place when InWorkshop => HelpTopics.Workshop,
            ToolMode.View when World?.IsLive == true => HelpTopics.Live,
            _ => HelpTopics.Tool(Tools.Mode, Tools.Tool),
        };
    }

    /// <summary>F1, on any page and while typing: the Help window at what the window shows (HelpTopic).</summary>
    /// <returns>Whether the key was F1.</returns>
    private bool HelpKey(Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.F1 || e.KeyModifiers != Avalonia.Input.KeyModifiers.None)
        {
            return false;
        }
        HelpWindow.Open(HelpTopic());
        e.Handled = true;
        return true;
    }
}
