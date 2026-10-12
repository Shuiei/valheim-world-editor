using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A model source that fails on everything (a damaged bundle): the store gives no model, mesh or texture and a plain
/// white material instead of failing, asks once per model, and names objects from the catalogue.
/// </summary>
public class ModelStoreFailuresTests
{
    /// <summary>
    /// Throws for every model, mesh, material and texture; counts the models asked.
    /// </summary>
    private sealed class Broken : ModelStore.ISource
    {
        public int ModelsAsked;

        public string? ObjectName(int prefab) => null;

        public ModelStore.Model? Model(string name)
        {
            ModelsAsked++;
            throw new InvalidDataException("damaged");
        }

        public ModelStore.MeshData? Mesh(string id) => throw new IOException("damaged");

        public ModelStore.MaterialData Material(string id) => throw new InvalidDataException("damaged");

        public ModelStore.ImageData? Texture(string id) => throw new EndOfStreamException("damaged");
    }

    [Fact]
    public void WhatCannotBeReadIsNothingAndAMaterialIsPlainWhite()
    {
        var source = new Broken();
        var store = new ModelStore(source);
        Assert.Null(store.LoadModel("woodwall"));
        // Asked once: what failed is kept as none.
        Assert.Null(store.LoadModel("woodwall"));
        Assert.Equal(1, source.ModelsAsked);
        Assert.Null(store.LoadMesh("m"));
        Assert.Null(store.BoundsOf("m"));
        Assert.Null(store.LoadTexture("t.png"));
        var white = store.Material("mat");
        Assert.Equal(Vector4.One, white.Color);
        Assert.Null(white.Map);
        Assert.Equal(0, white.Cutoff);
        Assert.False(white.DoubleSided);
        Assert.Equal(new Vector4(1, 1, 0, 0), white.UvTransform);
        // No model name of its own: the catalogue's name for the prefab.
        Assert.Equal(Terrain.PrefabCatalog.DisplayName(Save.StableHash.Of("Beech1")), store.NameOf(Save.StableHash.Of("Beech1")));
    }
}
