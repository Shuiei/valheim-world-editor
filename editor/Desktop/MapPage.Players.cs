using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The live players: asking the game where they are, and their names over the map.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// Live: asks the game for the players online and puts them on the map. One question at a time per world (a game
    /// slow to answer gets no pile of them), and an answer for a world no longer shown is dropped.
    /// </summary>
    internal async Task PollPlayers()
    {
        if (_session is not { Live: { } live } s || !_polling.Add(s))
        {
            return;
        }
        try
        {
            string answer = await live.Players();
            if (_session != s)
            {
                return;
            }
            var json = System.Text.Json.Nodes.JsonNode.Parse(answer)?.AsArray();
            var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, (float)(p["x"] ?? 0), (float)(p["z"] ?? 0))).ToList() ?? [];
            LiveText.Text = players.Count > 0
                ? Lang.T("map.live.playersNamed", players.Count, string.Join(", ", players.Select(p => p.Item1)))
                : Lang.T("map.live.players", players.Count);
            Map.Players = players;
            PlaceLabels();
            Map.RequestNextFrameRendering();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            // Brief: it can fail at every poll while the game does not answer.
            Log.CaughtBrief(ex, "asking the game where the players are");
            if (_session == s)
            {
                LiveText.Text = Lang.T("map.live.noAnswer");
            }
        }
        finally
        {
            _polling.Remove(s);
        }
    }

    /// <summary>
    /// The worlds whose players are being asked for now (a question waits for each one's answer): a world shown again
    /// before its game answered is not asked a second time.
    /// </summary>
    private readonly HashSet<WorldSession> _polling = [];

    /// <summary>
    /// The players' names next to their dots.
    /// </summary>
    private void PlaceLabels()
    {
        _labels.Children.Clear();
        foreach (var (name, x, z) in Map.Players)
        {
            var at = Map.ScreenOf(x, z);
            var t = new TextBlock { Text = name, FontSize = 12, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, Effect = new DropShadowEffect { BlurRadius = 3, OffsetX = 0, OffsetY = 0, Color = Colors.Black, Opacity = 1 } };
            Canvas.SetLeft(t, at.X + 10);
            Canvas.SetTop(t, at.Y - 8);
            _labels.Children.Add(t);
        }
    }
}
