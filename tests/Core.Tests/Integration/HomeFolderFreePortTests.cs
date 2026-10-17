using System.Net;
using System.Net.Sockets;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Free ports: the first free one from a start, or any when fifty in a row are taken.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderFreePortTests : HomeFolderTestBase
{
    [Fact]
    public void AFreePortIsTheFirstFreeOneOrAnyWhenFiftyAreTaken()
    {
        int from = FakeGame.FreePort();
        var taken = new TcpListener(IPAddress.Loopback, from);
        taken.Start();
        try
        {
            Assert.NotEqual(from, Places.FreePort(from));
        }
        finally
        {
            taken.Stop();
        }
        // Fifty ports in a row taken: any free port.
        var held = new List<TcpListener>();
        int start = 0;
        for (int attempt = 0; attempt < 20 && held.Count < 50; attempt++)
        {
            foreach (var l in held)
            {
                l.Stop();
            }
            held.Clear();
            start = 20000 + Random.Shared.Next(0, 30000);
            for (int p = start; p < start + 50; p++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, p);
                    l.Start();
                    held.Add(l);
                }
                catch (SocketException)
                {
                    break;
                }
            }
        }
        try
        {
            Assert.Equal(50, held.Count);
            int port = Places.FreePort(start);
            Assert.True(port < start || port >= start + 50, $"{port}");
        }
        finally
        {
            foreach (var l in held)
            {
                l.Stop();
            }
        }
    }
}
