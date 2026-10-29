using System.Net;
using System.Net.Sockets;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The free port the stand-in servers listen on: a port the system reports as free can be listened on at once.
/// </summary>
public sealed class LoopbackPortTests
{
    [Fact]
    public void TheFreePortCanBeListenedOnAtOnce()
    {
        int port = LoopbackPort.Free();

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        listener.Stop();

        Assert.InRange(port, 1, 65535);
    }
}
