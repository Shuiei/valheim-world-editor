using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// Blueprints (WorldEdit's schematics): copies kept under a name, pasted again in any area or world. They
/// are Homestead's blueprints (Homestead.cs): .blueprint files with a picture in Homestead's folder of
/// Valheim's save folder, the same files Homestead lists in game, so a building saved here is built in
/// game with Homestead (and one saved in game is pasted here). The panel says whether Homestead is
/// installed in the game's BepInEx (or a mod manager profile), and warns when it is not. Blueprints
/// kept in the editor's own format before (the data folder's blueprints/) are still listed, and can be
/// moved to Homestead. Other mods' files (PlanBuild .blueprint, .vbuild) are imported, and written.
/// Each blueprint shows its picture (BuildingPicture), what it costs to build in game (PieceCost), its
/// description and tags; the search looks through all of them.
/// </summary>
public sealed partial class BlueprintsPanel
{
    /// <summary>The panel itself, hidden until opened.</summary>
    public Control Card { get; }

    /// <summary>The editor's own blueprints from before (blueprints/ of the data folder).</summary>
    public BlueprintStore Store { get; set; } = BlueprintStore.Default;

    /// <summary>Whether Homestead is installed, and its blueprint folder (tests give their own).</summary>
    internal Func<Homestead.Status> FindHomestead { get; set; } = () => Homestead.Find(AppSettings.Load());

    /// <summary>Filters the list: every word somewhere in a blueprint's name, creator, description or tags.</summary>
    internal TextBox Search { get; } = SearchBox();

    /// <summary>Imports another mod's blueprint file (see <see cref="Import"/>).</summary>
    internal Button ImportButton { get; } = ImportFileButton();

    /// <summary>Opens an empty Workshop to build a new blueprint in.</summary>
    internal Button NewButton { get; } = new() { Content = Lang.T("blueprints.label.new"), FontSize = 12 };

    /// <summary>Edit (a blueprint file) or New in Workshop (null): the window opens the Workshop.</summary>
    public event Action<string?>? EditAsked;

    /// <summary>Opens Homestead's download page; shown only while it is not installed.</summary>
    internal Button GetHomesteadButton { get; } = new() { Content = Lang.T("blueprints.label.getHomestead"), FontSize = 11, Padding = new Thickness(6, 1) };

    /// <summary>The blueprint rows.</summary>
    internal StackPanel List { get; } = new() { Spacing = 4 };

    /// <summary>Whether Homestead is installed, and what that means for the blueprints.</summary>
    internal TextBlock Banner { get; } = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap };

    /// <summary>The banner's box, coloured as a warning while Homestead is not installed.</summary>
    private readonly Border _bannerBox;

    /// <summary>Where the blueprints are kept.</summary>
    private readonly TextBlock _folder = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>The open world's scene, null when none is open.</summary>
    private readonly Func<WorldScene?> _scene;

    /// <summary>The paste tool whose clipboard blueprints are saved from and loaded onto.</summary>
    private readonly PasteTool _paste;

    /// <summary>A line for the status bar, in the user's language and in English (for Claude).</summary>
    public event Action<Said>? Message;

    /// <summary>✕: the window goes back to View.</summary>
    public event Action? Closed;

    /// <summary>A blueprint went onto the clipboard: the window starts pasting.</summary>
    public event Action? Pasting;

    /// <summary>Asks for a blueprint's name, description and tags (given the ones to start from, and what it costs).</summary>
    /// <remarks>Null: cancelled.</remarks>
    internal Func<Homestead.Details, string, Task<Homestead.Details?>> AskDetails { get; set; } = (_, _) => Task.FromResult<Homestead.Details?>(null);

    /// <summary>The game's models, for the blueprints' pictures (null: their boxes are drawn).</summary>
    internal Func<ModelStore?> Models { get; set; } = () => null;

    /// <summary>Asks a yes/no question (the window's dialog): before deleting or replacing a blueprint.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

    /// <summary>Asks for a blueprint file to import (the window's file picker).</summary>
    internal Func<Task<string?>> PickFile { get; set; } = () => Task.FromResult<string?>(null);

    /// <summary>Opens a web page in the browser.</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

    /// <summary>Homestead's blueprints have ids "hs:&lt;file name&gt;"; the editor's older ones their store id.</summary>
    private const string HomesteadId = "hs:";

    /// <summary>The editor's older blueprints, as last listed.</summary>
    private List<BlueprintStore.Summary> _older = [];

    /// <summary>Homestead's blueprints, as last listed.</summary>
    private List<Homestead.Entry> _homestead = [];

    /// <summary>Homestead as last looked for (on refresh and before saving or importing; first when needed).</summary>
    internal Homestead.Status Status { get => field ??= FindHomestead(); private set; }
}
