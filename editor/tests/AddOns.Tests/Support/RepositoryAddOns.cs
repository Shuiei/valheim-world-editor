using System.Text.Json.Nodes;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The repository's add-ons (addons/&lt;Name&gt;/): each one's manifest and the DLL its build made.</summary>
internal static class RepositoryAddOns
{
    /// <summary>The repository's addons folder, found above the tests.</summary>
    public static string Folder { get; } = Find();

    /// <summary>Every add-on's name (its folder's), for the theories.</summary>
    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(Folder).Where(d => File.Exists(Path.Combine(d, "VERSION"))).Order(StringComparer.Ordinal))
        {
            names.Add(Path.GetFileName(dir));
        }
        return names;
    }

    /// <summary>An add-on's manifest: the worldeditor.json beside its project (addons/&lt;Name&gt;/src/&lt;Project&gt;/).</summary>
    public static string Manifest(string name) =>
        Directory.GetDirectories(Path.Combine(Folder, name, "src")).Select(d => Path.Combine(d, "worldeditor.json")).Single(File.Exists);

    /// <summary>
    /// The extension DLL an add-on's build made, in this run's configuration (its project's bin/&lt;configuration&gt;);
    /// null when its manifest names none.
    /// </summary>
    public static string? Dll(string name)
    {
        string manifest = Manifest(name);
        if ((string?)JsonNode.Parse(File.ReadAllText(manifest))!["extension"] is not { } dll)
        {
            return null;
        }
        // This test project's own configuration (bin/<configuration>/net10.0/), the one the add-ons were built in.
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string built = Path.Combine(Path.GetDirectoryName(manifest)!, "bin", configuration);
        return Directory.GetFiles(built, dll, SearchOption.AllDirectories).Single();
    }

    /// <summary>What the manifest says the add-on serves (the mod's DLL), null for none.</summary>
    public static string? Serves(string name) => (string?)JsonNode.Parse(File.ReadAllText(Manifest(name)))!["serves"];

    /// <summary>The prefabs the manifest marks as holding only the mod's data.</summary>
    public static IEnumerable<string> DataPrefabs(string name) =>
        JsonNode.Parse(File.ReadAllText(Manifest(name)))!["prefabs"] is JsonObject prefabs
            ? prefabs.Where(p => p.Value?["data"]?.GetValue<bool>() == true).Select(p => p.Key)
            : [];

    private static string Find()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string addons = Path.Combine(d.FullName, "addons");
            if (Directory.Exists(addons) && File.Exists(Path.Combine(d.FullName, "VERSION")))
            {
                return addons;
            }
        }
        throw new DirectoryNotFoundException("the repository's addons folder not found above " + AppContext.BaseDirectory);
    }
}
