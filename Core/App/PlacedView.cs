using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// An object as it sits in the world, as the views of a pasted copy and of a world object give it: its prefab,
/// where it is, its turn and its size.
/// </summary>
/// <param name="prefab">Its prefab's name ("" when not known).</param>
/// <param name="position">Where it is.</param>
/// <param name="rotation">Its turn, Unity Euler degrees.</param>
/// <param name="scale">Its size (0 or 1: the prefab's own).</param>
public abstract class PlacedView(string prefab, Vector3 position, Vector3 rotation, float scale)
{
    /// <summary>Its prefab's name ("" when not known).</summary>
    public string Prefab => prefab;

    /// <summary>Where it is.</summary>
    public Vector3 Position => position;

    /// <summary>Its turn, Unity Euler degrees.</summary>
    public Vector3 Rotation => rotation;

    /// <summary>Its turn as a quaternion (BlueprintFormats.FromEuler).</summary>
    public Quaternion Orientation => BlueprintFormats.FromEuler(rotation);

    /// <summary>Its size: the given one, or 1 when none (0 or less means the prefab's own).</summary>
    public float Scale => scale > 0 ? scale : 1;
}
