using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page asks the world rules providers again once the game's own location rules are read (they are read
/// in the background, often after the page opens), so a location file converts; rules given to the page stay as given.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageRefreshRulesTests : IDisposable
{
    /// <summary>Forgets the extensions, and puts the game's location rules back.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        SeedLocations.Use(new LocationRuleSet());
    }

    /// <summary>A provider whose location file converts only once the game's location rules are read, as Expand World Data's.</summary>
    private sealed class Located : IWorldEditorExtension, IWorldRulesProvider
    {
        public void Register(IExtensionRegistry registry) => registry.AddWorldRules(this);

        public Task<WorldRules?> ForWorld(WorldRulesRequest world) => ForNewWorld();

        public Task<WorldRules?> ForNewWorld() => Task.FromResult<WorldRules?>(new WorldRules
        {
            DataInstalled = true,
            Locations = SeedLocations.Available ? new LocationRuleSet { Locations = [new LocationRule { Prefab = "StartTemple:Twice" }] } : null,
        });
    }

    [AvaloniaFact]
    public void TheLocationFileConvertsOnceTheGamesRulesAreRead()
    {
        ModExtensions.UseForTests(("Located", new Located()));
        SeedLocations.Use(new LocationRuleSet());
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
        ModExtensions.UseForTests(("Located", new Located()));
        Fixtures.UseLocations();
        using var page = new NewWorldPage([], previewSize: 16, rules: new WorldRules { DataInstalled = true });
        page.RefreshRules();
        Assert.Null(page.Rules.Locations);
    }
}
