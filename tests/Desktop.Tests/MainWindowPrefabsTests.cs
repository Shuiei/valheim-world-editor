using System.Numerics;
using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The model boxes the window gives the Place tool, from the game's models: a kind's parts put together in Unity's axes
/// and scaled like the model, worked out once; none for a kind without a model or whose meshes cannot be read.
/// </summary>
public sealed class MainWindowPrefabsTests
{
    /// <summary>
    /// Models made in code: Crate, a box from (0, 0, 0) to (1, 2, 3) moved 1 m along x plus a part whose mesh is
    /// missing, its root scaled 2 along x; Hollow, only the missing mesh.
    /// </summary>
    private sealed class Crates : ModelStore.ISource
    {
        public string? ObjectName(int prefab) => null;

        public ModelStore.Model? Model(string name)
        {
            var missing = new ModelStore.Part("missing", 0, "wood", Matrix4x4.Identity);
            return name switch
            {
                "Crate" => new(name, [new ModelStore.Part("box", 0, "wood", Matrix4x4.CreateTranslation(1, 0, 0)), missing], new Vector3(2, 1, 1)),
                "Hollow" => new(name, [missing], Vector3.One),
                _ => null,
            };
        }

        public ModelStore.MeshData? Mesh(string id) => id == "box" ? new([0, 0, 0, 0, 1, 0, 0, 0, 1, 2, 3, 0, 1, 0, 0, 0], [[0, 1, 0]]) : null;

        public ModelStore.MaterialData Material(string id) => new(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0), false);

        public ModelStore.ImageData? Texture(string id) => null;
    }

    [AvaloniaFact]
    public void AKindsBoxComesFromItsModelOnce()
    {
        var w = new MainWindow(load: false);
        PrivateMember.Set(w, "_models", new ModelStore(new Crates()));

        // The box's x doubled and moved, its z mirrored; the part without a mesh adds nothing.
        Assert.Equal((new Vector3(2, 0, -3), new Vector3(4, 2, 0)), w.PlaceTool.ModelBox("Crate"));
        Assert.Null(w.PlaceTool.ModelBox("Hollow"));
        Assert.Null(w.PlaceTool.ModelBox("Unknown"));
        // Kept by the window: known still once the models are let go.
        PrivateMember.Set(w, "_models", null);
        Assert.Equal((new Vector3(2, 0, -3), new Vector3(4, 2, 0)), w.PlaceTool.ModelBox("Crate"));
        Assert.Null(w.PlaceTool.ModelBox("Hollow"));
    }
}
