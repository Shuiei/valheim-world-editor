using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector and a mod's manifest asking for a chest's contents ("items") to be shown only: the contents' controls
/// are off, and a change made to the item rows all the same is not among the changes applied. Runs alone (the
/// manifests are shared).
/// </summary>
[Collection("DataDir")]
public sealed class InspectorPanelManifestTests : IDisposable
{
    private static readonly int Chest = StableHash.Of("piece_chest_wood");
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-inspectmanifest-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _world = Fixtures.CopyFixture();

    public InspectorPanelManifestTests()
    {
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>Forgets the manifests, puts the default places' search back, and deletes the folders.</summary>
    public void Dispose()
    {
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        foreach (string dir in (string[])[_dir, Path.GetDirectoryName(_world)!])
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    [AvaloniaFact]
    public void ReadOnlyContentsAreShownOnlyAndTheirChangesNotApplied()
    {
        var world = WorldSave.Load(_world);
        var at = world.Objects.First(o => o.Prefab == Chest).Position;
        var scene = WorldScene.Load(_world, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        w.View.Show(scene, null);
        w.Edit(scene.Session!);
        // After the world opened: opening one reads the manifests again, from the app's own settings.
        string folder = Path.Combine(_dir, "BepInEx", "plugins", "Mod");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModManifests.FileName), """{ "version": 1, "data": { "items": { "readOnly": true } } }""");
        ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] });
        var ins = w.Inspector;
        Assert.True(ins.Open(scene.Things.FindIndex(t => t.Prefab == Chest)));
        Assert.NotNull(ins.Items);

        Assert.False(ins.ItemsBox.IsEnabled);
        var (x, y) = ins.FreeSlot()!.Value;
        ins.Items!.Add(new InspectorPanel.ItemRow { Name = "Resin", Stack = 7, X = x, Y = y });
        Assert.Null(ins.Changes().Inventory);

        // The manifest gone: the contents are the user's to change again.
        ModManifests.Forget();
        Assert.True(ins.Open(scene.Things.FindIndex(t => t.Prefab == Chest)));
        Assert.True(ins.ItemsBox.IsEnabled);
    }
}
