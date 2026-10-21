using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The tunnels drawn in an open area (the scene layer hook): each built column's rock meshed as the mod meshes it,
/// with the game's ground look (the biome colours of the ground there, painted as the mod paints it), and the editor's
/// ground left undrawn over the whole column, as the mod cuts the game's.
/// </summary>
/// <param name="host">The editor, for the game's terrain operations.</param>
/// <param name="log">Where what cannot be read is told.</param>
public sealed class TunnelScene(IEditorHost host, Action<string> log) : ISceneProvider
{
    /// <summary>
    /// The mod's default palette, 2 × 2 from the bottom row: natural (the game's untouched ground), dirt; cultivated,
    /// paved. Red is the lowest byte.
    /// </summary>
    private static readonly MaskTexture Palette = new(2, 2, [0xFF000000u, 0xFF0000FFu, 0xFF00FF00u, 0xFFFF0000u]);

    /// <inheritdoc/>
    public SceneLayer? Build(IAreaView area)
    {
        if (area.ObjectsOf(TunnelheimMod.RecordPrefabName).Count == 0)
        {
            return null;
        }
        var grid = TunnelArea.Build(area, TerrainOps.From(host.Game.Components(TerrainOps.ScriptClass)), log);
        var layer = new SceneLayer();
        foreach (var (cx, cz) in grid.Columns)
        {
            int x0 = cx * (int)RockGrid.ColumnMetres, z0 = cz * (int)RockGrid.ColumnMetres;
            layer.Holes.Add(new GroundHole(x0, z0, x0 + (int)RockGrid.ColumnMetres, z0 + (int)RockGrid.ColumnMetres));
            foreach (int cy in grid.ChunksOf(cx, cz))
            {
                if (Mesh(area, RockMesher.Build(grid, cx, cy, cz), new Vector3(x0, cy * RockGrid.ColumnMetres, z0)) is { } mesh)
                {
                    layer.Meshes.Add(mesh);
                }
            }
        }
        return layer;
    }

    /// <summary>A chunk's rock in the world, coloured and painted as the mod draws it; null when it has no surface.</summary>
    private static SceneMesh? Mesh(IAreaView area, RockMesh rock, Vector3 corner)
    {
        if (rock.Triangles.Length == 0)
        {
            return null;
        }
        var positions = new Vector3[rock.Positions.Length];
        uint[] colours = new uint[positions.Length];
        var uvs = new Vector2[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i] = corner + rock.Positions[i];
            colours[i] = area.BiomeColor((int)Math.Floor(p.X), (int)Math.Floor(p.Z));
            uvs[i] = PaintUv(rock.Paint[i], rock.Dug[i]);
        }
        return new SceneMesh(positions, rock.Normals, rock.Triangles) { BiomeColors = colours, Mask = Palette, MaskUvs = uvs };
    }

    /// <summary>
    /// Where a vertex's paint lies on the palette: dirt, cultivated and paved their own; natural and no-vegetation
    /// natural; unpainted rock dirt where it was dug, natural where it was not.
    /// </summary>
    private static Vector2 PaintUv(byte paint, bool dug) => paint switch
    {
        RockPaint.Dirt => new(0.75f, 0.25f),
        RockPaint.Cultivated => new(0.25f, 0.75f),
        RockPaint.Paved => new(0.75f, 0.75f),
        RockPaint.None when dug => new(0.75f, 0.25f),
        _ => new(0.25f, 0.25f),
    };
}
