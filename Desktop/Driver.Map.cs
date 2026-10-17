
namespace TerrainEditor.Desktop;

/// <summary>
/// The commands for the world map: its view, its zones, its search and the hits of the search.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'mapview' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMapview(Call call)
    {
        MainWindow w = call.W;
        w.MapPage!.Map.LookAt(call.F(0), call.F(1), call.F(2));
        return Task.FromResult(State(w));
    }

    /// <summary>The 'mappick' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMappick(Call call)
    {
        MainWindow w = call.W;
        w.MapPage!.Pick((int)call.F(0), (int)call.F(1));
        return Task.FromResult(State(w));
    }

    /// <summary>The 'search' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdSearch(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        w.MapPage!.SearchBox.Text = a[1];
        await w.MapPage.Search();
        return State(w);
    }

    /// <summary>The 'hit' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdHit(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        w.MapPage!.Hits.SelectedIndex = int.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
        return Task.FromResult(State(w));
    }

    /// <summary>The 'zones' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdZones(Call call)
    {
        MainWindow w = call.W;
        w.MapPage!.NoBuildBox.IsChecked = w.MapPage.NoEditBox.IsChecked = w.MapPage.OnlyGenBox.IsChecked = false;
        await w.MapPage.ShowMatching();
        return State(w);
    }
}
