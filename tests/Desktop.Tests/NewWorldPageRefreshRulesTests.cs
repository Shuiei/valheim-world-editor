using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page reads Expand World's rules again once the game's own location rules are read (they are read in
/// the background, often after the page opens), so the location file converts; rules given to the page stay as given.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageRefreshRulesTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-refresh-" + Guid.NewGuid().ToString("N")[..8]);

    public NewWorldPageRefreshRulesTests()
    {
        AppSettings.DataDirOverride = _dir;
    }

    /// <summary>Puts the data folder and the game's location rules back, and deletes the folder.</summary>
    public void Dispose()
    {
        ValheimGen.SeedLocations.Use(new ValheimGen.SeedLocations.RuleSet());
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>A BepInEx folder with Expand World Data's settings, the library and a location file, named in the saved settings.</summary>
    private string Bep()
    {
        string bep = Path.Combine(_dir, "bep", "BepInEx");
        Directory.CreateDirectory(Path.Combine(bep, "config", "expand_world"));
        string package = Path.Combine(bep, "plugins", "ValheimModding-YamlDotNet");
        Directory.CreateDirectory(package);
        File.Copy(typeof(YamlDotNet.Serialization.Deserializer).Assembly.Location, Path.Combine(package, YamlReader.DllName));
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_data.cfg"), "[3. Data]\nLocation data = true\n");
        File.WriteAllText(Path.Combine(bep, "config", "expand_world", "expand_locations.yaml"), "- prefab: StartTemple:Twice\n  biome: Meadows\n  quantity: 2\n");
        var settings = AppSettings.Load();
        settings.ExpandWorld = true;
        settings.ExpandWorldFolder = bep;
        Assert.Null(settings.Save());
        return bep;
    }

    [AvaloniaFact]
    public void TheLocationFileConvertsOnceTheGamesRulesAreRead()
    {
        Bep();
        ValheimGen.SeedLocations.Use(new ValheimGen.SeedLocations.RuleSet());
        using var page = new NewWorldPage([], previewSize: 16);
        // Opened before the game's rules: the location file waits.
        Assert.True(page.Rules.DataInstalled);
        Assert.Null(page.Rules.Locations);
        Assert.DoesNotContain("locations", page.Generation.Text);
        page.RefreshRules();
        Assert.Null(page.Rules.Locations);
        // The game's rules read: the file converts, and the hint says so.
        Fixtures.UseLocations();
        page.RefreshRules();
        Assert.NotNull(page.Rules.Locations);
        Assert.Contains(page.Rules.Locations.Locations, l => l.Prefab == "StartTemple:Twice");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Contains("locations (", page.Generation.Text);
    }

    [AvaloniaFact]
    public void RulesGivenToThePageStayAsGiven()
    {
        Bep();
        Fixtures.UseLocations();
        using var page = new NewWorldPage([], previewSize: 16, rules: new ValheimGen.WorldRules { DataInstalled = true });
        page.RefreshRules();
        Assert.Null(page.Rules.Locations);
    }
}
