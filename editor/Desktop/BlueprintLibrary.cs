using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// The blueprint library as it is listed, the same in the two places that show it: the Blueprints panel of a world and
/// the Library tab of the Workshop's Build dock. One search, one list of rows (picture, name, what it holds, cost,
/// description, tags) and the same buttons on each row: put it down (Paste in a world; Add, or dragged, onto the
/// Workshop's plot), Open in the Workshop, Details, .vbuild and Delete; the editor's older blueprints after Homestead's,
/// with To Homestead. What a button does is its host's (the events): the list only shows and asks.
/// </summary>
internal sealed partial class BlueprintLibrary
{
    /// <summary>Where a library is shown: in a world's Blueprints panel, or in the Workshop's Build dock.</summary>
    internal enum Places
    {
        /// <summary>A world: a blueprint is pasted.</summary>
        World,

        /// <summary>The Workshop: a blueprint is added to the plot (its button, or dragged onto it).</summary>
        Workshop,
    }

    /// <summary>A blueprint of the list.</summary>
    /// <param name="Id">Its id: <see cref="HomesteadId"/> and its file name, or an older one's store id.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Entry">Homestead's blueprint (its file, details and pieces), null for an older one.</param>
    internal sealed record Item(string Id, string Name, Homestead.Entry? Entry);

    /// <summary>Homestead's blueprints have ids "hs:&lt;file name&gt;"; the editor's older ones their store id.</summary>
    internal const string HomesteadId = "hs:";

    /// <summary>What a blueprint dragged from the list carries: its file.</summary>
    internal static readonly DataFormat<string> DragFormat = DataFormat.CreateInProcessFormat<string>("vwe-blueprint");

    /// <summary>The label of the .vbuild buttons: a file format's name, the same in every language.</summary>
    private const string VbuildLabel = ".vbuild";

    /// <summary>A row's picture's side, in pixels.</summary>
    internal const double PictureSize = 72;

    /// <summary>Where this library is shown.</summary>
    internal Places Place { get; }

    /// <summary>Filters the list: every word somewhere in a blueprint's name, creator, description or tags.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("blueprints.hint.search"), FontSize = 12 };

    /// <summary>Imports another mod's blueprint file (the host picks the file and imports it).</summary>
    internal Button ImportButton { get; } = new() { Content = Lang.T("blueprints.label.import"), FontSize = 12 };

    /// <summary>A blank plot in the Workshop, to build a new blueprint on (the host opens it).</summary>
    internal Button NewButton { get; } = new() { Content = Lang.T("blueprints.label.new"), FontSize = 12 };

    /// <summary>The blueprints' rows, side by side where there is room (the text shown instead when there are none).</summary>
    internal WrapPanel List { get; } = new() { ItemSpacing = 8, LineSpacing = 6 };

    /// <summary>A row's width, in pixels.</summary>
    internal double RowWidth { get; }

    /// <summary>Homestead's blueprint folder.</summary>
    internal Func<string> Folder { get; set; } = () => "";

    /// <summary>The editor's own blueprints from before (null: none are listed).</summary>
    internal Func<BlueprintStore?> Older { get; set; } = () => null;

    /// <summary>The game's models, for the pictures drawn (null: each piece's boxes).</summary>
    internal Func<ModelStore?> Models { get; set; } = () => null;

    /// <summary>
    /// A row's button was clicked: put it down (Paste, or Add in the Workshop), Open (a Homestead blueprint in the
    /// Workshop), Details, .vbuild, Delete, To Homestead (an older one).
    /// </summary>
    internal event Action<Item>? PlaceAsked, OpenAsked, DetailsAsked, ExportAsked, DeleteAsked, ToHomesteadAsked;

    /// <summary>The blueprints last read, and from which folder: searched through again as the search changes.</summary>
    private (string Folder, List<Homestead.Entry> Homestead, List<BlueprintStore.Summary> Older)? _read;

    /// <summary>The list's children that take a whole line (the older blueprints' heading), kept as wide as the list.</summary>
    private readonly List<Control> _wholeLines = [];

    /// <summary>
    /// Pictures of blueprints without one (made elsewhere), drawn from their pieces, by file and its last write time.
    /// Kept in memory only (the folder is left as it is).
    /// </summary>
    private readonly Dictionary<(string, DateTime), Bitmap?> _drawn = [];

    /// <summary>Pictures being drawn, with the images waiting for each: a blueprint shown again meanwhile is not drawn twice.</summary>
    private readonly Dictionary<(string, DateTime), List<Image>> _drawing = [];

    /// <summary>Builds a library's list, empty until <see cref="Render"/>.</summary>
    /// <param name="place">Where it is shown.</param>
    /// <param name="rowWidth">A row's width, in pixels.</param>
    internal BlueprintLibrary(Places place, double rowWidth)
    {
        Place = place;
        RowWidth = rowWidth;
        Search.Tip("blueprints.search");
        ImportButton.Tip("blueprints.import");
        NewButton.Tip("blueprints.new");
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Render(again: false); } };
        List.SizeChanged += (_, e) =>
        {
            foreach (var line in _wholeLines)
            {
                line.Width = e.NewSize.Width;
            }
        };
    }

    /// <summary>The id of a Homestead blueprint, from its file.</summary>
    internal static string IdOf(Homestead.Entry e) => HomesteadId + Path.GetFileName(e.Path);

    /// <summary>The blueprints as last read: Homestead's and the editor's older ones (none before the first <see cref="Render"/>).</summary>
    internal (IReadOnlyList<Homestead.Entry> Homestead, IReadOnlyList<BlueprintStore.Summary> Older) Listed =>
        _read is { } read ? (read.Homestead, read.Older) : ([], []);
}
