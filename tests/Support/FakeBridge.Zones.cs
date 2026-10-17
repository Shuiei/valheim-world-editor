using System.Net;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in plugin's answer to the zone reset call: the zones reset are recorded.
/// </summary>
public sealed partial class FakeBridge
{
    /// <summary>
    /// Records the zone resets a request lists and returns the answer for them.
    /// </summary>
    private string ServeZoneReset(HttpListenerContext ctx)
    {
        using var r = new BinaryReader(ctx.Request.InputStream);
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            Resets.Add((r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()));
        }
        return $"{{\"zones\":{n},\"destroyed\":0,\"locations\":0}}";
    }
}
