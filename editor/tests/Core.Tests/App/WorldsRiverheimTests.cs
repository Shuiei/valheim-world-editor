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
        // Riverheim's generator reads the settings saved.
        var settings = new AppSettings { WorldFolders = { w.Dir } };
        Assert.Null(settings.Save());
        var off = Assert.Single(Worlds.Find(settings));
        Assert.Equal(("Riverheim", false, Lang.T("riverheim.off")), (off.Generator, off.Usable, off.Problem));
        // On, with Riverheim found.
        string plugins = Path.Combine(Data, "bep", "plugins");
        Directory.CreateDirectory(plugins);
        File.WriteAllBytes(Path.Combine(plugins, Riverheim.DllName), []);
        settings.Riverheim = true;
        settings.ExpandWorldFolder = Path.Combine(Data, "bep");
        Assert.Null(settings.Save());
        var on = Assert.Single(Worlds.Find(settings));
        Assert.Equal(("Riverheim", true), (on.Generator, on.Usable));
        Assert.Null(on.Problem);
        // A generator the editor cannot read: not usable, and why.
        WorldFileHead.Write(head, 42, [], WorldFileHead.RiverheimPreset, Riverheim.BetaPluginId);
        var beta = Assert.Single(Worlds.Find(settings));
        Assert.Null(beta.Generator);
        Assert.Equal((false, Lang.T("riverheim.beta")), (beta.Usable, beta.Problem));
    }
}
