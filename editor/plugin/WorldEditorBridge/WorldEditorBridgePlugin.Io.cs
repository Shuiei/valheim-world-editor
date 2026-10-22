using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Logging;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Writes an error that a catch handled to BepInEx's log (a warning unless <paramref name="level"/> says otherwise):
    /// where it was caught (the source file, line and member, which the compiler fills in), what was being done, and the
    /// exception's type and message, with its stack trace unless <paramref name="brief"/>. The server's home folder is
    /// written ~. Every catch that handles an error calls this; one that rethrows leaves it to the one that handles it.
    /// </summary>
    /// <param name="e">What was caught.</param>
    /// <param name="doing">What was being done, to follow "while": "answering a request".</param>
    /// <param name="brief">One line without the stack trace: for expected failures (a client that went away).</param>
    /// <param name="level">The log level.</param>
    /// <param name="file">The source file of the catch (the compiler's).</param>
    /// <param name="line">The line of the catch (the compiler's).</param>
    /// <param name="member">The member the catch is in (the compiler's).</param>
    private void Caught(Exception e, string doing, bool brief = false, LogLevel level = LogLevel.Warning, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
    {
        string where = file.Substring(file.LastIndexOfAny(['/', '\\']) + 1);
        var text = new StringBuilder($"WorldEditorBridge: caught at {where}:{line} ({member}) while {doing}: {e.GetType().FullName}: {e.Message.Replace("\r", " ").Replace("\n", " ")}");
        if (!brief)
        {
            text.AppendLine().Append(e.StackTrace);
            for (Exception inner = e.InnerException; inner != null; inner = inner.InnerException)
            {
                text.AppendLine().Append(" ---> ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message).AppendLine().Append(inner.StackTrace);
            }
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/', '\\');
        Logger.Log(level, home.Length > 1 ? text.Replace(home, "~").ToString() : text.ToString());
    }

    /// <summary>A request body, read to its end.</summary>
    private static byte[] ReadAll(Stream s)
    {
        using MemoryStream ms = new();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>A stream's bytes, up to a limit; null once more than that came (reading stops there).</summary>
    private static byte[] ReadAtMost(Stream s, int limit)
    {
        using MemoryStream ms = new();
        byte[] buffer = new byte[64 << 10];
        int n;
        while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + n > limit)
            {
                return null;
            }
            ms.Write(buffer, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>Writes a whole response and closes it, of the type given only (no browser guesses another).</summary>
    private static void Reply(HttpListenerResponse res, int code, string type, byte[] body)
    {
        res.StatusCode = code;
        res.ContentType = type;
        res.Headers["X-Content-Type-Options"] = "nosniff";
        res.ContentLength64 = body.Length;
        res.OutputStream.Write(body, 0, body.Length);
        res.OutputStream.Close();
    }

    /// <summary>ZDOMan's private dictionary of every object the game knows, by id, as a dictionary (main thread only).</summary>
    private static Dictionary<ZDOID, ZDO> AllZdos() => (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);

    /// <summary>The bytes compressed with gzip (fastest level): the answers of /snapshot and /zone.</summary>
    private static byte[] Gzip(byte[] raw)
    {
        using MemoryStream packed = new();
        using (GZipStream gz = new(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(raw, 0, raw.Length);
        }
        return packed.ToArray();
    }

    /// <summary>Answers 405 (POST only) and returns false unless the request is a POST.</summary>
    private static bool RequirePost(HttpListenerContext ctx)
    {
        if (ctx.Request.HttpMethod == "POST")
        {
            return true;
        }
        Reply(ctx.Response, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
        return false;
    }

    /// <summary>Answers 200 with the JSON the main thread makes for <paramref name="work"/>.</summary>
    private void ReplyJson(HttpListenerResponse res, Func<byte[]> work, int timeoutMs = 30000)
    {
        Reply(res, 200, "application/json", OnMainThread(work, timeoutMs));
    }

    /// <summary>Answers a POST with the JSON the main thread makes of its body (<paramref name="work"/>); other methods get 405.</summary>
    private void PostJson(HttpListenerContext ctx, Func<byte[], byte[]> work, int timeoutMs = 30000)
    {
        if (!RequirePost(ctx))
        {
            return;
        }
        byte[] body = ReadAll(ctx.Request.InputStream);
        ReplyJson(ctx.Response, () => work(body), timeoutMs);
    }

    /// <summary>Answers 200 with the main thread's answer for <paramref name="work"/>, gzip-compressed.</summary>
    private void ReplyGzip(HttpListenerResponse res, Func<byte[]> work, int timeoutMs = 30000)
    {
        byte[] raw = OnMainThread(work, timeoutMs);
        Reply(res, 200, "application/octet-stream", Gzip(raw));
    }

    /// <summary>Answers a POST with the main thread's answer for <paramref name="work"/> on its body, gzip-compressed; other methods get 405.</summary>
    private void PostGzip(HttpListenerContext ctx, Func<byte[], byte[]> work)
    {
        if (!RequirePost(ctx))
        {
            return;
        }
        byte[] body = ReadAll(ctx.Request.InputStream);
        ReplyGzip(ctx.Response, () => work(body));
    }
}
