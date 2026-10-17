using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The move set_transform makes of one object (its place, turn and size, what is left out kept as it is), or why it is
    /// left out: it would leave the area, or its size is out of range.
    /// </summary>
    /// <param name="scene">The open area.</param>
    /// <param name="o">The object's new values.</param>
    /// <param name="area">The area's bounds in world metres.</param>
    internal ((int Index, Vector3 Position, Vector3 Rotation, float Scale)? Move, string? Problem) PlanTransform(WorldScene scene, TransformSpec o, (float MinX, float MinZ, float MaxX, float MaxZ) area)
    {
        var t = scene.Things[o.Id];
        float x = o.X ?? t.Position.X, z = o.Z ?? t.Position.Z;
        if (x < area.MinX || x > area.MaxX || z < area.MinZ || z > area.MaxZ)
        {
            return (null, $"{o.Id}: ({x:0.##}, {z:0.##}) is outside the area (x {area.MinX} to {area.MaxX}, z {area.MinZ} to {area.MaxZ})");
        }
        if (o.Scale is float sc && (!float.IsFinite(sc) || sc < MinScale || sc > MaxScale))
        {
            return (null, $"{o.Id}: a scale of {sc} is not between {MinScale} and {MaxScale}");
        }
        var rot = Wrapped(new Vector3(o.Pitch ?? t.Rotation.X, o.Yaw ?? t.Rotation.Y, o.Roll ?? t.Rotation.Z));
        float scale = o.Scale ?? t.Scale;
        // A scale of 0 is the prefab's own size.
        float y = o.Y ?? (o.OnGround && _w.NameOfPrefab(t.Prefab) is string n
            ? GroundAt(scene, x, z) - Editing.Hammer.Bottom(n, BlueprintFormats.FromEuler(rot), scale > 0 ? scale : 1)
            : t.Position.Y);
        return ((o.Id, new Vector3(x, y, z), rot, scale), null);
    }
}
