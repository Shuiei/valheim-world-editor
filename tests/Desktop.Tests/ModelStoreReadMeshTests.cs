using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Reading one mesh: its winding is turned for the right-handed view and its box is kept; a mesh that is not there is none.
/// </summary>
public class ModelStoreReadMeshTests
{
    /// <summary>A source that has one mesh, or none, and no models or materials.</summary>
    private sealed class OneMesh(ModelStore.MeshData? mesh) : ModelStore.ISource
    {
        public string? ObjectName(int prefab) => null;

        public ModelStore.Model? Model(string name) => null;

        public ModelStore.MeshData? Mesh(string id) => mesh;

        public ModelStore.MaterialData Material(string id) => throw new NotSupportedException();

        public ModelStore.ImageData? Texture(string id) => null;
    }

    /// <summary>A triangle of three vertices of eight floats each (position, normal, uv), wound 0, 1, 2.</summary>
    private static ModelStore.MeshData Triangle()
    {
        float[] v =
        [
            0, 0, 0, 0, 1, 0, 0, 0,
            1, 0, 0, 0, 1, 0, 0, 0,
            0, 2, 0, 0, 1, 0, 0, 0,
        ];
        return new ModelStore.MeshData(v, [[0u, 1u, 2u]]);
    }

    [Fact]
    public void ReadingAMeshTurnsItsWindingAndKeepsItsBox()
    {
        var store = new ModelStore(new OneMesh(Triangle()));
        var mesh = store.ReadMesh("m");
        Assert.NotNull(mesh);
        Assert.Equal(new uint[] { 0, 2, 1 }, mesh!.Submeshes[0]);
        var box = store.BoundsOf("m");
        Assert.NotNull(box);
        Assert.Equal(Vector3.Zero, box!.Value.Min);
        Assert.Equal(new Vector3(1, 2, 0), box.Value.Max);
    }

    [Fact]
    public void AMeshThatIsNotThereIsNoneAndKeepsNoBox()
    {
        var store = new ModelStore(new OneMesh(null));
        Assert.Null(store.ReadMesh("m"));
        Assert.Null(store.BoundsOf("m"));
    }
}
