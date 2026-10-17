using System.Globalization;
using System.Text;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>A JSON string literal: backslashes, quotes and control characters escaped.</summary>
    private static string Json(string s)
    {
        StringBuilder sb = new("\"");
        foreach (char c in s)
        {
            _ = c switch
            {
                '\\' => sb.Append("\\\\"),
                '"' => sb.Append("\\\""),
                < ' ' => sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)),
                _ => sb.Append(c),
            };
        }
        return sb.Append('"').ToString();
    }

    /// <summary>A float for JSON: round-trip form, invariant culture.</summary>
    private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// GET /status: plugin version, world and seed names, object and player counts, the network time and the features
    /// this plugin has (editors look for "move" before sending moves to /objects, and "once" before sending call ids), and
    /// this run of it (<see cref="_run"/>), as JSON.
    /// </summary>
    private byte[] Status()
    {
        World w = ZNet.World;
        var dict = AllZdos();
        string json = $"{{\"plugin\":{Json(Version)},\"world\":{Json(w?.m_name ?? "")},\"seedName\":{Json(w?.m_seedName ?? "")},\"objects\":{dict.Count},\"players\":{ZNet.instance.GetPlayerList().Count},\"netTime\":{ZNet.instance.GetTimeSeconds().ToString("R", CultureInfo.InvariantCulture)},\"features\":[\"move\",\"once\"],\"run\":{Json(_run)}}}";
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// GET /players: each connected player's name, position and yaw (degrees), as a JSON array. The position is the
    /// character object's when the server has it, else the one the player reported.
    /// </summary>
    private byte[] Players()
    {
        StringBuilder sb = new("[");
        bool first = true;
        foreach (ZNet.PlayerInfo p in ZNet.instance.GetPlayerList())
        {
            ZDO zdo = p.m_characterID.IsNone() ? null : ZDOMan.instance.GetZDO(p.m_characterID);
            Vector3 pos = zdo != null ? zdo.GetPosition() : p.m_position;
            float yaw = zdo != null ? zdo.GetRotation().eulerAngles.y : 0f;
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append($"{{\"name\":{Json(p.m_name ?? "")},\"x\":{F(pos.x)},\"y\":{F(pos.y)},\"z\":{F(pos.z)},\"yaw\":{F(yaw)}}}");
        }
        return Encoding.UTF8.GetBytes(sb.Append(']').ToString());
    }
}
