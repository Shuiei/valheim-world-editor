using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// What is waiting, as a list for people: the zones of ground as the caller says them, then the objects deleted,
    /// added and the zones reset; empty when nothing is.
    /// </summary>
    /// <param name="zones">The zones of ground changed, as text; empty when none.</param>
    /// <param name="deleted">Objects deleted.</param>
    /// <param name="added">Objects added.</param>
    /// <param name="resets">Zones marked for reset.</param>
    internal static string PendingList(string zones, int deleted, int added, int resets)
    {
        return string.Join(", ", new[]
        {
            zones,
            deleted > 0 ? Lang.T("save.count.deleted", deleted) : "",
            added > 0 ? Lang.T("save.count.added", added) : "",
            resets > 0 ? Lang.T("save.count.resets", resets) : "",
        }.Where(x => x != ""));
    }

    /// <summary>
    /// Whether the busy veil is up.
    /// </summary>
    internal bool IsBusy => _busy.IsVisible;

    /// <summary>
    /// The busy veil with what is being done, or none (null).
    /// </summary>
    private void Busy(string? text)
    {
        _busyText.Text = text ?? "";
        _busy.IsVisible = text != null;
    }

    /// <summary>
    /// A new world: the seed's preview and search, then Create (a world in the game's folder, opened). Null when that
    /// page is not shown.
    /// </summary>
    internal NewWorldPage? NewWorld { get; private set; }

    /// <summary>
    /// Shows a new New world page.
    /// </summary>
    internal void ShowNewWorld()
    {
        var page = NewWorld = new NewWorldPage();
        page.BackRequested += () => { page.Dispose(); NewWorld = null; ShowStart(); };
        page.CreateRequested += async (folder, name, seed) => await CreateWorld(folder, name, seed, page.Rules);
        Title = Lang.T("world.title.newWorld", BuildInfo.Version);
        _pages.Content = page.View;
    }

    /// <summary>
    /// Makes a world in <paramref name="folder"/> and opens it; on failure the New world page says why.
    /// </summary>
    /// <param name="folder">The world's folder.</param>
    /// <param name="name">The world's name.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="rules">What the world follows (Expand World's rules, or the game's); null: as the New world page would.</param>
    /// <returns>Null when made, else why it could not be, in English (what Claude is told).</returns>
    internal async Task<string?> CreateWorld(string folder, string name, string seed, ValheimGen.WorldRules? rules = null)
    {
        Busy(Lang.T("newWorld.msg.making", name));
        try
        {
            var with = rules ?? ExpandWorld.ForNewWorld(AppSettings.Load());
            await Task.Run(() => TerrainEditor.Save.WorldCreator.Create(folder, name, seed, rules: with));
            Console.WriteLine($"New world: made {name} (seed {seed}) in {folder}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Log.Caught(ex, "making a new world");
            Busy(null);
            var failed = Said.Of("newWorld.error.make", ex.Message);
            if (NewWorld is { } p)
            {
                p.Problem.Text = failed.Shown;
            }
            return failed.English;
        }
        Busy(null);
        NewWorld?.Dispose();
        NewWorld = null;
        await OpenWorld(() => Task.Run(() => WorldSession.Open(folder)), Lang.T("newWorld.msg.opening", name));
        var made = Said.Of("newWorld.msg.made", name, seed);
        // Opened: on its map, where the status bar is not shown.
        if (World != null)
        {
            Say(made);
        }
        else
        {
            Msg(made);
        }
        return null;
    }
}
