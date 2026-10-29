using System.Collections.Specialized;
using System.Net;
using WorldEditorBridge;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The bridge plugin's access rules (BridgeAccess, the plugin's own file compiled here): who may connect, the token
/// compared in constant time and read from the header only, callers from the internet refused before their token is
/// looked at, and the log's warning when BindAddress opens the plugin beyond this machine.
/// </summary>
public sealed class BridgeAccessTests
{
    // This machine, its local networks and a VPN's shared range; IPv4 as a dual-mode socket gives it too. Never the
    // internet's, nor an unknown caller.
#pragma warning disable S1313 // Made-up addresses of each kind (loopback, private, link-local, VPN, mapped, IPv6, public): the test's cases, not hosts.
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.127.255.255", true)]
    [InlineData("fd00::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("::ffff:192.168.1.20", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.128.0.1", false)]
    [InlineData("::ffff:8.8.8.8", false)]
    [InlineData("2001:db8::1", false)]
    public void OnlyThisMachineAndItsLocalNetworksMayConnect(string caller, bool may) =>
        Assert.Equal(may, BridgeAccess.MayConnect(IPAddress.Parse(caller)));

    [Fact]
    public void AnUnknownCallerMayNot() => Assert.False(BridgeAccess.MayConnect(null));

    // From the internet: 403 and the way in (an SSH tunnel), before the token is looked at, so even the right one does
    // not get it through.
    [Fact]
    public void ACallerFromTheInternetIsRefusedWithTheWayIn()
    {
        var headers = new NameValueCollection { [BridgeAccess.TokenHeader] = "secret" };
        var refused = BridgeAccess.Refusal(IPAddress.Parse("203.0.113.5"), headers, "secret");
        Assert.NotNull(refused);
        Assert.Equal(403, refused.Value.Status);
        Assert.Contains("SSH tunnel", refused.Value.Text);
        Assert.Null(BridgeAccess.Refusal(IPAddress.Parse("192.168.1.30"), headers, "secret"));
    }
#pragma warning restore S1313

    // The token compared whole: the same one only, not one that differs, starts the same or is longer; none and an empty
    // one never match, nor anything when the plugin has no token.
    [Theory]
    [InlineData("secret", "secret", true)]
    [InlineData("secreT", "secret", false)]
    [InlineData("secre", "secret", false)]
    [InlineData("secrets", "secret", false)]
    [InlineData("", "secret", false)]
    [InlineData(null, "secret", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public void OnlyTheSameTokenMatches(string? given, string? expected, bool same) =>
        Assert.Equal(same, BridgeAccess.SameToken(given, expected));

    // A token in the URL (?token=) is not taken, only the header's: a request answered as the plugin answers it, through
    // a real listener on this machine.
    [Fact]
    public async Task ATokenInTheUrlIsNotTaken()
    {
        int port = LoopbackPort.Free();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        var ct = TestContext.Current.CancellationToken;
        var answering = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)
            {
                var ctx = await listener.GetContextAsync();
                var refused = BridgeAccess.Refusal(ctx.Request.RemoteEndPoint?.Address, ctx.Request.Headers, "secret");
                ctx.Response.StatusCode = refused?.Status ?? 200;
                ctx.Response.Close();
            }
        }, ct);
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync($"http://localhost:{port}/world/meta?token=secret", ct)).StatusCode);
        using var withHeader = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}/world/meta");
        withHeader.Headers.Add(BridgeAccess.TokenHeader, "secret");
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(withHeader, ct)).StatusCode);
        await answering.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    // The log warns when BindAddress lets other machines in (the token then unencrypted), not on this machine only.
#pragma warning disable S1313 // BindAddress values of each kind (loopback, every address, a network's): the test's cases, not hosts.
    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("localhost", false)]
    [InlineData("::1", false)]
    [InlineData("[::1]", false)]
    [InlineData("127.0.0.2", false)]
    [InlineData("0.0.0.0", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("*", true)]
    [InlineData("+", true)]
    public void TheLogWarnsWhenOtherMachinesCanConnect(string bind, bool warns)
    {
        string? warning = BridgeAccess.BindWarning(bind);
        Assert.Equal(warns, warning != null);
        if (warning != null)
        {
            Assert.Contains("unencrypted", warning);
            Assert.Contains(bind, warning);
        }
    }
#pragma warning restore S1313
}
