using System.Net;
using System.Net.Sockets;

namespace WorldEditor.Tests;

/// <summary>
/// A local TCP port for a test's stand-in server, found by letting the system pick one.
/// </summary>
public static class LoopbackPort
{
    /// <summary>
    /// A local TCP port free right now, on the address a test's server will listen on (127.0.0.1 unless given: a port
    /// free there can be taken on another of this computer's addresses).
    /// </summary>
    /// <param name="address">The address the server will listen on (null: 127.0.0.1).</param>
    public static int Free(IPAddress? address = null)
    {
        var l = new TcpListener(address ?? IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
