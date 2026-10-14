using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Triangles drawn for an object, in its own frame (metres; x right, y up, z forward, as the game's local
/// space; or in the world's, see <see cref="WorldSpace"/>), in one colour.
/// </summary>
public sealed class Shape
{
    /// <summary>A shape.</summary>
    /// <param name="vertices">Its corners, in the object's frame.</param>
    /// <param name="triangles">Three corner indices per triangle, wound clockwise seen from the front (Unity's way).</param>
    /// <param name="color">Its colour, red, green, blue and alpha from 0 to 1 (alpha below 1: see-through).</param>
    public Shape(Vector3[] vertices, int[] triangles, Vector4 color)
    {
        Vertices = vertices;
        Triangles = triangles;
        Color = color;
    }

    /// <summary>Its corners, in the object's frame.</summary>
    public Vector3[] Vertices { get; }

    /// <summary>Three corner indices per triangle, wound clockwise seen from the front.</summary>
    public int[] Triangles { get; }

    /// <summary>Its colour, red, green, blue and alpha from 0 to 1.</summary>
    public Vector4 Color { get; }

    /// <summary>Seen from both sides (a sail, a cloth); else only from the front.</summary>
    public bool BothSides { get; set; }

    /// <summary>
    /// Its corners are world positions (a rope from this object to another), not in the object's frame: the object's
    /// place, turn and size are not applied.
    /// </summary>
    public bool WorldSpace { get; set; }
}
