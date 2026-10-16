using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The objects' models read for the 3D view on worker threads (GlView.Load).
/// </summary>
public class GlViewModelsTests
{
    /// <summary>
    /// A model of one part whose texture cannot be read for want of memory (the one failure the store does not turn into none).
    /// </summary>
    private sealed class TextureTooBig : ModelStore.ISource
    {
        public string? ObjectName(int prefab) => "tree";

        public ModelStore.Model? Model(string name) => new(name, [new ModelStore.Part("m", 0, "bark", Matrix4x4.Identity)], Vector3.One);

        public ModelStore.MeshData? Mesh(string id) => new([0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0], [[0, 1, 2]]);

        public ModelStore.MaterialData Material(string id) => new(Vector4.One, "bark.png", 0, false, new Vector4(1, 1, 0, 0));

        public ModelStore.ImageData? Texture(string id) => throw new InsufficientMemoryException("no memory left for the texture");
    }

    [AvaloniaFact]
    public async Task AModelThatFailsToLoadGetsTheStandInBox()
    {
        var v = new GlView();
        var w = new Window { Width = 400, Height = 300, Content = v };
        w.Show();
        var said = new List<string>();
        v.Status += said.Add;
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false));
        v.Show(s.Scene, new ModelStore(new TextureTooBig()));
        // Read on a worker thread.
        for (int i = 0; i < 200 && v.ModelsWaiting.Length == 0; i++)
        {
            await Task.Delay(50);
        }
        // Waiting for the drawing thread like any model, with the stand-in box in place of the model; the failure said.
        var (prefab, hasModel) = Assert.Single(v.ModelsWaiting);
        Assert.Equal(StableHash.Of("Beech1"), prefab);
        Assert.False(hasModel);
        Assert.Contains(said, t => t.Contains("no memory left for the texture"));
    }
}
