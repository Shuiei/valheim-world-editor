using System.Reflection;

[assembly: Xunit.AssemblyFixture(typeof(WorldEditor.Tests.TestMods.Cleanup))]

namespace WorldEditor.Tests;

/// <summary>
/// The Expand World mods a test installs into its BepInEx folder: the real packages, downloaded from Thunderstore when
/// the test project builds (tests/TestMods.targets) into the system's temporary folder, deleted when the test run ends.
/// </summary>
public static class TestMods
{
    /// <summary>A mod the tests install: its package.</summary>
    /// <param name="Name">Its Thunderstore package (the folder a mod manager puts it in).</param>
    /// <param name="Dll">Its plugin's file.</param>
    public sealed record Package(string Name, string Dll);

    /// <summary>Expand World Size.</summary>
    public static readonly Package Size = new("JereKuusela-Expand_World_Size", "ExpandWorldSize.dll");

    /// <summary>Expand World Data.</summary>
    public static readonly Package Data = new("JereKuusela-Expand_World_Data", "ExpandWorldData.dll");

    /// <summary>Expand World Rivers.</summary>
    public static readonly Package Rivers = new("JereKuusela-Expand_World_Rivers", "ExpandWorldRivers.dll");

    /// <summary>Where the build put the downloaded mods.</summary>
    public static string Folder { get; } = typeof(TestMods).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "TestMods").Value!;

    /// <summary>Installs a mod into a BepInEx folder as a mod manager does (its package's folder under plugins), and gives its plugin's path.</summary>
    /// <param name="bepInEx">The BepInEx folder (made when missing).</param>
    /// <param name="mod">The mod.</param>
    public static string Install(string bepInEx, Package mod)
    {
        string from = Path.Combine(Folder, mod.Name, mod.Dll);
        if (!File.Exists(from))
        {
            throw new FileNotFoundException($"{mod.Dll} was not downloaded: build the test project again (tests/TestMods.targets)", from);
        }
        string package = Path.Combine(bepInEx, "plugins", mod.Name);
        Directory.CreateDirectory(package);
        string dll = Path.Combine(package, mod.Dll);
        File.Copy(from, dll, overwrite: true);
        return dll;
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
