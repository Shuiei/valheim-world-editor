using System.Collections.Generic;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>What a scene provider draws in an area: meshes, and holes in the editor's ground.</summary>
public sealed class SceneLayer
{
    /// <summary>The meshes.</summary>
    public IList<SceneMesh> Meshes { get; } = [];

    /// <summary>Where the editor's ground is not drawn (and clicks go through it): what the meshes stand in for.</summary>
    public IList<GroundHole> Holes { get; } = [];
}

/// <summary>
/// A mesh drawn in the area, in the world's frame (metres; x east, y up, z north), triangles wound clockwise seen from
/// the front as in Unity. Drawn with the game's ground look when it has <see cref="BiomeColors"/>, else in
/// <see cref="Color"/>.
/// </summary>
public sealed class SceneMesh
{
    /// <summary>A mesh.</summary>
    /// <param name="positions">The corners, in world metres.</param>
    /// <param name="normals">Each corner's normal (unit length, towards the front).</param>
    /// <param name="triangles">Three corner indices a triangle.</param>
    public SceneMesh(Vector3[] positions, Vector3[] normals, int[] triangles)
    {
        Positions = positions;
        Normals = normals;
        Triangles = triangles;
    }

    /// <summary>The corners, in world metres.</summary>
    public Vector3[] Positions { get; }

    /// <summary>Each corner's normal (unit length, towards the front).</summary>
    public Vector3[] Normals { get; }

    /// <summary>Three corner indices a triangle, wound clockwise seen from the front.</summary>
    public int[] Triangles { get; }

    /// <summary>The colour drawn without the ground look (red, green, blue, alpha from 0 to 1; alpha is not used).</summary>
    public Vector4 Color { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>
    /// For the game's ground look: each corner's biome colour, as the game's terrain mesh has it (red in the lowest
    /// byte; <see cref="IAreaView.BiomeColor"/>). Null: drawn in <see cref="Color"/>.
    /// </summary>
    public uint[]? BiomeColors { get; set; }

    /// <summary>
    /// For the game's ground look: the paint mask the mesh is painted from (the terrain's _ClearedMaskTex: red dirt,
    /// green cultivated, blue paved, alpha vegetation), sampled at <see cref="MaskUvs"/> between its texels. Null with
    /// either: unpainted, as the game's untouched ground.
    /// </summary>
    public MaskTexture? Mask { get; set; }

    /// <summary>Each corner's place on <see cref="Mask"/> (0 to 1 across, 0 to 1 up).</summary>
    public Vector2[]? MaskUvs { get; set; }

    /// <summary>Whether clicks land on it (true), or go through it.</summary>
    public bool Pickable { get; set; } = true;
}

/// <summary>A small paint mask texture, filtered between its texels and clamped at its edges.</summary>
public sealed class MaskTexture
{
    /// <summary>A mask.</summary>
    /// <param name="width">Texels across (1 to 256).</param>
    /// <param name="height">Texels up (1 to 256).</param>
    /// <param name="texels">Row after row from the bottom, red in the lowest byte.</param>
    public MaskTexture(int width, int height, uint[] texels)
    {
        Width = width;
        Height = height;
        Texels = texels;
    }

    /// <summary>Texels across.</summary>
    public int Width { get; }

    /// <summary>Texels up.</summary>
    public int Height { get; }

    /// <summary>Row after row from the bottom, red in the lowest byte.</summary>
    public uint[] Texels { get; }
}

/// <summary>
/// A rectangle of the editor's ground left undrawn, in whole metres of the world: x from <see cref="X0"/> to
/// <see cref="X1"/>, z from <see cref="Z0"/> to <see cref="Z1"/> (the far edges not included).
/// </summary>
public sealed class GroundHole
{
    /// <summary>A hole.</summary>
    /// <param name="x0">West edge, world x in whole metres.</param>
    /// <param name="z0">South edge, world z in whole metres.</param>
    /// <param name="x1">East edge, past the hole.</param>
    /// <param name="z1">North edge, past the hole.</param>
    public GroundHole(int x0, int z0, int x1, int z1)
    {
        X0 = x0;
        Z0 = z0;
        X1 = x1;
        Z1 = z1;
    }

    /// <summary>West edge, world x in whole metres.</summary>
    public int X0 { get; }

    /// <summary>South edge, world z in whole metres.</summary>
    public int Z0 { get; }

    /// <summary>East edge, past the hole.</summary>
    public int X1 { get; }

    /// <summary>North edge, past the hole.</summary>
    public int Z1 { get; }
}
