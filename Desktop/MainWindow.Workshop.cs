using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Workshop in the window (Workshop.cs): a blank plot opened like an area, its building saved as a
// Homestead blueprint (Save blueprint), back to the start page (Start), and the support check: each
// building piece outlined in the colour the game gives its support, pieces that would fall in pink.
public partial class MainWindow
{
    /// <summary>
    /// The cut: the building seen from a height (1 m steps, a half wall's height), to build inside it. 0..40 m above
    /// the plot, 0 the ground; the right end (<see cref="CutOff"/>) is off.
    /// </summary>
    internal Slider CutSlider { get; } = new() { Minimum = 0, Maximum = CutOff, Value = CutOff, SmallChange = 1, LargeChange = 2, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 120, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// The cut slider's right end: no cut.
    /// </summary>
    internal const double CutOff = 40;
    /// <summary>
    /// The cut's height, or that it is off.
    /// </summary>
    internal TextBlock CutText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 74 };
    /// <summary>
    /// The cut's slider and text, shown in the Workshop.
    /// </summary>
    internal StackPanel CutBox { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false };

    /// <summary>
    /// Turns the support check on or off; shown in the Workshop.
    /// </summary>
    internal CheckBox SupportBox { get; } = new CheckBox { Content = Lang.T("workshop.label.support"), FontSize = 12, IsVisible = false, IsChecked = true, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");

    /// <summary>
    /// The building's name (null: not named yet, "My building" when saved).
    /// </summary>
    private string? _workshopName;
    /// <summary>
    /// The blueprint's details (name, description, tags); null for a blank plot.
    /// </summary>
    private Homestead.Details? _workshopDetails;
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
    /// Wires the cut slider, the support switch and the Blueprints panel's Edit to the Workshop.
    /// </summary>
    private void SetUpWorkshop()
    {
        SupportBox.Tip("workshop.support");
        CutBox.Children.Add(CutSlider);
        CutBox.Children.Add(CutText);
        CutSlider.Tip("workshop.cut");
        CutSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty)
            {
                SetCut((float)CutSlider.Value);
            }
        };
        CutText.Text = Lang.T("workshop.cutOff");
        SupportBox.IsCheckedChanged += (_, _) => RefreshSupport();
        Blueprints.EditAsked += async path => await OpenWorkshop(path);
    }

    /// <summary>
    /// Wires the Build panel's Library (once the panel is made): open, add and import blueprints, and dungeons onto
    /// the plot.
    /// </summary>
    private void SetUpLibrary()
    {
        BuildPanel.Folder = () => Blueprints.Status.Folder;
        BuildPanel.OpenAsked += async path => await OpenWorkshop(path);
        BuildPanel.AddAsked += path => AddToWorkshop(path, null);
        BuildPanel.Dungeon.PlotAsked += async (settings, result, add) => await PlotDungeon(settings, result, add);
        BuildPanel.ImportAsked += async () =>
        {
            if (await Blueprints.PickFile() is string file && Blueprints.Import(file) != null)
            {
                BuildPanel.RenderLibrary();
            }
        };
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
