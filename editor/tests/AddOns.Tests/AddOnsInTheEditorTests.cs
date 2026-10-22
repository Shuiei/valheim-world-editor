using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Each of the repository's add-ons in the editor, installed as a player installs it: its DLL of its own version;
/// waiting while its mod is missing;
/// with the mod, its manifest used (its data holders known), its extension loaded and running without being allowed,
/// and every hook it registered called by the editor without turning it off. Runs alone (the manifests and extensions
/// are shared).
/// </summary>
[Collection("AddOns")]
public sealed class AddOnsInTheEditorTests : IDisposable
{
    private readonly AddOnSandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    public static TheoryData<string> Names() => RepositoryAddOns.Names();

    [Fact]
    public void TheRepositoryHasAddOns() => Assert.NotEmpty(RepositoryAddOns.Names());

    [Theory]
    [MemberData(nameof(Names))]
    public void AnAddOnsDllCarriesItsOwnVersion(string name)
    {
        // Released on its own zip, named after its VERSION (tools/addons.sh): the DLL says the same version.
        string version = File.ReadAllText(Path.Combine(RepositoryAddOns.Folder, Path.GetFileName(name), "VERSION")).Trim();
        if (RepositoryAddOns.Dll(name) is { } dll)
        {
            Assert.Equal(version, System.Reflection.AssemblyName.GetAssemblyName(dll).Version?.ToString(3));
            Assert.StartsWith(version, System.Diagnostics.FileVersionInfo.GetVersionInfo(dll).ProductVersion, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void AnAddOnWaitsForItsMod(string name)
    {
        _sandbox.Install(name);
        ModManifests.Refresh(_sandbox.Settings);
        var addOn = Assert.Single(ModManifests.InstalledAddOns);
        Assert.Equal(name, addOn.Name);
        Assert.Equal(RepositoryAddOns.Serves(name) == null, addOn.Served);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void AnAddOnRunsInTheEditorWithItsMod(string name)
    {
        string folder = _sandbox.Install(name);
        _sandbox.InstallMod(name);

        ModManifests.Refresh(_sandbox.Settings);
        Assert.True(Assert.Single(ModManifests.InstalledAddOns).Served);
        Assert.All(RepositoryAddOns.DataPrefabs(name), p => Assert.True(ModManifests.IsData(StableHash.Of(p)), p + " is not a data holder"));
        if (RepositoryAddOns.Dll(name) is not { } dll)
        {
            return;
        }

        Assert.Equal(1, ModExtensions.Refresh(_sandbox.Settings));
        var extension = Assert.Single(ModExtensions.Offered);
        Assert.Equal((name, ModExtensions.Status.Running, true, null), (extension.Mod, extension.State, extension.AddOn, extension.Error));
        Assert.Equal(Path.Combine(folder, Path.GetFileName(dll)), extension.Dll);
        Assert.Empty(_sandbox.Settings.Extensions);

        // Every hook the editor offers called as the editor calls them, with an empty area: none may throw (a hook
        // that throws turns its extension off).
        var area = new PlainArea(-64, -64, 64, 64, 30);
        ModExtensions.ScenesOf(area);
        Assert.All(ModExtensions.Tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Info.Name)));
        ModExtensions.CopyArea(area, new CopyRegion(Vector3.Zero, [new(-8, -8), new(8, -8), new(8, 8)], (_, _) => true));
        WorldGenerators.ForNewWorld(_sandbox.Settings);
        Assert.Equal(ModExtensions.Status.Running, Assert.Single(ModExtensions.Offered).State);
    }
}
