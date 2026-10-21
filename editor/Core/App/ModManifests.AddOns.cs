using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Reading the add-ons' manifests (AddOns): each listed, and what it says used while the mod it serves is installed.
/// </summary>
public static partial class ModManifests
{
    /// <summary>
    /// Lists every installed add-on in <paramref name="addOns"/> and adds what the manifests of those in use say to
    /// <paramref name="notes"/>. One whose manifest is not one this editor reads, or whose "serves" is not a DLL's
    /// name, is said in the log and left out.
    /// </summary>
    /// <returns>How many manifests were used.</returns>
    private static int ReadAddOns(AppSettings settings, Notes notes, List<AddOns.AddOn> addOns)
    {
        int read = 0;
        foreach (var (file, folder) in AddOns.Manifests())
        {
            string name = Path.GetFileName(folder);
            try
            {
                if (file.Length > MaxFile || ParseManifest(File.ReadAllText(file.FullName)) is not { } root)
                {
                    Console.WriteLine($"add-on {name}: its {FileName} is not one this editor reads, skipped.");
                    continue;
                }
                if (!Text(root["serves"], out string? serves) || !AddOns.IsDllName(serves))
                {
                    Console.WriteLine($"add-on {name}: \"serves\" is not a mod's DLL name (like \"SomeMod.dll\"), skipped.");
                    continue;
                }
                bool served = serves == null || AddOns.IsInstalled(settings, serves);
                addOns.Add(new AddOns.AddOn(name, folder, serves, served, ExtensionPath(root, folder)));
                if (!served)
                {
                    Console.WriteLine($"add-on {name}: waiting for {serves} (not in the plugins folders).");
                    continue;
                }
                Read(root, notes, (folder, name), addOn: true);
                read++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Log.Caught(e, $"reading the add-on {name}'s {FileName} (skipped)");
            }
        }
        return read;
    }
}
