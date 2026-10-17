using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>Home's card for the running game (empty while it does not run).</summary>
    private readonly ContentControl _running = new();
    /// <summary>Home's error line (a server tile that could not connect).</summary>
    private readonly TextBlock _homeError = Err();
    /// <summary>The chosen page's title.</summary>
    private readonly TextBlock _pageTitle = new() { FontSize = 26, FontWeight = FontWeight.SemiBold };
    /// <summary>What the chosen page is for (hidden when empty).</summary>
    private readonly TextBlock _pageText = new() { Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
    /// <summary>The "running" tag beside My game in the menu.</summary>
    private readonly TextBlock _gameTag = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(123, 196, 127)), VerticalAlignment = VerticalAlignment.Center };
    /// <summary>A card per world found on this computer.</summary>
    internal WrapPanel WorldCards { get; } = new() { ItemSpacing = 10, LineSpacing = 10 };
    /// <summary>Looks for the worlds again (folders added or removed while the editor runs).</summary>
    internal Button RefreshWorlds { get; } = new Button { Content = Icons.With("reload", Lang.T("start.label.refresh")), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left }.Classed("ghost");
    /// <summary>Another world folder to open.</summary>
    internal TextBox PathBox { get; } = new() { PlaceholderText = Lang.T("start.hint.folder"), FontSize = 13 };
    /// <summary>Why that folder cannot be opened.</summary>
    internal TextBlock PathError { get; } = Err();
    /// <summary>The error that brought the window back here (a world that could not open), above every page.</summary>
    private readonly TextBlock _lastError = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, IsVisible = false };
    /// <summary>Polls whether the game runs (on Home and My game): every 2.5 s, 5 s while it runs.</summary>
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    /// <summary>Looks for the worlds again every 3 s while A saved world shows, so added or removed worlds show by themselves.</summary>
    private readonly DispatcherTimer _worldsTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    /// <summary>The worlds as last shown (path, save, usable), so an unchanged look redraws nothing.</summary>
    private string _worldsKey = "";
    /// <summary>The look for the worlds running, or the last one (Refresh's or the timer's; see <see cref="CheckWorlds"/>).</summary>
    internal Task? WorldsCheck { get; private set; }
    /// <summary>Counts the looks for the worlds, so a look overtaken by a later one shows nothing.</summary>
    private int _worldsLook;
    /// <summary>Counts the fills of Home's tiles, so a fill overtaken by a later one shows nothing.</summary>
    private int _homeLook;
    /// <summary>Counts the fills of the Workshop's tiles, likewise.</summary>
    private int _workshopLook;
    /// <summary>The blueprint pictures Home's tiles show, disposed when the tiles are made again.</summary>
    private readonly List<Bitmap> _homePictures = [];
    /// <summary>The blueprint pictures the Workshop's tiles show, likewise.</summary>
    private readonly List<Bitmap> _workshopPictures = [];

    /// <summary>Homestead (the building library's in-game side): found in the game's BepInEx or not.</summary>
    internal TextBlock HomesteadText { get; } = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>Opens Homestead's page; shown while it is not installed.</summary>
    internal Button GetHomesteadButton { get; } = new() { Content = Lang.T("blueprints.label.getHomestead"), FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false };
    /// <summary>Why the game could not be checked (the folders or the plugin), until a check works again.</summary>
    private readonly TextBlock _gameError = Err();

}
