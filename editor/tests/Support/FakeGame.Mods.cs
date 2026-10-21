using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's answers to the mods requests: the registered mods (/mods) and their routes (/mods/id/name).
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>The plugin's report of the game's prefabs (GET /catalogue); null: a plugin too old to have it (404).</summary>
    public string? Catalogue { get; set; }

    /// <summary>The answer to POST /models (what the game draws of the prefabs asked for): a JSON object, empty by default.</summary>
    public string Models { get; set; } = "{}";

    /// <summary>
    /// Answers the mods' paths: the registered mods and their routes, the catalogue of prefabs and the models asked for.
    /// </summary>
    private async Task RouteMods(HttpListenerContext ctx, string path, Reply reply)
    {
        switch (path)
        {
            case "/mods":
                ServeMods(reply);
                break;
            case "/catalogue":
                ServeCatalogue(reply);
                break;
            case "/models":
                await ServeModels(ctx, reply);
                break;
            default:
                await ServeModRoute(ctx, path, reply);
                break;
        }
    }

    /// <summary>
    /// The catalogue report; a plugin without one (Catalogue null) answers 404.
    /// </summary>
    private void ServeCatalogue(Reply reply)
    {
        if (Catalogue == null)
        {
            reply.Status = 404;
            reply.Type = "text/plain";
            reply.Body = Encoding.UTF8.GetBytes("unknown path");
        }
        else
        {
            reply.Body = Encoding.UTF8.GetBytes(Catalogue);
        }
    }

    /// <summary>The models of the prefabs asked for (the names in the body are read and left aside).</summary>
    private async Task ServeModels(HttpListenerContext ctx, Reply reply)
    {
        using var ms = new MemoryStream();
        await ctx.Request.InputStream.CopyToAsync(ms);
        reply.Body = Encoding.UTF8.GetBytes(Models);
    }

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
