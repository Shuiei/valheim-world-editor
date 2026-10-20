using System.Globalization;
using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game's files, as the plugin serves them from 1.54.0: the plugins it loaded (GET /plugins), the files
/// and folders under its BepInEx/config (GET /config?path=) and its world file (GET /world/meta). None set: an older
/// plugin, which answers 404 to all three.
/// </summary>
public sealed partial class FakeGame
{
    /// <summary>The plugins the game loaded (GET /plugins); null: a plugin too old to list them (404).</summary>
    public List<(string Guid, string Name, string Version)>? Plugins { get; set; }

    /// <summary>
    /// The files under the game's BepInEx/config, by their path there ("/" between parts): a folder is every path's
    /// prefix. Served only when <see cref="Plugins"/> is set.
    /// </summary>
    public Dictionary<string, byte[]> ConfigFiles { get; } = [];

    /// <summary>Files under BepInEx/config the bridge refuses (413, as for one over its size limit).</summary>
    public HashSet<string> RefusedConfigFiles { get; } = [];

    /// <summary>The world's meta file on the game's disk (GET /world/meta); null: none, or an older plugin (404).</summary>
    public byte[]? WorldMeta { get; set; }

    /// <summary>How many files and folders were asked for (GET /config).</summary>
    public int ConfigReads => _configReads;
    private int _configReads;

    /// <summary>A file under BepInEx/config, as text.</summary>
    public void AddConfig(string path, string text) => ConfigFiles[path] = Encoding.UTF8.GetBytes(text);

    /// <summary>Answers the files' paths; an older plugin (no plugins listed) knows none of them.</summary>
    private void RouteFiles(HttpListenerContext ctx, string path, Reply reply)
    {
        if (Plugins == null)
        {
            NotFound(reply, "unknown path");
            return;
        }
        switch (path)
        {
            case "/plugins":
                reply.Body = Encoding.UTF8.GetBytes("{\"plugins\":[" + string.Join(",", Plugins.Select(p => $"{{\"guid\":\"{p.Guid}\",\"name\":\"{p.Name}\",\"version\":\"{p.Version}\"}}")) + "]}");
                break;
            case "/config":
                ServeConfig(ctx.Request.QueryString["path"] ?? "", reply);
                break;
            default:
                ServeWorldMeta(reply);
                break;
        }
    }

    /// <summary>A file's bytes, a folder's entries as the plugin lists them, or 404; a path out of the folder is refused (400).</summary>
    private void ServeConfig(string wanted, Reply reply)
    {
        Interlocked.Increment(ref _configReads);
        string path = wanted.Replace('\\', '/').Trim('/');
        if (path.Split('/').Any(p => p is "." or ".."))
        {
            reply.Status = 400;
            reply.Type = "text/plain";
            reply.Body = Encoding.UTF8.GetBytes("the path must name a file or folder inside BepInEx/config");
        }
        else if (RefusedConfigFiles.Contains(path))
        {
            reply.Status = 413;
            reply.Type = "text/plain";
            reply.Body = Encoding.UTF8.GetBytes("the file is larger than the bridge serves");
        }
        else if (ConfigFiles.TryGetValue(path, out byte[]? bytes))
        {
            reply.Type = "application/octet-stream";
            reply.Body = bytes;
        }
        else if (path == "" || ConfigFiles.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal)))
        {
            string prefix = path == "" ? "" : path + "/";
            var names = ConfigFiles.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k[prefix.Length..]).ToList();
            var entries = names.Select(n => n.Split('/')[0]).Distinct(StringComparer.Ordinal).Select(n =>
            {
                bool folder = !ConfigFiles.ContainsKey(prefix + n);
                long size = folder ? 0 : ConfigFiles[prefix + n].Length;
                return $"{{\"name\":\"{n}\",\"folder\":{(folder ? "true" : "false")},\"size\":{size.ToString(CultureInfo.InvariantCulture)},\"modified\":\"2026-10-18T10:00:00.0000000Z\"}}";
            });
            reply.Body = Encoding.UTF8.GetBytes("{\"folder\":true,\"entries\":[" + string.Join(",", entries) + "]}");
        }
        else
        {
            NotFound(reply, "no such file or folder under BepInEx/config");
        }
    }

    /// <summary>The world file's bytes, or 404 without one.</summary>
    private void ServeWorldMeta(Reply reply)
    {
        if (WorldMeta == null)
        {
            NotFound(reply, "the game has no world file");
            return;
        }
        reply.Type = "application/octet-stream";
        reply.Body = WorldMeta;
    }

    /// <summary>A 404 with a plain text reason, as the plugin answers one.</summary>
    private static void NotFound(Reply reply, string why)
    {
        reply.Status = 404;
        reply.Type = "text/plain";
        reply.Body = Encoding.UTF8.GetBytes(why);
    }
}
