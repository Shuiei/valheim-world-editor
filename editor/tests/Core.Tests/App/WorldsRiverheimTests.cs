using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The worlds list's view of worlds made with Riverheim: tagged, not usable while Riverheim cannot draw them (with why),
/// usable once it is on and found; a world of a generator the editor cannot read is not usable either.
/// </summary>
[Collection("DataDir")]
public class WorldsRiverheimTests : AppSettingsTestBase
{
    [Fact]
    public void AWorldMadeWithRiverheimIsTaggedAndSaysWhyItCannotOpen()
    {
        using var w = new TempWorld();
        int save = w.Load().SaveNumber;
        string head = Path.Combine(w.Dir, $"_main.{save}.fwl2");
        WorldFileHead.Write(head, 42, [], WorldFileHead.RiverheimPreset);
        var off = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { w.Dir } }));
        Assert.Equal((true, false, Lang.T("riverheim.off")), (off.Riverheim, off.Usable, off.Problem));
        // On, with Riverheim found.
        string plugins = Path.Combine(Data, "bep", "plugins");
        Directory.CreateDirectory(plugins);
        File.WriteAllBytes(Path.Combine(plugins, Riverheim.DllName), []);
        var on = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { w.Dir }, Riverheim = true, ExpandWorldFolder = Path.Combine(Data, "bep") }));
        Assert.Equal((true, true), (on.Riverheim, on.Usable));
        Assert.Null(on.Problem);
        // A generator the editor cannot read: not usable, and why.
        WorldFileHead.Write(head, 42, [], WorldFileHead.RiverheimPreset, Riverheim.BetaPluginId);
        var beta = Assert.Single(Worlds.Find(new AppSettings { WorldFolders = { w.Dir } }));
        Assert.Equal((false, false, Lang.T("riverheim.beta")), (beta.Riverheim, beta.Usable, beta.Problem));
    }
}
