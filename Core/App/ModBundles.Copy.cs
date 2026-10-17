using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Copying the mods' bundles into the data folder, and re-packing each LZMA bundle as LZ4 so GameBundles reads it in place.
/// </summary>
public static partial class ModBundles
{
    /// <summary>
    /// Copies the bundles into <see cref="Folder"/> (emptied first), unless the stamp says they are there already.
    /// </summary>
    private static void Copy(List<Source> sources, string stamp)
    {
        bool same = File.Exists(StampFile) && File.ReadAllText(StampFile) == stamp && Directory.Exists(Folder);
        // The same sources as last time: only those that could not be copied then are tried again (a passing
        // error), the rest stays as copied.
        var retry = same && File.Exists(FailedFile) ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FailedFile)) ?? []).ToHashSet(StringComparer.Ordinal) : [];
        if (same && retry.Count == 0)
        {
            return;
        }
        if (!same)
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
            Directory.CreateDirectory(Folder);
        }
        var failed = new List<string>();
        foreach (var s in sources)
        {
            if (same && !retry.Contains(s.Name))
            {
                continue;
            }
            try
            {
                byte[] bytes = s.Resource == null ? File.ReadAllBytes(s.Path) : FromDll(s.Path, s.Resource) ?? [];
                if (bytes.Length > 0)
                {
                    File.WriteAllBytes(Path.Combine(Folder, s.Name), Lz4(bytes));
                }
            }
            // Whatever a damaged or unusual bundle makes the reader throw: that bundle alone is left out (the others
            // still go), else no mod's model would be drawn; it is tried again next time.
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Log.Caught(e, $"copying the mod bundle {s.Name} (left out, tried again next time)");
                failed.Add(s.Name);
            }
        }
        SafeFile.WriteAllText(StampFile, stamp);
        if (failed.Count > 0)
        {
            SafeFile.WriteAllText(FailedFile, JsonSerializer.Serialize(failed));
        }
        else if (File.Exists(FailedFile))
        {
            File.Delete(FailedFile);
        }
        Console.WriteLine($"mod bundles: {sources.Count - failed.Count} copied into {Folder}{(failed.Count > 0 ? $", {failed.Count} not (tried again next time)" : "")}.");
    }
}
