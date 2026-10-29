using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// An area of flat ground, the same everywhere (IAreaView): what a mod's tool reads while no area is open, and a stand-in
/// for an open one where only its objects matter.
/// </summary>
/// <param name="minX">Its west edge, world x in metres.</param>
/// <param name="minZ">Its south edge, world z in metres.</param>
/// <param name="maxX">Its east edge, world x in metres.</param>
/// <param name="maxZ">Its north edge, world z in metres.</param>
/// <param name="height">The ground's height everywhere, in metres.</param>
public sealed class PlainArea(float minX, float minZ, float maxX, float maxZ, float height) : IAreaView
{
    /// <summary>Its objects, by prefab name; none of a prefab not listed.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IObjectView>> Objects { get; init; } = new Dictionary<string, IReadOnlyList<IObjectView>>();

    /// <summary>The biome everywhere (the game's Heightmap.Biome value; 0 none).</summary>
    public int BiomeValue { get; init; }

    /// <summary>The terrain colour everywhere, red in the lowest byte.</summary>
    public uint Colour { get; init; }

    /// <inheritdoc/>
    public float MinX => minX;

    /// <inheritdoc/>
    public float MinZ => minZ;

    /// <inheritdoc/>
    public float MaxX => maxX;

    /// <inheritdoc/>
    public float MaxZ => maxZ;

    /// <inheritdoc/>
    public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => Objects.TryGetValue(prefab, out var found) ? found : [];

    /// <inheritdoc/>
    public float GroundHeight(float x, float z) => height;

    /// <inheritdoc/>
    public int Biome(float x, float z) => BiomeValue;

    /// <inheritdoc/>
    public bool IsDeepNorth(float x, float z) => false;

    /// <inheritdoc/>
    public bool IsAshlands(float x, float z) => false;

    /// <inheritdoc/>
    public uint BiomeColor(int x, int z) => Colour;

    /// <inheritdoc/>
    public uint GroundMask(int x, int z) => 0xFF000000;
}
