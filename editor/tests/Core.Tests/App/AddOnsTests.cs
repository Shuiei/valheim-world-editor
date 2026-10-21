using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Editor add-ons (AddOns): installed in the addons folder next to the program, one folder each; listed whether in use
/// or not; their notes used and their extension run (without being allowed in Settings) while the mod's DLL they serve
/// is in a plugins folder, or always when they serve none, unless their switch in Settings is off; a mod's own manifest read after them; ones not in the expected
/// form left out. Runs alone (the manifests and extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class AddOnsTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-addons-" + Guid.NewGuid().ToString("N")[..8]);

    public AddOnsTests()
    {
        LocalGame.SearchDefaultPlaces = false;
        AddOns.FolderForTests = _dir;
        Settings.BepInExFolders = [Path.Combine(_dir, "BepInEx")];
        Directory.CreateDirectory(Path.Combine(_dir, "BepInEx", "plugins"));
    }

    /// <summary>Forgets the manifests and extensions, puts the program's folder and the default places back, and deletes the folder.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        ModManifests.Forget();
        AddOns.FolderForTests = null;
        LocalGame.SearchDefaultPlaces = _defaults;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>Settings naming the test's BepInEx folder (and nothing allowed).</summary>
    private AppSettings Settings { get; } = new();

    /// <summary>The sample extension's DLL, built with the tests.</summary>
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "ExampleVehicles.WorldEditor.dll");

    /// <summary>An add-on's folder with its manifest (the JSON given after the version).</summary>
    private string AddOn(string name, string more)
    {
        string folder = Path.Combine(_dir, AddOns.FolderName, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModManifests.FileName), $$"""{ "version": 1{{more}} }""");
        return folder;
    }

    /// <summary>A mod's DLL in the plugins folder, in a folder of its own.</summary>
    private string Plugin(string mod, string dll)
    {
        string folder = Path.Combine(_dir, "BepInEx", "plugins", mod);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, dll), [0]);
        return folder;
    }

    [Fact]
    public void AnAddOnIsUsedWhileTheModItServesIsInstalled()
    {
        string folder = AddOn("Digging", """, "serves": "DigMod.dll", "prefabs": { "dig_record": { "data": true } }""");

        Assert.Equal(0, ModManifests.Refresh(Settings));
        Assert.Equal(new AddOns.AddOn("Digging", folder, "DigMod.dll", false, null), Assert.Single(ModManifests.InstalledAddOns));
        Assert.False(ModManifests.IsData(StableHash.Of("dig_record")));

        Plugin("Someone-DigMod", "digmod.dll");
        Assert.Equal(1, ModManifests.Refresh(Settings));
        Assert.True(Assert.Single(ModManifests.InstalledAddOns).Served);
        Assert.True(ModManifests.IsData(StableHash.Of("dig_record")));
    }

    [Fact]
    public void AnAddOnTurnedOffIsListedButNothingOfItIsUsed()
    {
        string folder = AddOn("Vehicles", """, "extension": "ExampleVehicles.WorldEditor.dll", "prefabs": { "dig_record": { "data": true } }""");
        File.Copy(Sample, Path.Combine(folder, "ExampleVehicles.WorldEditor.dll"));
        Settings.AddOnsOff = ["Vehicles"];

        Assert.Equal(0, ModManifests.Refresh(Settings));
        Assert.Equal(0, ModExtensions.Refresh(Settings));
        Assert.False(Assert.Single(ModManifests.InstalledAddOns).On);
        Assert.False(ModManifests.IsData(StableHash.Of("dig_record")));
        Assert.Empty(ModExtensions.Offered);

        Settings.AddOnsOff = [];
        Assert.Equal(1, ModManifests.Refresh(Settings));
        Assert.True(Assert.Single(ModManifests.InstalledAddOns).On);
        Assert.True(ModManifests.IsData(StableHash.Of("dig_record")));
    }

    [Fact]
    public void SettingsSwitchesTurnAddOnsOffAndKeepThoseNotListed()
    {
        Assert.Equal(["Gone", "Tunnels"], AddOns.TurnedOff(["Gone", "Rivers"], [("Rivers", true), ("Tunnels", false)]));
        Assert.Empty(AddOns.TurnedOff([], [("Rivers", true)]));
    }

    [Fact]
    public void AnAddOnsExtensionRunsWithoutBeingAllowed()
    {
        string folder = AddOn("Vehicles", """, "extension": "ExampleVehicles.WorldEditor.dll", "gameComponents": ["Ship"]""");
        File.Copy(Sample, Path.Combine(folder, "ExampleVehicles.WorldEditor.dll"));

        ModManifests.Refresh(Settings);
        Assert.Equal(1, ModExtensions.Refresh(Settings));

        var x = Assert.Single(ModExtensions.Offered);
        Assert.Equal(("Vehicles", ModExtensions.Status.Running, true), (x.Mod, x.State, x.AddOn));
        Assert.Equal(Path.Combine(folder, "ExampleVehicles.WorldEditor.dll"), Assert.Single(ModManifests.InstalledAddOns).Dll);
        Assert.Empty(Settings.Extensions);
        Assert.Equal(["Ship"], ModExtensions.RequestedComponents);
    }

    [Fact]
    public void AnAddOnWhoseModIsMissingRunsNothing()
    {
        string folder = AddOn("Vehicles", """, "serves": "Vehicles.dll", "extension": "ExampleVehicles.WorldEditor.dll" """);
        File.Copy(Sample, Path.Combine(folder, "ExampleVehicles.WorldEditor.dll"));

        ModManifests.Refresh(Settings);
        Assert.Equal(0, ModExtensions.Refresh(Settings));
        Assert.Empty(ModExtensions.Offered);
        Assert.False(Assert.Single(ModManifests.InstalledAddOns).Served);
    }

    [Fact]
    public void AModsOwnManifestHasTheLastWord()
    {
        AddOn("Digging", """, "serves": "DigMod.dll", "prefabs": { "dig_tool": { "group": "Add-on" }, "dig_shovel": { "group": "Add-on" } }""");
        string mod = Plugin("DigMod", "DigMod.dll");
        File.WriteAllText(Path.Combine(mod, ModManifests.FileName), """{ "version": 1, "prefabs": { "dig_tool": { "group": "Mod" } } }""");
        // Older than the add-on's: the mod's own file is still read after it.
        File.SetLastWriteTimeUtc(Path.Combine(mod, ModManifests.FileName), DateTime.UtcNow.AddDays(-30));

        Assert.Equal(2, ModManifests.Refresh(Settings));
        Assert.Equal("Mod", ModManifests.PrefabOf("dig_tool")?.Group);
        Assert.Equal("Add-on", ModManifests.PrefabOf("dig_shovel")?.Group);
    }

    [Fact]
    public void AddOnsNotInTheExpectedFormAreLeftOut()
    {
        AddOn("Path", """, "serves": "../DigMod.dll" """);
        AddOn("NotADll", """, "serves": "DigMod.txt" """);
        File.WriteAllText(Path.Combine(AddOn("Newer", ""), ModManifests.FileName), """{ "version": 99 }""");
        File.WriteAllText(Path.Combine(AddOn("Broken", ""), ModManifests.FileName), "{ not json");
        string deep = Path.Combine(_dir, AddOns.FolderName, "Holder", "Inner");
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(deep, ModManifests.FileName), """{ "version": 1 }""");
        AddOn("Outside", """, "extension": "../Elsewhere.dll" """);

        Assert.Equal(1, ModManifests.Refresh(Settings));
        var outside = Assert.Single(ModManifests.InstalledAddOns);
        Assert.Equal(("Outside", null, true, null), (outside.Name, outside.Serves, outside.Served, outside.Dll));
        Assert.Empty(ModManifests.Extensions);
    }

    [Fact]
    public void AnAddOnServesADllNamedPlainly()
    {
        Assert.True(AddOns.IsDllName(null));
        Assert.True(AddOns.IsDllName("Tunnelheim.DLL"));
        Assert.False(AddOns.IsDllName(".dll"));
        Assert.False(AddOns.IsDllName("plugins/Tunnelheim.dll"));
        Assert.False(AddOns.IsDllName(@"plugins\Tunnelheim.dll"));
        Assert.False(AddOns.IsDllName("Tunnelheim.so"));
    }

    [Fact]
    public void AddOnsAreNextToTheProgramOrTheMacApp()
    {
        string program = Path.Combine(_dir, "Editor");
        Assert.Equal([Path.Combine(program, "addons")], AddOns.FoldersFor(program));
        string mac = Path.Combine(_dir, "Apps", "ValheimWorldEditor.app", "Contents", "MacOS");
        Assert.Equal([Path.Combine(mac, "addons"), Path.Combine(_dir, "Apps", "addons")], AddOns.FoldersFor(mac));
        Assert.Equal([Path.Combine(_dir, "addons")], AddOns.Folders);
    }
}
