using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>Connects to the server the form describes (address and token required).</summary>
    internal async Task ConnectNew()
    {
        if (string.IsNullOrWhiteSpace(SHost.Text))
        {
            ServerError.Text = Lang.T("start.error.host");
            return;
        }
        if (string.IsNullOrWhiteSpace(SToken.Text))
        {
            ServerError.Text = Lang.T("start.error.token");
            return;
        }
        await Connect(new Tunnel.Request(SHost.Text.Trim(), (int)(SPort.Value ?? 22), SUser.Text?.Trim() ?? "", string.IsNullOrWhiteSpace(SPass.Text) ? null : SPass.Text, NullIfEmpty(SKey.Text), NullIfEmpty(SPhrase.Text), SToken.Text.Trim(),
            SBridge.Value is decimal b ? (int)b : null, SSave.IsChecked == true, NullIfEmpty(SName.Text), NullIfEmpty(SFolder.Text)), ServerError, Lang.T("start.msg.connectingTo", SHost.Text.Trim()));
    }

    /// <summary>
    /// Opens the SSH tunnel, checks the plugin answers through it, remembers the server, then opens. Progress and
    /// problems go to <paramref name="error"/>; a plugin that does not answer closes the tunnel again.
    /// </summary>
    /// <param name="t">The server and how to log in.</param>
    /// <param name="error">The line for progress and errors.</param>
    /// <param name="what">What to say while connecting.</param>
    private async Task Connect(Tunnel.Request t, TextBlock error, string what)
    {
        if (_connecting)
        {
            return;
        }
        _connecting = true;
        try
        {
            await ConnectOnce(t, error, what);
        }
        finally
        {
            _connecting = false;
        }
    }

    /// <summary>Trimmed text, or null when blank.</summary>
    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
