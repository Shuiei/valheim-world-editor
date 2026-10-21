using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// Home's tiles: saved worlds and blueprints opened or saved last (newest first), then the servers
    /// that connect in one click (token and password or key saved).
    /// </summary>
    internal async Task FillHome()
    {
        int look = ++_homeLook;
        var blueprints = await Task.Run(() => Pictured(Blueprints().Take(4)));
        if (look != _homeLook)
        {
            Dispose(blueprints);
            return;
        }
        Keep(_homePictures, blueprints);
        RecentTiles.Children.Clear();
        var dated = new List<(DateTime When, Button Tile)>();
        foreach (var r in _settings.Recent.Where(r => Directory.Exists(r.Path)).Take(8))
        {
            string path = r.Path;
            dated.Add((r.Opened, Tile(null, "folder", Color.FromRgb(42, 58, 44), r.Name, Lang.T("start.tile.savedWorld", Ago(r.Opened)), Ui.Tilde(path), () => OpenFolder(path))));
        }
        foreach (var (b, picture) in blueprints)
        {
            string path = b.Path;
            dated.Add((b.Saved, Tile(picture, "shape", Color.FromRgb(58, 48, 36), b.Name, Lang.T("start.tile.blueprint", b.Pieces), Lang.T("start.tip.openWorkshop"), () => WorkshopRequested?.Invoke(path))));
        }
        foreach (var (_, tile) in dated.OrderByDescending(d => d.When).Take(8))
        {
            RecentTiles.Children.Add(tile);
        }
        foreach (var s in ServerConfig.Load().Where(s => s.Token != null && (s.Password != null || s.KeyFile != null)))
        {
            var server = s;
            RecentTiles.Children.Add(Tile(null, "server", Color.FromRgb(40, 51, 70), s.Name, Lang.T("start.tile.server"), $"{s.User}@{s.Host}", async () =>
                await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, server.Password, server.KeyFile, null, server.Token, null,
                    server.Password != null, server.Name, server.GameFolder), _homeError, Lang.T("start.msg.connectingTo", server.Name))));
        }
        if (RecentTiles.Children.Count == 0)
        {
            RecentTiles.Children.Add(Hint(Lang.T("start.hint.nothingYet")));
        }
    }

    /// <summary>The Workshop's tiles: every blueprint, newest first.</summary>
    internal async Task FillBlueprints()
    {
        int look = ++_workshopLook;
        var blueprints = await Task.Run(() => Pictured(Blueprints()));
        if (look != _workshopLook)
        {
            Dispose(blueprints);
            return;
        }
        Keep(_workshopPictures, blueprints);
        BlueprintTiles.Children.Clear();
        foreach (var (b, picture) in blueprints)
        {
            string path = b.Path;
            BlueprintTiles.Children.Add(Tile(picture, "shape", Color.FromRgb(58, 48, 36), b.Name, Lang.T("start.tile.pieces", b.Pieces, Ago(b.Saved)), Lang.T("start.tip.openWorkshop"), () => WorkshopRequested?.Invoke(path)));
        }
        if (BlueprintTiles.Children.Count == 0)
        {
            BlueprintTiles.Children.Add(Hint(Lang.T("start.hint.noBlueprints")));
        }
    }

    /// <summary>The blueprints in Homestead's folder, newest first; empty when the folder cannot be read.</summary>
    private static List<Homestead.Entry> Blueprints()
    {
        try
        {
            return [.. Homestead.List(Homestead.Folder()).OrderByDescending(b => b.Saved)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "listing the Library's blueprints");
            return [];
        }
    }

    /// <summary>Whether Homestead is in the game: the editor's blueprints are Homestead's, built in game with it.</summary>
    internal void ShowHomestead(Homestead.Status hs)
    {
        if (hs.Installed)
        {
            HomesteadText.Text = Lang.T("start.homestead.installed", hs.Version, string.Join(", ", hs.Where.Distinct()));
            HomesteadText.Foreground = Muted;
        }
        else
        {
            HomesteadText.Text = Lang.T("start.homestead.missing");
            HomesteadText.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
        }
        GetHomesteadButton.IsVisible = !hs.Installed;
        WorkshopHomestead.Text = HomesteadText.Text;
        WorkshopHomestead.Foreground = HomesteadText.Foreground;
    }
}
