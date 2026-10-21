using System.Collections.Generic;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>What a copy takes (<see cref="IAreaCopier.Copy"/>): an outline on the ground around its middle.</summary>
public sealed class CopyRegion
{
    /// <summary>A copy's region.</summary>
    /// <param name="middle">Its middle, in world metres.</param>
    /// <param name="outline">Its outline, world x and z (as a vector's X and Y).</param>
    public CopyRegion(Vector3 middle, IReadOnlyList<Vector2> outline)
    {
        Middle = middle;
        Outline = outline;
    }

    /// <summary>
    /// The copy's middle, in world metres: the copy's objects keep their places as offsets from it, its height being
    /// the one they are put down from (<see cref="CopyPlacement.Middle"/>).
    /// </summary>
    public Vector3 Middle { get; }

    /// <summary>The copy's outline, world x and z (as a vector's X and Y), in order around it.</summary>
    public IReadOnlyList<Vector2> Outline { get; }

    /// <summary>Whether a point of the ground lies inside the outline.</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    public bool Contains(float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
        {
            Vector2 a = Outline[i], b = Outline[j];
            if (a.Y > z != b.Y > z && x < (b.X - a.X) * (z - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
