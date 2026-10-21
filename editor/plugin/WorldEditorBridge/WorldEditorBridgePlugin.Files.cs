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
        string[] parts = ConfigPartsOf(ctx.Request.QueryString["path"] ?? "");
        string full = parts == null ? null : parts.Length == 0 ? root : Path.GetFullPath(Path.Combine(root, string.Join(Path.DirectorySeparatorChar.ToString(), parts)));
        if (full == null || (full != root && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            Reply(res, 400, "text/plain", Encoding.UTF8.GetBytes("the path must name a file or folder inside BepInEx/config"));
        }
        else if (Directory.Exists(full))
        {
            if (Linked(root, parts))
            {
                Reply(res, 400, "text/plain", Encoding.UTF8.GetBytes("the path must name a file or folder inside BepInEx/config"));
                return;
            }
            Reply(res, 200, "application/json", Encoding.UTF8.GetBytes(FolderJson(full)));
        }
        else if (File.Exists(full))
        {
            ServeFile(res, root, parts, full);
        }
        else
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("no such file or folder under BepInEx/config"));
        }
    }

    /// <summary>
    /// A file under the config folder: opened first, then checked for a link along its path, so that what is read is
    /// what was checked (a link put there after the check cannot change the file already open); refused when linked
    /// (400) or over 4 MB (413).
    /// </summary>
    private static void ServeFile(HttpListenerResponse res, string root, string[] parts, string full)
    {
        using FileStream file = new(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Linked(root, parts))
        {
            Reply(res, 400, "text/plain", Encoding.UTF8.GetBytes("the path must name a file or folder inside BepInEx/config"));
            return;
        }
        if (file.Length > MaxConfigFile)
        {
            Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the file is larger than 4 MB"));
            return;
        }
        using MemoryStream ms = new();
        file.CopyTo(ms);
        Reply(res, 200, "application/octet-stream", ms.ToArray());
    }

    /// <summary>
    /// The parts of a path asked for under the config folder (empty for the folder itself); null when it is absolute
    /// (a drive, a leading separator) or has an empty, "." or ".." part.
    /// </summary>
    /// <param name="wanted">The path asked for, relative to the config folder ("/" or "\" between parts).</param>
    private static string[] ConfigPartsOf(string wanted)
    {
        if (wanted.Length == 0)
        {
            return [];
        }
        if (wanted[0] is '/' or '\\' || Path.IsPathRooted(wanted))
        {
            return null;
        }
        string[] parts = wanted.Replace('\\', '/').TrimEnd('/').Split('/');
        return Array.Exists(parts, p => p is "" or "." or "..") ? null : parts;
    }

    /// <summary>
    /// Whether a link stands anywhere along a path under the config folder (a link out of the folder would serve what
    /// is not under it). A part that does not exist ends the walk: nothing is linked past it.
    /// </summary>
    private static bool Linked(string root, string[] parts)
    {
        string walked = root;
        foreach (string part in parts)
        {
            walked = Path.Combine(walked, part);
            if (!File.Exists(walked) && !Directory.Exists(walked))
            {
                return false;
            }
            if ((File.GetAttributes(walked) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }
        return false;
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
    /// GET /world/meta: the world's meta file as it lies on disk, read the way the game reads its saves (so from the
    /// cloud too), for the starting global keys as written there; null (404) when the game has no world, or no file
    /// of it. The file is the one the game saves (<c>worlds/name/_main.n.fwl2</c>, which a world made or saved by this
    /// game has whether it was loaded chunked or not), else the one the game loaded (<c>worlds/name.fwl</c>, a world
    /// saved by an older game). Main thread: the world is the game's.
    /// </summary>
    private static byte[] WorldMeta()
    {
        World w = ZNet.World;
        if (w == null)
        {
            return null;
        }
        string path = w.GetSaveFWLPath();
        if (!FileHelpers.Exists(path, w.m_fileSource))
        {
            // A game without the older method, or without the older file: nothing to serve.
            path = MetaPath == null ? null : (string)MetaPath.Invoke(w, null);
            if (path == null || !FileHelpers.Exists(path, w.m_fileSource))
            {
                return null;
            }
        }
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
