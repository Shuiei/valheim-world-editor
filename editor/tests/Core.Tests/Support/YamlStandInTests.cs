using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The tests' stand-in for the YamlDotNet library a BepInEx folder has (YamlStandIn).</summary>
public class YamlStandInTests
{
    [Fact]
    public void TheLibraryIsPutWhereThePackagePutsIt()
    {
        string bep = Path.Combine(Path.GetTempPath(), "vwe-yaml-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            string dll = YamlStandIn.Install(bep);
            Assert.Equal(Path.Combine(bep, "plugins", YamlStandIn.Package, YamlReader.DllName), dll);
            Assert.True(File.Exists(dll));
            Assert.Equal(dll, YamlReader.FindDll(Path.Combine(bep, "plugins")));
            // Again: the copy is made afresh, not refused.
            Assert.Equal(dll, YamlStandIn.Install(bep));
        }
        finally
        {
            if (Directory.Exists(bep))
            {
                Directory.Delete(bep, true);
            }
        }
    }
}
