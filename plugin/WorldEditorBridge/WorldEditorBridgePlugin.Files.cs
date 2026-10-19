using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;

namespace WorldEditorBridge;

/// <summary>
/// The game's files, read only, for an editor that follows the mods the game runs: GET /plugins lists the plugins
/// BepInEx loaded, GET /config?path= serves a file or lists a folder under BepInEx/config, and GET /world/meta serves
/// the world's meta file as it lies on disk (the starting global keys as written there: a mod may take its own out of
/// the loaded list). Expand World's settings and data files are the first use; nothing here is shaped for one mod.
/// </summary>
public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>A file under BepInEx/config larger than this is refused (413): settings are read, not archives.</summary>
    private const int MaxConfigFile = 4 << 20;

    /// <summary>World's path to its meta file for its file source (not public).</summary>
    private static readonly MethodInfo MetaPath = typeof(World).GetMethod("GetMetaPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

    /// <summary>GET /plugins: each plugin BepInEx loaded, its GUID, name and version, as JSON.</summary>
    private static byte[] Plugins()
    {
        StringBuilder sb = new("{\"plugins\":[");
        bool first = true;
        foreach (PluginInfo info in Chainloader.PluginInfos.Values)
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append($"{{\"guid\":{Json(info.Metadata.GUID)},\"name\":{Json(info.Metadata.Name)},\"version\":{Json(info.Metadata.Version.ToString())}}}");
        }
        return Encoding.UTF8.GetBytes(sb.Append("]}").ToString());
    }

    /// <summary>
    /// GET /config?path=&lt;relative&gt;: a file under BepInEx/config as its bytes (application/octet-stream), or a folder
    /// there as a JSON list of its entries (name, folder, size, modified); the folder itself for an empty path. Only
    /// what is inside the folder: a path that leads out of it (absolute, "..", a link) is refused with 400, one that
    /// does not exist gets 404, a file over 4 MB 413. Read on this thread: no game object is touched.
    /// </summary>
    private static void ServeConfig(HttpListenerContext ctx)
    {
        HttpListenerResponse res = ctx.Response;
        string root = Path.GetFullPath(Paths.ConfigPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = ConfigPathOf(root, ctx.Request.QueryString["path"] ?? "");
        if (full == null)
        {
            Reply(res, 400, "text/plain", Encoding.UTF8.GetBytes("the path must name a file or folder inside BepInEx/config"));
        }
        else if (Directory.Exists(full))
        {
            Reply(res, 200, "application/json", Encoding.UTF8.GetBytes(FolderJson(full)));
        }
        else if (File.Exists(full))
        {
            if (new FileInfo(full).Length > MaxConfigFile)
            {
                Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the file is larger than 4 MB"));
                return;
            }
            Reply(res, 200, "application/octet-stream", File.ReadAllBytes(full));
        }
        else
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("no such file or folder under BepInEx/config"));
        }
    }

    /// <summary>
    /// The full path of a file or folder asked for under the config folder; null when the path would lead out of it
    /// (an absolute path, a ".." part, a link anywhere along it).
    /// </summary>
    /// <param name="root">The config folder, full, without a trailing separator.</param>
    /// <param name="wanted">The path asked for, relative to it ("/" or "\" between parts).</param>
    private static string ConfigPathOf(string root, string wanted)
    {
        string relative = wanted.Replace('\\', '/').Trim('/');
        if (relative.Length == 0)
        {
            return root;
        }
        string[] parts = relative.Split('/');
        if (Path.IsPathRooted(relative) || Array.Exists(parts, p => p is "" or "." or ".."))
        {
            return null;
        }
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }
        // No link along the way: a link out of the folder would serve what is not under it.
        string walked = root;
        foreach (string part in parts)
        {
            walked = Path.Combine(walked, part);
            if (!File.Exists(walked) && !Directory.Exists(walked))
            {
                break;
            }
            if ((File.GetAttributes(walked) & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }
        }
        return full;
    }

    /// <summary>A folder's entries as JSON: each one's name, whether it is a folder, its size and when it was last written (UTC).</summary>
    private static string FolderJson(string folder)
    {
        StringBuilder sb = new("{\"folder\":true,\"entries\":[");
        bool first = true;
        foreach (FileSystemInfo entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            bool isFolder = (entry.Attributes & FileAttributes.Directory) != 0;
            long size = isFolder ? 0 : ((FileInfo)entry).Length;
            sb.Append($"{{\"name\":{Json(entry.Name)},\"folder\":{(isFolder ? "true" : "false")},\"size\":{size.ToString(CultureInfo.InvariantCulture)},\"modified\":{Json(entry.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture))}}}");
        }
        return sb.Append("]}").ToString();
    }

    /// <summary>
    /// GET /world/meta: the world's meta file (.fwl) as it lies on disk, read the way the game reads its saves (so from
    /// the cloud too), for the starting global keys as written there; null (404) when the game has no world. Main
    /// thread: the world is the game's.
    /// </summary>
    private static byte[] WorldMeta()
    {
        World w = ZNet.World;
        if (w == null)
        {
            return null;
        }
        string path = (string)MetaPath.Invoke(w, null);
        FileReader reader = new(path, w.m_fileSource, FileHelpers.FileHelperType.Binary);
        using (reader.m_binary)
        {
            using MemoryStream ms = new();
            reader.m_binary.BaseStream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    /// <summary>Answers with the main thread's bytes for <paramref name="work"/>, or 404 when it has none.</summary>
    private void ReplyBytesOr404(HttpListenerResponse res, Func<byte[]> work)
    {
        byte[] bytes = OnMainThread(work);
        if (bytes == null)
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("the game has no world file"));
            return;
        }
        Reply(res, 200, "application/octet-stream", bytes);
    }
}
