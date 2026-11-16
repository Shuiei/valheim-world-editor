using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Workshop in the window (Workshop.cs): a blank plot opened like an area, its building saved as a
// Homestead blueprint (Save blueprint), back to the start page (Start), and the support check: each
// building piece tinted the colour the game gives its support, pieces that would fall in red.
public partial class MainWindow
{
    /// <summary>
    /// Turns the support check on or off; shown in the Workshop.
    /// </summary>
    internal CheckBox SupportBox { get; } = new CheckBox { Content = Lang.T("workshop.label.support"), FontSize = 12, IsVisible = false, IsChecked = true, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");

    /// <summary>
    /// The building's name (null: not named yet, "My building" when saved).
    /// </summary>
    private string? _workshopName;
    /// <summary>
    /// The details of the blueprint opened or last saved (name, description, tags); null for a blank plot.
    /// </summary>
    internal Homestead.Details? WorkshopDetails { get; private set; }
    /// <summary>
    /// How far the blueprint opened was lifted to put its lowest point on the ground (taken off again when it is
    /// saved).
    /// </summary>
    private float _workshopLift;
    /// <summary>
    /// The ground the blueprints opened or added stood on in game (per metre; Workshop.GroundAt).
    /// </summary>
    private Dictionary<(int, int), float> _workshopTerrain = [];
    /// <summary>
    /// The file the blueprint was opened from or saved to (saving there again does not ask before replacing it); null
    /// when it is not in Homestead's own folder.
    /// </summary>
    private string? _workshopFile;
    /// <summary>
    /// The edits' version when the building was last saved (or opened).
    /// </summary>
    private int _workshopSaved;

    /// <summary>
    /// Whether the Workshop's plot is open.
    /// </summary>
    internal bool InWorkshop { get; private set; }
    /// <summary>
    /// The building on the plot changed since it was last saved as a blueprint (or opened).
    /// </summary>
    internal bool WorkshopDirty => InWorkshop && Session != null && Session.Edits.Version != _workshopSaved;

    /// <summary>
    /// The last support check (null: off, or not in the Workshop).
    /// </summary>
    internal Stability.Result? LastSupport { get; private set; }

    /// <summary>
    /// The things (indices in the scene) the last support check's pieces are, in its order.
    /// </summary>
    internal IReadOnlyList<int> LastSupportThings { get; private set; } = [];

    /// <summary>
    /// Wires the support switch and the Blueprints panel's Edit to the Workshop.
    /// </summary>
    private void SetUpWorkshop()
    {
        SupportBox.Tip("workshop.support");
        SupportBox.IsCheckedChanged += (_, _) => RefreshSupport();
        Blueprints.EditAsked += async path => await OpenWorkshop(path);
    }

    /// <summary>
    /// Wires the Build panel's Library (once the panel is made): the same blueprints and row buttons as the Blueprints
    /// panel, done by it (details, .vbuild, delete, to Homestead, import), with Add and Open onto the plot, a new
    /// blank plot, and dungeons onto the plot.
    /// </summary>
    private void SetUpLibrary()
    {
        var library = BuildPanel.Library;
        library.Folder = () => Blueprints.Status.Folder;
        library.Older = () => Blueprints.Store;
        library.OpenAsked += async b => await OpenWorkshop(b.Entry!.Path);
        library.PlaceAsked += b => AddToWorkshop(b.Entry!.Path, null);
        library.DetailsAsked += async b => await ChangeLibraryDetails(b.Entry!);
        library.ExportAsked += b => Blueprints.Export(b.Id, "vbuild");
        library.DeleteAsked += async b => await DeleteFromLibrary(b);
        library.ToHomesteadAsked += b => { if (Blueprints.ToHomestead(b.Id) != null) { BuildPanel.RenderLibrary(); } };
        library.NewButton.Click += async (_, _) => await OpenWorkshop(null);
        library.ImportButton.Click += async (_, _) =>
        {
            if (await Blueprints.PickFile() is string file && Blueprints.Import(file) != null)
            {
                BuildPanel.RenderLibrary();
            }
        };
        BuildPanel.Dungeon.PlotAsked += async (settings, result, add) => await PlotDungeon(settings, result, add);
        BuildPanel.RescanAsked += () => _ = ScanModPieces(again: true);
    }

    /// <summary>Whether a blueprint file is the one the plot's building was opened from, or last saved as.</summary>
    private bool IsWorkshopFile(string path) =>
        string.Equals(path, _workshopFile, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Details in the Workshop's Library: changed in the blueprint's file by the Blueprints panel. When it is the
    /// blueprint on the plot, the Workshop takes its new name, details and file, so Save blueprint writes that file.
    /// </summary>
    internal async Task ChangeLibraryDetails(Homestead.Entry e)
    {
        bool onPlot = IsWorkshopFile(e.Path);
        if (await Blueprints.ChangeDetails(e) is not var (details, path))
        {
            return;
        }
        if (onPlot)
        {
            _workshopName = details.Name;
            WorkshopDetails = details;
            _workshopFile = path;
            TitleText.Text = Lang.T("workshop.title", details.Name);
        }
        BuildPanel.RenderLibrary();
    }

    /// <summary>
    /// Delete in the Workshop's Library, by the Blueprints panel (it asks first). When the blueprint deleted is the one
    /// on the plot, the building there is no longer saved as a blueprint.
    /// </summary>
    internal async Task DeleteFromLibrary(BlueprintLibrary.Item b)
    {
        await Blueprints.Delete(b.Id, b.Name);
        if (b.Entry is { } e && IsWorkshopFile(e.Path) && !File.Exists(e.Path))
        {
            _workshopSaved = -1;
            UpdateSaveBar();
        }
        BuildPanel.RenderLibrary();
    }

    /// <summary>
    /// The Dungeon panel's generated dungeon onto the plot, alone or added to what is there; what goes wrong is said.
    /// </summary>
    /// <param name="settings">What it was made from.</param>
    /// <param name="result">The dungeon.</param>
    /// <param name="add">Added to the plot (true), or alone on a new one.</param>
    internal async Task PlotDungeon(DungeonGen.Settings settings, DungeonGen.Result result, bool add)
    {
        try
        {
            await GeneratedOntoPlot(settings, result, add);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "putting a generated dungeon on the Workshop's plot");
            Msg("workshop.msg.dungeonFailed", ex.Message);
        }
    }

    /// <summary>
    /// Drag and drop onto the 3D view: a blueprint from the Library, or .blueprint and .vbuild files from the
    /// computer's files (imported into the library first). In the Workshop each goes where it is dropped; in a world,
    /// files are only imported.
    /// </summary>
    private void SetUpDrops(Control surface)
    {
        DragDrop.SetAllowDrop(surface, true);
        surface.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            bool ours = e.DataTransfer.Contains(BuildPanel.BlueprintFormat) && InWorkshop || e.DataTransfer.Contains(DataFormat.File);
            e.DragEffects = ours ? DragDropEffects.Copy : DragDropEffects.None;
        });
        surface.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = (e.DataTransfer.TryGetFiles() ?? [])
                .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToList();
            Drop(e.DataTransfer.TryGetValue(BuildPanel.BlueprintFormat), files, e.GetPosition(surface), surface.Bounds.Size);
        });
    }
}
