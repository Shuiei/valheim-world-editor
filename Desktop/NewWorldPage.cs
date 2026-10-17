using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A new world (the start page's "A new world"): a name and a seed, the seed's world seen from above before it exists
/// (SeedPreview: the game's own generator, with where it will put the start, the bosses' altars and the traders), a
/// search through random seeds for one that suits (the start on a large landmass, the first Swamp or Mountain close,
/// Haldor near...), and Create: a normal Valheim world in the game's world folder. The game lays out its locations when
/// it first loads it, and generates its zones as players explore them; the editor edits what is generated.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The page.
    /// </summary>
    public Control View { get; }

    /// <summary>
    /// The Start button: back to the start page (what runs is stopped first).
    /// </summary>
    public event Action? BackRequested;

    /// <summary>
    /// Create, once checked: the world's folder (inside a world folder of the game), its name and seed.
    /// </summary>
    public event Action<string, string, string>? CreateRequested;

    /// <summary>
    /// The preview shown (null while it is made).
    /// </summary>
    internal SeedPreview.Preview? Shown { get; private set; }
    /// <summary>
    /// The search's results, best first, with their scores.
    /// </summary>
    internal List<(SeedPreview.Preview Preview, float Score)> Results { get; private set; } = [];
    /// <summary>
    /// The latest preview or search started, for tests to wait on.
    /// </summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;
    /// <summary>
    /// Cancel the search, the seed preview, and the dungeon finding.
    /// </summary>
    private CancellationTokenSource? _search, _preview, _dungeons;
    /// <summary>
    /// The seed shown or being made (Generate is greyed while the seed box holds it), or null.
    /// </summary>
    private string? _generated;
    /// <summary>
    /// The seed's map, in cells on a side.
    /// </summary>
    private readonly int _previewSize;
    /// <summary>
    /// The game's world folders (that exist, the local ones first), for Where.
    /// </summary>
    internal static List<string> Roots() => [.. Places.WorldRoots().Where(Directory.Exists).Distinct()];
    /// <summary>
    /// Builds the page and starts looking at a random seed.
    /// </summary>
    /// <param name="roots">The world folders to offer (tests); null for the game's own. With none, the first the game
    /// would use.</param>
    /// <param name="previewSize">The seed's map, in cells on a side (tests: smaller is quicker).</param>
    public NewWorldPage(IReadOnlyList<string>? roots = null, int previewSize = 512)
    {
        _previewSize = previewSize;
        Loading = BuildLoadingSign();
        var where = (roots ?? Roots()).ToList();
        if (where.Count == 0)
        {
            where.Add(Places.WorldRoots().First());
        }
        Where.ItemsSource = where.Select(Ui.Tilde).ToList();
        Where.Tag = where;
        Where.SelectedIndex = 0;
        SeedBox.Text = SeedPreview.RandomSeed();
        var back = new Button { Content = Icons.With("back", Lang.T("workshop.label.start")), FontSize = 12 }.Classed("ghost").Tip("newWorld.back");
        back.Click += (_, _) => { StopAll(); BackRequested?.Invoke(); };
        WireTips();
        WireSwitches();
        Map.ViewChanged += MoveMarks;
        Map.Hovered += (x, z) => Readout.Text = $"{x:0}, {z:0}";
        Find.Tip("newWorld.find");
        WireButtons();
        View = BuildPage(back);
        ShowSeed(SeedBox.Text);
    }

    /// <summary>
    /// Everything running stopped, and the page left as it is now (no loading sign left waiting for work that will not
    /// finish).
    /// </summary>
    internal void StopAll()
    {
        _search?.Cancel();
        _preview?.Cancel();
        StopDungeons();
        Loading.IsVisible = false;
        FillDungeons();
        // A seed being made is dropped: the one shown is the world on the map again.
        _generated = Shown?.Seed;
        FitGenerate();
    }
    /// <summary>
    /// Generate lit unless the seed box's world is the one shown or being made.
    /// </summary>
    private void FitGenerate() => Generate.IsEnabled = (SeedBox.Text?.Trim() ?? "") != _generated;
    /// <summary>
    /// Leaving the page: what runs is stopped.
    /// </summary>
    public void Dispose()
    {
        // Stopped, not disposed here: work still running reads their tokens; each is disposed when its work ends.
        StopAll();
        Map.Dispose();
    }
    /// <summary>
    /// Replaces the lists beside the map with one line.
    /// </summary>
    private void Note(string text)
    {
        Info.Children.Clear();
        Info.Children.Add(Ui.Hint(text));
    }
}
