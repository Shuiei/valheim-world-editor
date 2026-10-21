using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class DungeonEntrances
{
    /// <summary>
    /// The turn (degrees about the vertical, from 0 up to 360) that makes the location's door into its interior face
    /// from <paramref name="at"/> toward <paramref name="toward"/>; 0 when it has no such door or the two are one place.
    /// </summary>
    public static float YawToFace(LocationPrefabs.Prefab p, Vector3 at, Vector3 toward)
    {
        if (p.Entrance is not { } door)
        {
            return 0f;
        }
        var f = Vector3.Transform(Vector3.UnitZ, door.Rotation);
        float dx = toward.X - at.X, dz = toward.Z - at.Z;
        if (dx * dx + dz * dz < 1e-6f || f.X * f.X + f.Z * f.Z < 1e-6f)
        {
            return 0f;
        }
        return Wrapped((MathF.Atan2(dx, dz) - MathF.Atan2(f.X, f.Z)) * 180f / MathF.PI);
    }

    /// <summary>The turn about the vertical a pose faces (degrees from 0 up to 360, 0 along +z), as YawToFace's.</summary>
    public static float YawOf(Pose pose)
    {
        var f = pose.Forward;
        return Wrapped(MathF.Atan2(f.X, f.Z) * 180f / MathF.PI);
    }

    /// <summary>An angle in degrees brought into 0 up to 360.</summary>
    private static float Wrapped(float degrees) => ((degrees % 360f) + 360f) % 360f;
}
