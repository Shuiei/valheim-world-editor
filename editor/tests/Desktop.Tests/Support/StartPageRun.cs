using Avalonia.Controls;
using Avalonia.LogicalTree;
using TerrainEditor.App;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The start page tests' setup: a page run with its own settings, saved servers in a temporary file, the worlds it was
/// asked to open and the tunnel it is given; and the text under a control.
/// </summary>
internal static class StartPageRun
{
    internal sealed class Run : IDisposable
    {
        public StartPage P { get; }
        public AppSettings Settings { get; } = new();
        public List<Tunnel.Request> Tunnels { get; } = [];
        public List<string> Opened { get; } = [];
        public Func<Task<WorldSession>>? Open;
        private readonly string? _servers = ServerConfig.PathOverride;
        public string Temp { get; } = Path.Combine(Path.GetTempPath(), "vwe-start-" + Guid.NewGuid().ToString("N")[..8]);

        public Run()
        {
            Directory.CreateDirectory(Temp);
            ServerConfig.PathOverride = Path.Combine(Temp, "servers.cfg");
            P = new StartPage(Settings);
            P.OpenRequested += (open, what) => { Open = open; Opened.Add(what); };
        }

        /// <summary>
        /// The tunnel opens on this port with this token (a stand-in game's), or fails.
        /// </summary>
        public void Tunnel(int port, string token, string? error = null) => P.StartTunnel = r =>
        {
            Tunnels.Add(r);
            return Task.FromResult(new Tunnel.Result(port, token, error, null,
                error == null ? new ServerConfig.Server { Name = r.Name ?? r.Host, Host = r.Host, SshPort = r.Port, User = r.User, Token = r.Token } : null));
        };

        /// <inheritdoc/>
        public void Dispose()
        {
            ServerConfig.PathOverride = _servers;
            if (Directory.Exists(Temp))
            {
                Directory.Delete(Temp, true);
            }
        }
    }

    /// <summary>
    /// Every text block's text under the control, joined with " | ", for asserting on what it shows.
    /// </summary>
    internal static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
}
