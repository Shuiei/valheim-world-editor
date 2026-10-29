using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Saved servers (ServerConfig): what is kept of each, the password kept only when asked and with its spaces, and remembering
/// the same server again.
/// </summary>
[Collection("DataDir")]
public class SavedServersTests : AppSettingsTestBase
{
    [Fact]
    public void SavedServersRoundTripWithoutThePasswordUnlessAsked()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "A", Host = "my.server.com", User = "valheim", Token = "t1", HostKey = "SHA256:abc", GameFolder = "/home/valheim/server", BridgePort = 5190 });
        ServerConfig.Remember(new ServerConfig.Server { Name = "B", Host = "203.0.113.10", SshPort = 2222, User = "admin", Password = "secret", Token = "t2" });
        var list = ServerConfig.Load();
        Assert.Equal(2, list.Count);
        Assert.Equal("B", list[0].Name);
        var a = list.Single(s => s.Name == "A");
        Assert.Null(a.Password);
        Assert.Equal("t1", a.Token);
        Assert.Equal("SHA256:abc", a.HostKey);
        Assert.Equal("/home/valheim/server", a.GameFolder);
        Assert.Equal(5190, a.BridgePort);
        Assert.Equal("secret", list.Single(s => s.Name == "B").Password);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ServerConfig.FilePath));
        }
        ServerConfig.Forget(a.Id);
        Assert.Single(ServerConfig.Load());
    }

    // A saved password keeps spaces at its ends (it was trimmed, and the login then failed).
    [Fact]
    public void ASavedPasswordKeepsItsSpaces()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "S", Host = "my.server.com", User = "valheim", Password = " pass word  " });
        Assert.Equal(" pass word  ", Assert.Single(ServerConfig.Load()).Password);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ServerConfig.FilePath)!, "*.tmp"));
    }

    [Fact]
    public void RememberingTheSameServerUpdatesIt()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "Old name", Host = "my.server.com", User = "valheim", Token = "t1" });
        ServerConfig.Remember(new ServerConfig.Server { Host = "my.server.com", User = "valheim", Token = "t2" });
        var s = Assert.Single(ServerConfig.Load());
        Assert.Equal("Old name", s.Name);
        Assert.Equal("t2", s.Token);
    }
}
