using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>My game's page: the game's state and what to do.</summary>
    internal StackPanel GamePanel { get; } = new() { Spacing = 8 };
    /// <summary>A dedicated server's page: saved servers, then the form.</summary>
    internal StackPanel ServerPanel { get; } = new() { Spacing = 8 };
    /// <summary>The form to connect to a new server (folded away once there are saved servers).</summary>
    internal Expander ServerForm { get; } = new() { Header = new TextBlock { Text = Lang.T("start.label.connectServer"), FontWeight = FontWeight.SemiBold }, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>A saved world's page: the worlds found, and another folder to open.</summary>
    internal StackPanel OfflinePanel { get; } = new() { Spacing = 8 };
    /// <summary>Making something new, apart from editing a world: a new world.</summary>
    internal Button NewWorldButton { get; } = new Button { Content = Lang.T("start.label.newWorld"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    /// <summary>Make a new world was clicked.</summary>
    public event Action? NewWorldRequested;
    /// <summary>Open the Workshop on a blank plot.</summary>
    internal Button NewBuildingButton { get; } = new Button { Content = Lang.T("start.label.openWorkshop"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left }.Classed("primary");
    /// <summary>The Workshop page's copy of Homestead's state.</summary>
    internal TextBlock WorkshopHomestead { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    // The pages the side menu shows (one at a time), and what Home and the Workshop list.
    /// <summary>Home's page: the running game's card, then the recent tiles.</summary>
    internal StackPanel HomePanel { get; } = new() { Spacing = 10 };
    /// <summary>The Workshop's page: open it, Homestead's state, the blueprints.</summary>
    internal StackPanel WorkshopPanel { get; } = new() { Spacing = 10 };
    /// <summary>A new world's page.</summary>
    internal StackPanel NewWorldPanel { get; } = new() { Spacing = 10 };
    /// <summary>Home's tiles: recent worlds and blueprints, then one-click servers.</summary>
    internal WrapPanel RecentTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
    /// <summary>The Workshop's tiles: every blueprint.</summary>
    internal WrapPanel BlueprintTiles { get; } = new() { ItemSpacing = 12, LineSpacing = 12 };
}
