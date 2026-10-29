#pragma warning disable IDE0240 // The plugin builds without nullable annotations; the editor's tests, which compile this file too, with them.
#nullable enable
#pragma warning restore IDE0240
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WorldEditorBridge;

/// <summary>
/// Who may use the bridge: callers from this machine or its local networks, with the token in the X-Bridge-Token
/// header; and what the log warns when BindAddress opens it beyond this machine. Nothing of the game's here, so the
/// editor's tests compile this file too (Core.Tests, Plugin/BridgeAccessTests).
/// </summary>
internal static class BridgeAccess
{
    /// <summary>The header the editor sends the token in (never the address: it would end up in logs and histories).</summary>
    public const string TokenHeader = "X-Bridge-Token";

    /// <summary>
    /// Why a request is refused, as the status and text to answer with, or null when it may go on: a caller from
    /// elsewhere than this machine and its local networks (403, before its token is even looked at), then a token
    /// missing from the header or wrong (401).
    /// </summary>
    /// <param name="caller">The caller's address (null: unknown, refused).</param>
    /// <param name="headers">The request's headers.</param>
    /// <param name="token">The plugin's token.</param>
    public static (int Status, string Text)? Refusal(IPAddress? caller, NameValueCollection headers, string token)
    {
        if (!MayConnect(caller))
        {
            return (403, "only this machine and its local networks may connect: use an SSH tunnel to 127.0.0.1");
        }
        if (!SameToken(headers[TokenHeader], token))
        {
            return (401, "bad or missing token");
        }
        return null;
    }

    /// <summary>
    /// Whether a caller may connect: this machine (loopback) or a local network's address (10/8, 172.16/12, 192.168/16,
    /// link-local; IPv6 unique local fc00::/7 or link-local), or a VPN's shared range 100.64/10 (Tailscale's: it is
    /// never routed on the internet), never one from the internet. With BindAddress at its default (127.0.0.1) every
    /// caller is this machine.
    /// </summary>
    /// <param name="ip">The caller's address (null: unknown, refused).</param>
    public static bool MayConnect(IPAddress? ip)
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
    /// runtime's own comparison stops at the first difference, which a caller could time). An empty token is never
    /// the plugin's.
    /// </summary>
    /// <param name="given">The token sent (null: none).</param>
    /// <param name="expected">The plugin's token.</param>
    public static bool SameToken(string? given, string? expected)
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
    /// What the log warns when BindAddress lets other machines in (their local networks, never the internet's, and
    /// the token unencrypted); null when it is this machine only (127.0.0.1, localhost, ::1, another loopback address).
    /// </summary>
    /// <param name="bind">The BindAddress setting.</param>
    public static string? BindWarning(string bind)
    {
        bool loopback = string.Equals(bind, "localhost", System.StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(bind.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));
        return loopback
            ? null
            : $"WorldEditorBridge: BindAddress {bind} lets other machines of this one's local networks connect (never the internet's), "
                + "and the token travels unencrypted (plain HTTP): anyone on those networks can read it and change the world. "
                + "Keep 127.0.0.1 and reach it through an SSH tunnel (the editor makes one).";
    }
}
