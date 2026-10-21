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
                if (Mesh(area, grid, RockMesher.Build(grid, cx, cy, cz), new Vector3(x0, cy * RockGrid.ColumnMetres, z0)) is { } mesh)
                {
                    layer.Meshes.Add(mesh);
                }
            }
        }
        return layer;
    }

    /// <summary>A chunk's rock in the world, coloured and painted as the mod draws it; null when it has no surface.</summary>
    private static SceneMesh? Mesh(IAreaView area, RockGrid grid, RockMesh rock, Vector3 corner)
    {
        if (rock.Triangles.Length == 0)
        {
            return null;
        }
        float half = RockGrid.ColumnMetres / 2;
        var palette = RockLook.At(area, corner + new Vector3(half, half, half));
        var positions = new Vector3[rock.Positions.Length];
        uint[] colours = new uint[positions.Length];
        var uvs = new Vector2[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i] = corner + rock.Positions[i];
            colours[i] = area.BiomeColor((int)Math.Floor(p.X), (int)Math.Floor(p.Z));
            uvs[i] = RockLook.Uv(palette, rock.Paint[i], rock.Dug[i], Ground(area, grid, palette, rock, i, p));
        }
        return new SceneMesh(positions, rock.Normals, rock.Triangles) { BiomeColors = colours, Mask = RockLook.Mask(palette), MaskUvs = uvs };
    }

    /// <summary>
    /// The ground's value a vertex's natural paint follows on a strip palette: in the Ashlands where the vertex lies open
    /// to the sky (no rock above a little out from it), elsewhere where it is natural rock not dug; 0 otherwise.
    /// </summary>
    private static float Ground(IAreaView area, RockGrid grid, RockLook.Palette palette, RockMesh rock, int i, Vector3 p)
    {
        bool counts = palette switch
        {
            RockLook.Palette.Default => false,
            RockLook.Palette.Ashlands => OpenToSky(grid, p + (rock.Normals[i] * 0.6f)),
            _ => !rock.Dug[i] && rock.Paint[i] == RockPaint.None,
        };
        return counts ? GroundAt(area, palette, p.X, p.Z) : 0f;
    }

    /// <summary>Whether no rock stands over a point (the topmost rock there at most 0.2 m above it).</summary>
    private static bool OpenToSky(RockGrid grid, Vector3 p) =>
        grid.TopRockAt(RockMath.Round(p.X / RockGrid.Step), RockMath.Round(p.Z / RockGrid.Step)) <= p.Y + 0.2f;

    /// <summary>The ground's value at a point, between the four whole metres around it.</summary>
    private static float GroundAt(IAreaView area, RockLook.Palette palette, float x, float z)
    {
        int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
        float fx = x - x0, fz = z - z0;
        float V(int dx, int dz) => RockLook.GroundValue(palette, area.GroundMask(x0 + dx, z0 + dz));
        float south = V(0, 0) + ((V(1, 0) - V(0, 0)) * fx), north = V(0, 1) + ((V(1, 1) - V(0, 1)) * fx);
        return south + ((north - south) * fz);
    }
}
