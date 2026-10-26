using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The area open in the editor's 3D view, as a scene provider reads it (<see cref="ISceneProvider.Build"/>): its
/// bounds, its objects (with the changes not saved yet), and its ground as the game shows it.
/// </summary>
public interface IAreaView
{
    /// <summary>The area's west edge, world x in metres.</summary>
    float MinX { get; }

    /// <summary>The area's south edge, world z in metres.</summary>
    float MinZ { get; }

    /// <summary>The area's east edge, world x in metres.</summary>
    float MaxX { get; }

    /// <summary>The area's north edge, world z in metres.</summary>
    float MaxZ { get; }

    /// <summary>
    /// The objects of a prefab in the area, as they are with the changes not saved yet (deleted ones left out, added
    /// ones in), objects that only hold a mod's data (a manifest's "data") included; in no particular order.
    /// </summary>
    /// <param name="prefab">The prefab's name, as the mod's code names it.</param>
    IReadOnlyList<IObjectView> ObjectsOf(string prefab);

    /// <summary>
    /// The ground's height at a point as the game shows it (its heightmap with the edits, between the points a metre
    /// apart along the game's own triangles); the nearest edge's height outside the area.
    /// </summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    float GroundHeight(float x, float z);

    /// <summary>The biome at a point, as the game's Heightmap.Biome value (1 Meadows, 2 Swamp, 4 Mountain...).</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    int Biome(float x, float z);

    /// <summary>Whether a point lies in the Deep North, as the game's WorldGenerator.IsDeepnorth says.</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    bool IsDeepNorth(float x, float z);

    /// <summary>Whether a point lies in the Ashlands, as the game's WorldGenerator.IsAshlands says.</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    bool IsAshlands(float x, float z);

    /// <summary>
    /// The colour the game's terrain mesh has at a ground point a whole metre (the biomes it blends, as
    /// Heightmap.GetBiomeColor gives them), red in the lowest byte: what <see cref="SceneMesh.BiomeColors"/> takes.
    /// </summary>
    /// <param name="x">World x, a whole metre.</param>
    /// <param name="z">World z, a whole metre.</param>
    uint BiomeColor(int x, int z);

    /// <summary>
    /// The game's paint mask at a ground point a whole metre (the terrain's _ClearedMaskTex: red dirt, green
    /// cultivated, blue paved, alpha vegetation), red in the lowest byte.
    /// </summary>
    /// <param name="x">World x, a whole metre.</param>
    /// <param name="z">World z, a whole metre.</param>
    uint GroundMask(int x, int z);
}
