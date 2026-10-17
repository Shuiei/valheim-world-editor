using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The text of servers.cfg (ServerConfigFile): sections read as servers, values read as written, and what is written
/// reads back the same.
/// </summary>
public class ServerConfigFileTests
{
    [Fact]
    public void EachSectionIsOneServer()
    {
        var list = ServerConfigFile.Parse(["[Home]", "Host = h", "User = u", "SshPort = 2222"]);
        var s = Assert.Single(list);
        Assert.Equal(("Home", "h", "u", 2222), (s.Name, s.Host, s.User, s.SshPort));
    }

    [Fact]
    public void KeysBeforeAnySectionAreIgnored()
    {
        var list = ServerConfigFile.Parse(["Host = h", "[S]", "User = u"]);
        Assert.Equal("", Assert.Single(list).Host);
    }

    [Fact]
    public void APasswordKeepsItsSpacesAfterTheSeparator()
    {
        var list = ServerConfigFile.Parse(["[S]", "Password =  pw "]);
        Assert.Equal(" pw ", Assert.Single(list).Password);
    }

    [Fact]
    public void AnInvalidSshPortReadsAsTheDefault()
    {
        var list = ServerConfigFile.Parse(["[S]", "SshPort = 70000"]);
        Assert.Equal(22, Assert.Single(list).SshPort);
    }

    [Fact]
    public void FormatWritesTheSectionAndItsKeys()
    {
        string text = ServerConfigFile.Format([new ServerConfig.Server { Name = "Home", Host = "h", User = "u" }]);
        Assert.Contains("[Home]", text);
        Assert.Contains("Host = h", text);
    }

    [Fact]
    public void WhatIsWrittenReadsBackTheSame()
    {
        var server = new ServerConfig.Server { Name = "Home", Host = "h", User = "u", SshPort = 2222, BridgePort = 6000, Token = "t", HostKey = "SHA256:abc" };
        ServerConfig.Server back = Assert.Single(ServerConfigFile.Parse(ServerConfigFile.Format([server]).Split('\n')));
        Assert.Equal(("Home", "h", "u", 2222, 6000, "t", "SHA256:abc"), (back.Name, back.Host, back.User, back.SshPort, back.BridgePort, back.Token, back.HostKey));
    }

    [Fact]
    public void AValueWithALineBreakIsNotWritten()
    {
        Assert.Throws<ArgumentException>(() => ServerConfigFile.Format([new ServerConfig.Server { Name = "S", Host = "a\nb", User = "u" }]));
    }
}
