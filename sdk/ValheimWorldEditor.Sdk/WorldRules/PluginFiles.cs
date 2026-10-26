using System;
using System.IO;
using System.Linq;

namespace ValheimWorldEditor.Sdk;

/// <summary>Plugin DLLs in a BepInEx plugins folder, as a mod manager lays them out (at any depth, any letter case).</summary>
public static class PluginFiles
{
    /// <summary>
    /// Where a plugin's DLL is in a plugins folder under its own name, the first in name order; null when it is not
    /// there (a mod manager's file of a mod turned off, "Mod.dll.old", does not count), or the folder cannot be read
    /// (said to <paramref name="log"/>).
    /// </summary>
    /// <param name="plugins">The plugins folder.</param>
    /// <param name="dll">The DLL's file name.</param>
    /// <param name="log">Where a folder that cannot be read is told; null: nowhere.</param>
    public static string? Find(string plugins, string dll, Action<string>? log = null)
    {
        try
        {
            return Directory.Exists(plugins)
                ? Directory.EnumerateFiles(plugins, dll, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0, MatchCasing = MatchCasing.CaseInsensitive })
                    .Where(f => string.Equals(Path.GetFileName(f), dll, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).FirstOrDefault()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"searching {plugins} for {dll} failed (taken as not there): {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Whether a plugin's DLL is in a plugins folder (<see cref="Find"/>).</summary>
    /// <param name="plugins">The plugins folder.</param>
    /// <param name="dll">The DLL's file name.</param>
    /// <param name="log">Where a folder that cannot be read is told; null: nowhere.</param>
    public static bool Has(string plugins, string dll, Action<string>? log = null) => Find(plugins, dll, log) != null;
}
