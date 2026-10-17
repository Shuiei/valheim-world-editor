using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// A mod's name as players know it: the package it was installed from (the folder it is in under plugins, as mod
    /// managers make them: its manifest.json's name, else the folder's name without its "Author-"), else the
    /// plugin's own name. A package can hold several plugins, and Jötunn names the one that added a prefab
    /// (ValheimRAFT's are added by its "Inventory Patch Detector").
    /// </summary>
    private string ModName(BepInEx.BepInPlugin plugin)
    {
        if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(plugin.GUID, out BepInEx.PluginInfo info) || string.IsNullOrEmpty(info.Location))
        {
            return plugin.Name;
        }
        string plugins = Path.GetFullPath(BepInEx.Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string dll = Path.GetFullPath(info.Location);
        if (!dll.StartsWith(plugins, StringComparison.OrdinalIgnoreCase))
        {
            return plugin.Name;
        }
        string[] parts = dll.Substring(plugins.Length).Split(Path.DirectorySeparatorChar);
        if (parts.Length < 2)
        {
            return plugin.Name;
        }
        string folder = parts[0];
        try
        {
            string manifest = Path.Combine(plugins + folder, "manifest.json");
            if (File.Exists(manifest)
                && Regex.Match(File.ReadAllText(manifest), "\"name\"\\s*:\\s*\"([^\"]+)\"") is { Success: true } m)
            {
                return m.Groups[1].Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable manifest: the folder's name.
            Caught(ex, $"reading the manifest.json of {folder} (its folder's name is used)", brief: true);
        }
        int dash = folder.IndexOf('-');
        return dash > 0 && dash < folder.Length - 1 ? folder.Substring(dash + 1) : folder;
    }
}
