using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The saved server list: read leniently, and remembered servers named after their address.
/// </summary>
[Collection("World files")]
public class AppDataServersTests
{
    private static readonly string[] ConfigLines =
    [
        "Host = before any section", "; a comment", "# another",
        "[Main]", "Host = 203.0.113.1", "User = valheim", "SshPort = not a number", "BridgePort = x", "Password =", "KeyFile = ~/.ssh/k", "no equals sign here",
        "[No user]", "Host = 203.0.113.2",
        "[  Spaced  ]", "Host = 203.0.113.3", "User = u", "SshPort = 2222", "BridgePort = 5190", "Token = t", "HostKey = SHA256:x", "GameFolder = /srv/v", "Unknown = 1",
    ];

    private static readonly string[] Expected1 = ["Main", "Spaced"];
    [Fact]
    public void TheServerListIsReadLeniently()
    {
        string file = Path.Combine(Path.GetTempPath(), "vwe-servers-" + Guid.NewGuid().ToString("N")[..8] + ".cfg");
        string? old = ServerConfig.PathOverride;
        ServerConfig.PathOverride = file;
        try
        {
            Assert.Empty(ServerConfig.Load());
            File.WriteAllText(file, string.Join("\n", ConfigLines));
            var list = ServerConfig.Load();
            Assert.Equal(Expected1, list.Select(s => s.Name).ToArray());
            Assert.Equal((22, 5182, null, "~/.ssh/k"), (list[0].SshPort, list[0].BridgePort, list[0].Password, list[0].KeyFile));
            Assert.Equal((2222, 5190, "t", "SHA256:x", "/srv/v"), (list[1].SshPort, list[1].BridgePort, list[1].Token, list[1].HostKey, list[1].GameFolder));
            // A server remembered without a name is named after its address; ] in a name cannot end the section.
            ServerConfig.Remember(new ServerConfig.Server { Host = "198.51.100.7", User = "admin" });
            ServerConfig.Remember(new ServerConfig.Server { Name = "Odd] name", Host = "198.51.100.8", User = "admin" });
            var again = ServerConfig.Load();
            Assert.Equal("Odd) name", again[0].Name);
            Assert.Equal("198.51.100.7", again[1].Name);
            Assert.Equal(4, again.Count);
        }
        finally
        {
            ServerConfig.PathOverride = old;
            File.Delete(file);
        }
    }
}
