using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Mods' editor extensions (ModExtensions): offered by a worldeditor.json ("extension", inside the mod's folder only),
/// off until allowed, run once allowed (the SDK sample's DLL, loaded as a mod's would be), off again when the file
/// changes; a DLL that is no extension, or one needing a newer editor, not run, with why; a hook that throws turns its
/// extension off without failing the editor. Runs alone (the manifests and extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ModExtensionsTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-extensions-" + Guid.NewGuid().ToString("N")[..8]);

    public ModExtensionsTests()
    {
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>Forgets the manifests and extensions, puts the default places' search back, and deletes the folder.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Settings naming the test's BepInEx folder (and nothing allowed).</summary>
    private AppSettings Settings { get; } = new();

    /// <summary>A mod folder under plugins with a manifest naming its extension, and the DLL copied in (given).</summary>
    private string Mod(string mod, string extension, string? dllFrom)
    {
        Settings.BepInExFolders = [Path.Combine(_dir, "BepInEx")];
        string folder = Path.Combine(_dir, "BepInEx", "plugins", mod);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModManifests.FileName), $$"""{ "version": 1, "extension": "{{extension}}" }""");
        string dll = Path.GetFullPath(Path.Combine(folder, extension));
        if (dllFrom != null)
        {
            File.Copy(dllFrom, dll, true);
        }
        return dll;
    }

    /// <summary>The sample extension's DLL, built with the tests.</summary>
    private static string Sample => Path.Combine(AppContext.BaseDirectory, "ExampleVehicles.WorldEditor.dll");

    /// <summary>Reads the manifests, then runs the extensions.</summary>
    private int Refresh()
    {
        ModManifests.Refresh(Settings);
        return ModExtensions.Refresh(Settings);
    }

    [Fact]
    public void AnOfferedExtensionRunsOnlyOnceAllowedAndUntilItsFileChanges()
    {
        string dll = Mod("ExampleVehicles", "ExampleVehicles.WorldEditor.dll", Sample);

        Assert.Equal(0, Refresh());
        var offered = Assert.Single(ModExtensions.Offered);
        Assert.Equal((dll, "ExampleVehicles", ModExtensions.Status.Off), (offered.Dll, offered.Mod, offered.State));
        Assert.Empty(ModExtensions.RewrittenKeys);

        ModExtensions.Allow(Settings, offered, true);
        Assert.Equal(1, Refresh());
        Assert.Equal(ModExtensions.Status.Running, Assert.Single(ModExtensions.Offered).State);
        Assert.Contains(TerrainEditor.Save.StableHash.Of(ExampleMod.PackedKey), ModExtensions.RewrittenKeys);
        Assert.True(ModExtensions.HasShapes(TerrainEditor.Save.StableHash.Of("example_sail")));
        Assert.True(ModExtensions.HasCuts(TerrainEditor.Save.StableHash.Of("example_pit")));

        // Another file under the same name (a mod update): other code, off until allowed again.
        File.AppendAllText(dll, " ");
        Assert.Equal(0, Refresh());
        Assert.Equal(ModExtensions.Status.Off, Assert.Single(ModExtensions.Offered).State);
        Assert.False(ModExtensions.HasShapes(TerrainEditor.Save.StableHash.Of("example_sail")));
    }

    [Fact]
    public void OneFileOfferedByTwoModsRunsItsHooksOnce()
    {
        Mod("ExampleVehicles", "ExampleVehicles.WorldEditor.dll", Sample);
        Mod("ExampleVehiclesCopy", "ExampleVehicles.WorldEditor.dll", Sample);
        Refresh();
        foreach (var x in ModExtensions.Offered)
        {
            ModExtensions.Allow(Settings, x, true);
        }

        Assert.Equal(2, Refresh());

        Assert.All(ModExtensions.Offered, x => Assert.Equal(ModExtensions.Status.Running, x.State));
        var view = new ObjectView("", default, default, new TerrainEditor.Save.ZdoData());
        Assert.Single(ModExtensions.CutsOf(TerrainEditor.Save.StableHash.Of("example_pit"), view, ModExtensions.NoWorld));
    }

    [Fact]
    public void ADllThatIsNoExtensionDoesNotRunAndSaysWhy()
    {
        // A .NET assembly not marked as an extension (the SDK itself), and a file that is no assembly.
        Mod("NotMarked", "Plain.dll", typeof(IWorldEditorExtension).Assembly.Location);
        string junk = Mod("Junk", "Junk.dll", null);
        File.WriteAllText(junk, "not a DLL");
        Refresh();
        foreach (var x in ModExtensions.Offered)
        {
            ModExtensions.Allow(Settings, x, true);
        }

        Assert.Equal(0, Refresh());

        Assert.All(ModExtensions.Offered, x => Assert.Equal(ModExtensions.Status.Failed, x.State));
        Assert.Contains("not marked as an extension", ModExtensions.Offered.Single(x => x.Mod == "NotMarked").Error);
        Assert.NotNull(ModExtensions.Offered.Single(x => x.Mod == "Junk").Error);
    }

    [Theory]
    [InlineData("../Elsewhere.dll")]
    [InlineData("Notes.txt")]
    public void AnExtensionOutsideItsModsFolderOrNoDllIsNotOffered(string extension)
    {
        Mod("Mod", extension, null);

        Refresh();

        Assert.Empty(ModExtensions.Offered);
    }

    /// <summary>An extension whose decoder throws.</summary>
    private sealed class Throwing : IWorldEditorExtension, IDataDecoder
    {
        public void Register(IExtensionRegistry registry) => registry.AddDecoder("k", this);

        public string? Describe(IObjectView obj, byte[] value) => throw new InvalidOperationException("broken");
    }

    [Fact]
    public void AHookThatThrowsTurnsItsExtensionOff()
    {
        ModExtensions.UseForTests(("Broken", new Throwing()));
        int before = ModExtensions.Generation;
        var view = new ObjectView("", default, default, new TerrainEditor.Save.ZdoData());

        Assert.Null(ModExtensions.Decode(TerrainEditor.Save.StableHash.Of("k"), view, [1]));

        var x = Assert.Single(ModExtensions.Offered);
        Assert.Equal(ModExtensions.Status.Failed, x.State);
        Assert.Contains("broken", x.Error);
        Assert.NotEqual(before, ModExtensions.Generation);
        Assert.Null(ModExtensions.Decode(TerrainEditor.Save.StableHash.Of("k"), view, [1]));
    }

    /// <summary>A game with one mod and an echoing route.</summary>
    private sealed class FakeLive : ILiveGame
    {
        public IReadOnlyList<string> Mods => ["ExampleVehicles"];

        public Task<byte[]> CallAsync(string modId, string name, byte[] body, CancellationToken cancel = default) => Task.FromResult<byte[]>([.. body]);
    }

    /// <summary>An extension keeping what its live listener was told.</summary>
    private sealed class Watching : IWorldEditorExtension, ILiveListener
    {
        public List<string> Told { get; } = [];

        public IExtensionRegistry? Registry { get; private set; }

        public void Register(IExtensionRegistry registry)
        {
            Registry = registry;
            registry.AddLiveListener(this);
        }

        public void OnConnected(ILiveGame live) => Told.Add("connected " + string.Join(",", live.Mods));

        public void OnDisconnected() => Told.Add("disconnected");
    }

    // The editor connected to a game: the listeners told, the host's game set (and gone again), a redraw asked for
    // counted in the generation. A game opened over another ends the first.
    [Fact]
    public void LiveListenersAreToldOfTheGameAndTheHostGivesIt()
    {
        var w = new Watching();
        ModExtensions.UseForTests(("Watcher", w));
        Assert.Null(w.Registry!.Host.Live);

        ModExtensions.LiveConnected(new FakeLive());
        Assert.Equal(["connected ExampleVehicles"], w.Told);
        Assert.Same(ModExtensions.Host.Live, w.Registry.Host.Live);
        Assert.Equal("ExampleVehicles", Assert.Single(w.Registry.Host.Live!.Mods));

        int before = ModExtensions.Generation;
        w.Registry.Host.Redraw();
        Assert.NotEqual(before, ModExtensions.Generation);

        ModExtensions.LiveConnected(new FakeLive());
        ModExtensions.LiveDisconnected();
        ModExtensions.LiveDisconnected();
        Assert.Equal(["connected ExampleVehicles", "disconnected", "connected ExampleVehicles", "disconnected"], w.Told);
        Assert.Null(w.Registry.Host.Live);
    }
}
