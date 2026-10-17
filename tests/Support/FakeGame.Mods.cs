using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's answers to the mods requests: the registered mods (/mods) and their routes (/mods/id/name).
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>
    /// The registered mods; a plugin without the list (Mods null) answers 404.
    /// </summary>
    private void ServeMods(Reply reply)
    {
        if (Mods == null)
        {
            reply.Status = 404;
            reply.Type = "text/plain";
            reply.Body = Encoding.UTF8.GetBytes("unknown path");
        }
        else
        {
            reply.Body = Encoding.UTF8.GetBytes("{\"api\":1,\"mods\":[" + string.Join(",", Mods.Select(m => $"\"{m}\"")) + "]}");
        }
    }

    /// <summary>
    /// A mod's route: its answer to the request body, or a 404 for an unknown mod or route.
    /// </summary>
    private async Task ServeModRoute(HttpListenerContext ctx, string path, Reply reply)
    {
        string[] parts = path.Split('/');
        using var ms = new MemoryStream();
        await ctx.Request.InputStream.CopyToAsync(ms);
        reply.Type = "text/plain";
        if (Mods == null || parts.Length != 4 || !Mods.Contains(parts[2]))
        {
            reply.Status = 404;
            reply.Body = Encoding.UTF8.GetBytes("no mod of that id is registered with the bridge (GET /mods lists them)");
        }
        else if (!Routes.TryGetValue((parts[2], parts[3]), out var route))
        {
            reply.Status = 404;
            reply.Body = Encoding.UTF8.GetBytes("the mod has no route of that name");
        }
        else
        {
            reply.Type = "application/octet-stream";
            reply.Body = route(ms.ToArray());
        }
    }
}
