using System.Net;
using System.Net.Sockets;

namespace WorldEditor.Tests;

/// <summary>
/// A local TCP port for a test's stand-in server, found by letting the system pick one.
/// </summary>
public static class LoopbackPort
{
    /// <summary>
    /// A local TCP port free right now.
    /// </summary>
    public static int Free()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
