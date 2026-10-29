using System;
using System.Collections.Generic;
using System.Numerics;
using NVector3 = System.Numerics.Vector3;

namespace ValheimGen;

/// <summary>
/// A blueprint's terrain snapshot as Expand World Data applies it to a blueprint location's ground (BlueprintTerrain):
/// a grid of nodes, a value at some (a height above the blueprint's centre, in X; or a paint, r g b a), centred and
/// turned in the blueprint's frame. Each vertex of the ground it reaches takes the value of the node nearest to it.
/// </summary>
/// <param name="Center">The grid's centre in the blueprint's frame, metres.</param>
/// <param name="Turn">The grid's turn in the blueprint's frame (a turn about y only).</param>
/// <param name="Spacing">Metres between two nodes.</param>
/// <param name="Rows">The rows of nodes (z), each node's value or null.</param>
public sealed record TerrainSnapshot(NVector3 Center, Quaternion Turn, float Spacing, IReadOnlyList<Vector4?[]> Rows)
{
    /// <summary>How many nodes a row has.</summary>
    public int Width => Rows.Count == 0 ? 0 : Rows[0].Length;

    /// <summary>
    /// The value of the node nearest to a point of the ground (BlueprintTerrain.FindNearest) with the blueprint placed
    /// at a spot and turned; null where no node is, or the nearest has no value.
    /// </summary>
    /// <param name="node">The point, world metres.</param>
    /// <param name="at">Where the blueprint is placed.</param>
    /// <param name="placed">How it is turned.</param>
    public Vector4? Nearest(NVector3 node, NVector3 at, Quaternion placed)
    {
        if (Width == 0)
        {
            return null;
        }
        var relative = Yaw(placed * Quaternion.Inverse(Turn));
        var local = NVector3.Transform(node - at, Quaternion.Inverse(relative));
        float s = MathF.Max(0.001f, Spacing);
        float x0 = Center.X - (Width - 1) * s * 0.5f, z0 = Center.Z - (Rows.Count - 1) * s * 0.5f;
        int x = (int)MathF.Round((local.X - x0) / s, MidpointRounding.ToEven), z = (int)MathF.Round((local.Z - z0) / s, MidpointRounding.ToEven);
        return x < 0 || x >= Width || z < 0 || z >= Rows.Count ? null : Rows[z][x];
    }

    /// <summary>How far its farthest node with a value is from the blueprint's centre on the ground (GetRadius).</summary>
    public float Reach()
    {
        float s = MathF.Max(0.001f, Spacing), x0 = Center.X - (Width - 1) * s * 0.5f, z0 = Center.Z - (Rows.Count - 1) * s * 0.5f, reach = 0f;
        for (int z = 0; z < Rows.Count; z++)
        {
            for (int x = 0; x < Rows[z].Length; x++)
            {
                if (Rows[z][x] != null)
                {
                    float nx = x0 + x * s, nz = z0 + z * s;
                    reach = MathF.Max(reach, (float)Math.Sqrt((double)nx * nx + (double)nz * nz));
                }
            }
        }
        return reach;
    }

    /// <summary>
    /// A turn's part about y (the mod's Yaw): from where it points z (its products in float, the rest in double); none
    /// for one pointing straight up or down.
    /// </summary>
    /// <param name="q">The turn.</param>
    public static Quaternion Yaw(Quaternion q)
    {
        double fx = 2.0 * (q.X * q.Z + q.W * q.Y), fz = 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
        if (fx * fx + fz * fz <= 0.0001)
        {
            return Quaternion.Identity;
        }
        double half = Math.Atan2(fx, fz) * 0.5;
        return new Quaternion(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half));
    }
}
