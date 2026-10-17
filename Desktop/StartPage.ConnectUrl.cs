using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>I made my own tunnel: connects to the bridge address typed (http:// added when missing).</summary>
    internal async Task ConnectUrl()
    {
        if (_connecting)
        {
            return;
        }
        _connecting = true;
        try
        {
            await ConnectUrlOnce();
        }
        finally
        {
            _connecting = false;
        }
    }

    /// <inheritdoc cref="ConnectUrl"/>
    private async Task ConnectUrlOnce()
    {
        string url = LiveUrl.Text?.Trim() ?? "";
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }
        string token = LiveToken.Text?.Trim() ?? "";
        if (await LiveBridge.Check(url, token, TimeSpan.FromSeconds(10)) is string unreachable)
        {
            UrlError.Text = unreachable;
            return;
        }
        _settings.LiveUrl = url;
        OpenLive(new LiveBridge(url, token), "live " + url, "server", Lang.T("save.msg.loadingLive"));
    }
}
