using Renci.SshNet;

namespace TerrainEditor.App;

/// <summary>
/// Checks run on the server through a tunnel's open SSH connection: the plugin's port from its settings, and why the
/// plugin does not answer. Each runs its command off the caller's thread, and gives up after 15 s.
/// </summary>
internal static class TunnelServerCheck
{
    /// <summary>The plugin's port from its settings in the server's Valheim folder, when they hold a valid one.</summary>
    /// <param name="client">The connected client.</param>
    /// <param name="gameFolder">The server's Valheim folder (no trailing slash).</param>
    /// <returns>The port, or null when the settings do not give one.</returns>
    internal static async Task<int?> ReadBridgePort(SshClient client, string gameFolder)
    {
        string cfg = gameFolder + "/BepInEx/config/";
        // Off the caller's thread whatever the connect did: the command waits on the server (15 s at most).
        string text = await Task.Run(() => Run(client, $"cat {Quote(cfg + LocalGame.ConfigName)} 2>/dev/null || cat {Quote(cfg + LocalGame.OldConfigName)} 2>/dev/null")).ConfigureAwait(false);
        if (System.Text.RegularExpressions.Regex.Match(text, @"^\s*Port\s*=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Multiline) is { Success: true } pm
            && int.TryParse(pm.Groups[1].Value, out int cfgPort) && ServerConfig.IsPort(cfgPort))
        {
            return cfgPort;
        }
        return null;
    }

    /// <summary>Why the plugin does not answer, from what the server's Valheim folder holds.</summary>
    /// <param name="client">The connected client.</param>
    /// <param name="gameFolder">The server's Valheim folder.</param>
    /// <returns>The message for the player, or null when the check gave no known answer.</returns>
    internal static async Task<string?> Diagnose(SshClient client, string gameFolder)
    {
        string f = gameFolder.TrimEnd('/');
        string q = Quote(f);
        string command = $"if [ ! -d {q} ]; then echo nofolder; elif [ ! -d {q}/BepInEx ]; then echo nobepinex; " +
            $"elif ! find {q}/BepInEx/plugins -name WorldEditorBridge.dll 2>/dev/null | grep -q .; then echo noplugin; " +
            $"elif [ ! -f {q}/BepInEx/config/{LocalGame.ConfigName} ] && [ ! -f {q}/BepInEx/config/{LocalGame.OldConfigName} ]; then echo nocfg; else echo installed; fi";
        string answer = (await Task.Run(() => Run(client, command)).ConfigureAwait(false)).Trim();
        return answer switch
        {
            "nofolder" => $"The folder {f} does not exist on the server: check the server's Valheim folder.",
            "nobepinex" => $"BepInEx is not installed in {f} on the server: install BepInExPack for Valheim there.",
            "noplugin" => $"The plugin is not in {f}/BepInEx/plugins on the server: copy WorldEditorBridge.dll there and restart the server.",
            "nocfg" => "The plugin is installed but has never run: restart the server with BepInEx.",
            "installed" => "The plugin is installed but does not answer: is the server running, and started with BepInEx (not the plain start script)?",
            _ => null,
        };
    }

    /// <summary>A path quoted for the server's shell (single quotes).</summary>
    /// <param name="path">The path.</param>
    /// <returns>The quoted path.</returns>
    internal static string Quote(string path) => "'" + path.Replace("'", "'\\''") + "'";

    /// <summary>A shell command's output on the server (15 s at most); empty when it fails.</summary>
    /// <param name="client">The connected client.</param>
    /// <param name="command">The command to run.</param>
    /// <returns>The output, or empty when the command failed (the failure is logged).</returns>
    internal static string Run(SshClient client, string command)
    {
        try
        {
            using SshCommand cmd = client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(15);
            return cmd.Execute();
        }
        catch (Exception e)
        {
            Log.CaughtBrief(e, "running a check command on the server (no answer)");
            return "";
        }
    }
}
