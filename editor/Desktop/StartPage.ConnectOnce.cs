using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <inheritdoc cref="Connect"/>
    private async Task ConnectOnce(Tunnel.Request t, TextBlock error, string what)
    {
        error.Text = what;
        var tr = await StartTunnel(t);
        if (tr.Error != null)
        {
            error.Text = tr.Error;
            return;
        }
        string url = $"http://127.0.0.1:{tr.LocalPort}";
        string? down = await LiveBridge.Check(url, tr.Token, TimeSpan.FromSeconds(10));
        if (down != null)
        {
            string? why = down.Contains("refused the token") ? null : await Tunnel.Diagnose(t.GameFolder);
            Tunnel.Close();
            error.Text = down.Contains("refused the token") ? down : why ?? Lang.T("start.error.noAnswer");
            return;
        }
        if (tr.Server != null)
        {
            ServerConfig.Remember(tr.Server);
        }
        error.Text = "";
        OpenLive(new LiveBridge(url, tr.Token), "server " + t.Host, "server", Lang.T("start.msg.loadingServer"));
    }
}
