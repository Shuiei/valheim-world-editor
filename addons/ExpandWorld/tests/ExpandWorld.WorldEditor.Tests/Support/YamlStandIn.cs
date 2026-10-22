namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// The YamlDotNet library a BepInEx folder gets from the ValheimModding-YamlDotNet package, stood in for by the tests'
/// own copy: the editor ships none and loads the player's (YamlReader), so a test folder with Expand World Data's files
/// needs one under its plugins.
/// </summary>
public static class YamlStandIn
{
    /// <summary>The package folder under plugins the library is installed in.</summary>
    public const string Package = "ValheimModding-YamlDotNet";

    /// <summary>Puts the tests' copy of the library where the package puts it under a BepInEx folder, and gives its path.</summary>
    /// <param name="bepInEx">The BepInEx folder (made when missing, plugins and all).</param>
    public static string Install(string bepInEx)
    {
        string package = Path.Combine(bepInEx, "plugins", Package);
        Directory.CreateDirectory(package);
        string dll = Path.Combine(package, YamlReader.DllName);
        File.Copy(typeof(YamlDotNet.Serialization.Deserializer).Assembly.Location, dll, overwrite: true);
        return dll;
    }
}
