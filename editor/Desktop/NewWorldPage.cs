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
    /// What the worlds looked at and made here follow: Expand World's rules from the folder found when the switch in
    /// Settings is on, else the game's own values; with a world generation mod's generator when it is chosen (<see cref="GeneratorChoice"/>). Read again once the game's own location rules are read
    /// (<see cref="RefreshRules"/>): the location file converts against them.
    /// </summary>
    internal ValheimGen.WorldRules Rules { get; private set; }
    /// <summary>The rules were given (tests): never read again.</summary>
    private readonly bool _rulesGiven;

    /// <summary>
    /// Reads Expand World's rules again when the location file could not convert before (the game's own location
    /// rules were not read yet when the page opened: GameLocations.UseGameRules reads them in the background) and
    /// they are read now. Any thread; the hint is updated on the window's thread.
    /// </summary>
    internal void RefreshRules()
    {
        if (_rulesGiven || !Rules.DataInstalled || Rules.Locations != null || !ValheimGen.SeedLocations.Available)
        {
            return;
        }
        var rules = WorldGenerators.ForNewWorld(AppSettings.Load());
        if (rules.Locations == null)
        {
            return;
        }
        Rules = WithChoice(rules);
        Avalonia.Threading.Dispatcher.UIThread.Post(ShowGeneration);
    }
    /// <summary>
    /// The game's world folders for Where: the installed game's own first (a Windows build run through Proton saves
    /// inside its Steam prefix, not under ~/.config), then the others that exist.
    /// </summary>
    internal static List<string> Roots() => Places.WorldRootsForNew(AppSettings.Load());
    /// <summary>
    /// Builds the page and starts looking at a random seed.
    /// </summary>
    /// <param name="roots">The world folders to offer (tests); null for the game's own. With none, the first the game
    /// would use.</param>
    /// <param name="previewSize">The seed's map, in cells on a side (tests: smaller is quicker).</param>
    /// <param name="rules">What the worlds follow (tests); null: Expand World's rules when its switch is on, else the game's,
    /// with the first world generation mod's generator in use that makes new worlds (WorldGenerators.ForNewWorld).</param>
    public NewWorldPage(IReadOnlyList<string>? roots = null, int previewSize = 512, ValheimGen.WorldRules? rules = null)
    {
        _previewSize = previewSize;
        _rulesGiven = rules != null;
        Rules = rules ?? WorldGenerators.ForNewWorld(AppSettings.Load());
        ShowGeneration();
        Loading = BuildLoadingSign();
        var where = (roots ?? Roots()).ToList();
        if (where.Count == 0)
        {
            where.Add(Places.GameWorldRoot(AppSettings.Load()));
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
