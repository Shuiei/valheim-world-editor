using System.Reflection;

[assembly: Xunit.AssemblyFixture(typeof(WorldEditor.Tests.TestMods.Cleanup))]

namespace WorldEditor.Tests;

/// <summary>
/// The game mods a test installs into its BepInEx folder (Expand World, Riverheim, BepInEx itself): the real packages, downloaded from Thunderstore when
/// the test project builds (editor/tests/TestMods.targets) into the system's temporary folder, deleted when the test run ends.
/// </summary>
public static class TestMods
{
    /// <summary>A mod the tests install: its package.</summary>
    /// <param name="Name">Its Thunderstore package (the folder a mod manager puts it in).</param>
    /// <param name="Dll">Its plugin's file.</param>
    /// <param name="Inside">The folder its plugin is in inside the package (none: at its top).</param>
    public sealed record Package(string Name, string Dll, string Inside = "");

    /// <summary>Expand World Size.</summary>
    public static readonly Package Size = new("JereKuusela-Expand_World_Size", "ExpandWorldSize.dll");

    /// <summary>Expand World Data.</summary>
    public static readonly Package Data = new("JereKuusela-Expand_World_Data", "ExpandWorldData.dll");

    /// <summary>Expand World Rivers.</summary>
    public static readonly Package Rivers = new("JereKuusela-Expand_World_Rivers", "ExpandWorldRivers.dll");

    /// <summary>Riverheim.</summary>
    public static readonly Package Riverheim = new("Gurebu-Riverheim", "Riverheim.dll", "BepInEx/plugins");

    /// <summary>BepInEx's own assemblies (its pack's core folder), which Riverheim's plugin classes name.</summary>
    private static string BepInExCore => Path.Combine(Folder, "denikson-BepInExPack_Valheim", "BepInExPack_Valheim", "BepInEx", "core");

    /// <summary>Where the build put the downloaded mods.</summary>
    public static string Folder { get; } = typeof(TestMods).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "TestMods").Value!;

    /// <summary>Installs a mod into a BepInEx folder as a mod manager does (its package's folder under plugins), and gives its plugin's path.</summary>
    /// <param name="bepInEx">The BepInEx folder (made when missing).</param>
    /// <param name="mod">The mod.</param>
    public static string Install(string bepInEx, Package mod)
    {
        string from = Path.Combine(Folder, mod.Name, mod.Inside, mod.Dll);
        if (!File.Exists(from))
        {
            throw new FileNotFoundException($"{mod.Dll} was not downloaded: build the test project again (editor/tests/TestMods.targets)", from);
        }
        string package = Path.Combine(bepInEx, "plugins", mod.Name);
        Directory.CreateDirectory(package);
        string dll = Path.Combine(package, mod.Dll);
        File.Copy(from, dll, overwrite: true);
        return dll;
    }

    /// <summary>Installs BepInEx's own assemblies into a BepInEx folder's core (BepInEx and Harmony), as its pack does.</summary>
    /// <param name="bepInEx">The BepInEx folder (made when missing).</param>
    public static void InstallCore(string bepInEx)
    {
        string core = Path.Combine(bepInEx, "core");
        Directory.CreateDirectory(core);
        foreach (string dll in new[] { "BepInEx.dll", "0Harmony.dll" })
        {
            File.Copy(Path.Combine(BepInExCore, dll), Path.Combine(core, dll), overwrite: true);
        }
    }

    /// <summary>
    /// Deletes the downloaded mods when the test run ends (not when the tests are only listed); CI keeps them through
    /// its several test processes (VWE_KEEP_TEST_MODS) and deletes them after the last.
    /// </summary>
    public sealed class Cleanup : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (Environment.GetEnvironmentVariable("VWE_KEEP_TEST_MODS") != "1" && Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
        }
    }
}
