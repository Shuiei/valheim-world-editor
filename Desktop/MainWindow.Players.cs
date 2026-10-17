using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    // ---- Live players.

    /// <summary>Live: the players' names and positions from the game; the last ones stay when it does not answer.</summary>
    internal async Task PollPlayers()
    {
        if (View.Scene?.Owner?.Live is not { } live)
        {
            return;
        }
        try
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(await live.Players())?.AsArray();
            var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, new Vector3((float)(p["x"] ?? 0), (float)(p["y"] ?? 0), (float)(p["z"] ?? 0)))).ToList() ?? [];
            SetPlayers(players);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            // The game does not answer right now: the last positions stay (brief: it can fail every tick).
            Log.CaughtBrief(ex, "asking the game where the players are");
        }
    }
}
