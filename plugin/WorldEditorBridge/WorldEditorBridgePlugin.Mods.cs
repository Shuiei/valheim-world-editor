using System;
using System.Net;
using System.Text;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

/// <summary>
/// The mods' plugins taking part (WorldEditorBridge.Api, a registry they fill): GET /mods lists them, POST
/// /mods/&lt;id&gt;/&lt;name&gt; calls one of a mod's routes on the main thread, and every apply tells the listeners what
/// changed, after it, on the main thread too; a listener that throws is logged and changes nothing of what was done.
/// </summary>
public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>A call's body larger than this is refused.</summary>
    private const int MaxModBody = 16 << 20;

    /// <summary>GET /mods: the bridge's API version and the ids registered (JSON).</summary>
    private static string Mods()
    {
        StringBuilder sb = new("{\"api\":");
        sb.Append(BridgeApi.Version).Append(",\"mods\":[");
        bool first = true;
        foreach (string id in BridgeApi.ModIds)
        {
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append(Json(id));
        }
        return sb.Append("]}").ToString();
    }

    /// <summary>
    /// POST /mods/&lt;id&gt;/&lt;name&gt;: the mod's answer (application/octet-stream); 404 when no such mod or route is
    /// registered, 413 for a body too large.
    /// </summary>
    private void CallMod(HttpListenerResponse res, string path, byte[] body)
    {
        string[] parts = path.Split('/');
        // "", "mods", id, name.
        if (parts.Length != 4 || !BridgeApi.IsValidId(parts[2]) || !BridgeApi.IsValidId(parts[3]))
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("a mod's route is /mods/<mod id>/<route name>"));
            return;
        }
        if (body.Length > MaxModBody)
        {
            Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the body is too large"));
            return;
        }
        string id = parts[2], name = parts[3];
        IBridgeRoutes routes = BridgeApi.RoutesOf(id);
        if (routes == null)
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes($"no mod {id} is registered with the bridge"));
            return;
        }
        byte[] answer = OnMainThread(() => routes.Handle(name, body));
        if (answer == null)
        {
            Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes($"the mod {id} has no route {name}"));
            return;
        }
        Reply(res, 200, "application/octet-stream", answer);
    }

    /// <summary>Tells the listeners what an apply changed, then gives its answer on. Main thread.</summary>
    private byte[] Applied((byte[] Json, AppliedChanges Changes) result)
    {
        foreach (var registered in BridgeApi.Listeners)
        {
            try
            {
                registered.Value.OnApplied(result.Changes);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"WorldEditorBridge: the mod {registered.Key} failed on what the editor applied: {ex}");
            }
        }
        return result.Json;
    }

    /// <summary>An object as the listeners are told of it.</summary>
    private static AppliedObject Applied(ZDO zdo)
    {
        Vector3 p = zdo.GetPosition();
        return new AppliedObject(zdo.m_uid.UserID, zdo.m_uid.ID, zdo.GetPrefab(), p.x, p.y, p.z);
    }
}
