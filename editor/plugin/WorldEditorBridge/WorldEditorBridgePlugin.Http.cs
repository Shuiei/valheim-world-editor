using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// The listener thread's loop: each request is answered on the thread pool, until the plugin stops.
    /// </summary>
    private void Serve()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch (Exception ex)
            {
                if (!_running)
                {
                    // The listener was stopped (OnDestroy): the end of the loop, not an error.
                    Logger.LogInfo("WorldEditorBridge: stopped listening.");
                    return;
                }
                Caught(ex, "waiting for the next request (waited for again)", brief: true);
                continue;
            }
            ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
        }
    }

    /// <summary>
    /// Whether a caller may connect: this machine (loopback) or a local network's address (10/8, 172.16/12, 192.168/16,
    /// link-local; IPv6 unique local fc00::/7 or link-local), or a VPN's shared range 100.64/10 (Tailscale's: it is
    /// never routed on the internet), never one from the internet. With BindAddress at its default (127.0.0.1) every
    /// caller is this machine.
    /// </summary>
    /// <param name="ip">The caller's address (null: unknown, refused).</param>
    internal static bool MayConnect(IPAddress ip)
    {
        if (ip == null)
        {
            return false;
        }
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        byte[] b = ip.GetAddressBytes();
        return ip.AddressFamily == AddressFamily.InterNetwork
            ? b[0] == 10 || (b[0] == 172 && (b[1] & 0xF0) == 16) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && (b[1] & 0xC0) == 64)
            : (b[0] & 0xFE) == 0xFC || ip.IsIPv6LinkLocal;
    }

    /// <summary>
    /// Whether the token sent is the plugin's, compared in a time that does not depend on where they differ (the
    /// runtime's own comparison stops at the first difference, which a caller could time).
    /// </summary>
    /// <param name="given">The token sent (null: none).</param>
    /// <param name="expected">The plugin's token.</param>
    internal static bool SameToken(string given, string expected)
    {
        byte[] a = Encoding.UTF8.GetBytes(given ?? ""), b = Encoding.UTF8.GetBytes(expected ?? "");
        int diff = a.Length ^ b.Length;
        for (int i = 0; i < b.Length; i++)
        {
            diff |= (i < a.Length ? a[i] : 0) ^ b[i];
        }
        return diff == 0 && b.Length > 0;
    }

    /// <summary>
    /// Answers one request by its path, after checking the token and that this game hosts a loaded world (a client
    /// connected to someone else's server only knows the objects around it). A failure answers 500 with its message.
    /// </summary>
    private void Handle(HttpListenerContext ctx)
    {
        HttpListenerResponse res = ctx.Response;
        try
        {
            // Only this machine and its local networks, never the internet's, whatever BindAddress opens.
            if (!MayConnect(ctx.Request.RemoteEndPoint?.Address))
            {
                Reply(res, 403, "text/plain", Encoding.UTF8.GetBytes("only this machine and its local networks may connect: use an SSH tunnel to 127.0.0.1"));
                return;
            }
            // In the header only: a token in the URL ends up in logs and histories.
            if (!SameToken(ctx.Request.Headers["X-Bridge-Token"], _token.Value))
            {
                Reply(res, 401, "text/plain", Encoding.UTF8.GetBytes("bad or missing token"));
                return;
            }
            if (ZNet.instance == null || ZDOMan.instance == null || ZoneSystem.instance == null)
            {
                Reply(res, 503, "text/plain", Encoding.UTF8.GetBytes("no world loaded yet"));
                return;
            }
            // A client connected to someone else's server only knows the objects around it.
            if (!ZNet.instance.IsServer())
            {
                Reply(res, 409, "text/plain", Encoding.UTF8.GetBytes("this game is connected to a server: install WorldEditorBridge on that server, or host the world yourself"));
                return;
            }
            string path = ctx.Request.Url.AbsolutePath;
            if (path.StartsWith("/mods/", StringComparison.Ordinal))
            {
                if (!RequirePost(ctx))
                {
                    return;
                }
                // A mod's body is bounded, and refused before it is held whole.
                byte[] body = ctx.Request.ContentLength64 > MaxModBody ? null : ReadAtMost(ctx.Request.InputStream, MaxModBody);
                if (body == null)
                {
                    Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the body is too large"));
                    return;
                }
                CallMod(res, path, body);
                return;
            }
            Route(ctx, path);
        }
        catch (Exception ex)
        {
            // The path comes from the request: keep line breaks out so it cannot forge log lines.
            string path = ctx.Request.Url.AbsolutePath.Replace("\r", "").Replace("\n", "");
            Caught(ex, $"answering the request {path}");
            try
            {
                Reply(res, 500, "text/plain", Encoding.UTF8.GetBytes(ex.Message));
            }
            catch (Exception replyError)
            {
                Caught(replyError, $"sending the error answer to the request {path} (its client has gone)", brief: true);
            }
        }
    }
}
