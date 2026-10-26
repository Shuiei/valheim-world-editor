using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// A program folder and a BepInEx folder of the test's own, where add-ons are installed as players install them (a
/// folder of the add-on's name in the addons folder next to the program) and mods as a mod manager installs them; the
/// editor's manifests and extensions forgotten, and everything put back, when it is disposed. Its settings are saved in
/// a data folder of its own.
/// </summary>
internal sealed class AddOnSandbox : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-addon-check-" + Guid.NewGuid().ToString("N")[..8]);

    public AddOnSandbox()
    {
        LocalGame.SearchDefaultPlaces = false;
        AddOns.FolderForTests = _dir;
        // A data folder of its own: the editor's host reads the settings saved there (Run saves them).
        AppSettings.DataDirOverride = Path.Combine(_dir, "data");
        Settings.BepInExFolders = [Path.Combine(_dir, "BepInEx")];
        Directory.CreateDirectory(Path.Combine(_dir, "BepInEx", "plugins"));
    }

    /// <summary>Settings naming the sandbox's BepInEx folder, nothing allowed.</summary>
    public AppSettings Settings { get; } = new();

    /// <summary>Installs one of the repository's add-ons as its zip holds it: its manifest and its DLL.</summary>
    /// <returns>Its folder.</returns>
    public string Install(string name)
    {
        string folder = Path.Combine(_dir, AddOns.FolderName, name);
        Directory.CreateDirectory(folder);
        File.Copy(RepositoryAddOns.Manifest(name), Path.Combine(folder, ModManifests.FileName));
        if (RepositoryAddOns.Dll(name) is { } dll)
        {
            File.Copy(dll, Path.Combine(folder, Path.GetFileName(dll)));
        }
        return folder;
    }

    /// <summary>The mod an add-on serves, in a folder of its own under plugins (an empty stand-in for its DLL).</summary>
    public void InstallMod(string name)
    {
        if (RepositoryAddOns.Serves(name) is { } dll)
        {
            string folder = Path.Combine(_dir, "BepInEx", "plugins", "Someone-" + name);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, dll), [0]);
        }
    }

    /// <summary>
    /// Installs an add-on and its mod, saves the settings (what the editor's host reads, as in a real run) and has the
    /// editor read the manifests and load the extensions.
    /// </summary>
    public void Run(string name)
    {
        Install(name);
        InstallMod(name);
        if (Settings.Save() is { } problem)
        {
            throw new InvalidOperationException("the sandbox's settings could not be saved: " + problem);
        }
        ModManifests.Refresh(Settings);
        ModExtensions.Refresh(Settings);
    }

    /// <summary>Forgets the manifests and extensions, puts the program's folder and the default places back, and deletes the folders.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        ModManifests.Forget();
        AddOns.FolderForTests = null;
        AppSettings.DataDirOverride = _dataDir;
        LocalGame.SearchDefaultPlaces = _defaults;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }
}
