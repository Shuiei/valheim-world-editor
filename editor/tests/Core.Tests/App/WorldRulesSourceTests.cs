using TerrainEditor.App;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The rules worlds are generated with (WorldRulesSource, ModExtensions.FirstRules): a running extension's world rules
/// provider is asked first, with the world as the editor read it; one giving the game's own, or nothing, leaves the next
/// to answer; one that throws turns its extension off. Runs alone (the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class WorldRulesSourceTests : IDisposable
{
    /// <summary>Forgets the extensions.</summary>
    public void Dispose() => ModExtensions.Forget();

    /// <summary>An extension whose provider gives these rules (or throws), keeping the worlds it was asked for.</summary>
    private sealed class Rules(Func<WorldRules?> give) : IWorldEditorExtension, IWorldRulesProvider
    {
        public List<WorldRulesRequest> Asked { get; } = [];

        public void Register(IExtensionRegistry registry) => registry.AddWorldRules(this);

        public Task<WorldRules?> ForWorld(WorldRulesRequest world)
        {
            Asked.Add(world);
            return Task.FromResult(give());
        }

        public Task<WorldRules?> ForNewWorld() => Task.FromResult(give());
    }

    [Fact]
    public async Task AnExtensionsRulesAreUsedWithTheWorldAsRead()
    {
        var mod = new Rules(() => new WorldRules { Radius = 30000f });
        ModExtensions.UseForTests(("Sized", mod));
        using var w = new TempWorld();
        var save = w.Load();
        save.StartingGlobalKeys.Add("mod_size 3");

        var rules = await WorldRulesSource.ForWorld(save, null);

        Assert.Equal(30000f, rules.Radius);
        var asked = Assert.Single(mod.Asked);
        Assert.Equal(save.Name, asked.Name);
        Assert.Contains("mod_size 3", asked.StartingGlobalKeys);
        Assert.Equal(save.GridWidth, asked.GridWidth);
        Assert.Null(asked.Live);
        Assert.Equal(30000f, WorldRulesSource.ForNewWorld().Radius);
    }

    [Fact]
    public async Task TheGamesOwnRulesOrNoneLeaveTheNextProvider()
    {
        ModExtensions.UseForTests(("Plain", new Rules(() => WorldRules.Vanilla)), ("Nothing", new Rules(() => null)), ("Sized", new Rules(() => new WorldRules { Radius = 12000f })));
        using var w = new TempWorld();

        Assert.Equal(12000f, (await WorldRulesSource.ForWorld(w.Load(), null)).Radius);
        ModExtensions.UseForTests(("Plain", new Rules(() => WorldRules.Vanilla)));
        // None changes the world: the game's own.
        Assert.True((await WorldRulesSource.ForWorld(w.Load(), null)).IsVanilla);
    }

    [Fact]
    public async Task AProviderThatThrowsTurnsItsExtensionOff()
    {
        var broken = new Rules(() => throw new InvalidOperationException("no rules"));
        ModExtensions.UseForTests(("Broken", broken), ("Sized", new Rules(() => new WorldRules { Radius = 15000f })));
        using var w = new TempWorld();

        Assert.Equal(15000f, (await WorldRulesSource.ForWorld(w.Load(), null)).Radius);

        var x = Assert.Single(ModExtensions.Offered, o => o.Mod == "Broken");
        Assert.Equal(ModExtensions.Status.Failed, x.State);
        Assert.Contains("no rules", x.Error);
        Assert.Equal(15000f, WorldRulesSource.ForNewWorld().Radius);
        Assert.Single(broken.Asked);
    }
}
