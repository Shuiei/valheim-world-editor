using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor.Tests;

/// <summary>An open area holding the objects given (IAreaView), over flat ground at 0 in the Meadows.</summary>
internal sealed class ObjectsArea(params IObjectView[] objects) : IAreaView
{
    public float MinX => -1000;

    public float MinZ => -1000;

    public float MaxX => 1000;

    public float MaxZ => 1000;

    public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => [.. objects.Where(o => o.Prefab == prefab)];

    public float GroundHeight(float x, float z) => 0;

    public int Biome(float x, float z) => 1;

    public bool IsDeepNorth(float x, float z) => false;

    public bool IsAshlands(float x, float z) => false;

    public uint BiomeColor(int x, int z) => 0;

    public uint GroundMask(int x, int z) => 0;
}
