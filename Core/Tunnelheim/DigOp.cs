using System.Numerics;

namespace TerrainEditor.Tunnelheim;

/// <summary>What one of Tunnelheim's edits does (the int each edit starts with in a column's record).</summary>
public enum DigKind
{
    /// <summary>Carves a sphere: <see cref="DigOp.Value"/> is its radius (the oldest records hold only these).</summary>
    Sphere = 0,

    /// <summary>A hoe or cultivator stroke on rock, the game's terrain operation named by <see cref="DigOp.Code"/> (its prefab's hash).</summary>
    Hoe = 1,

    /// <summary>Carves a box: <see cref="DigOp.Value"/> is its half size, <see cref="DigOp.Extra"/> its turn (degrees).</summary>
    Box = 2,

    /// <summary>Adds rock in a sphere (the hoe's Add Earth): <see cref="DigOp.Value"/> is its radius.</summary>
    Fill = 3,

    /// <summary>Marks the ore vein cell at <see cref="DigOp.Position"/> as found; changes no rock.</summary>
    Vein = 4,

    /// <summary>Smooths rock towards a plane (Smooth Surface, Level Wall, Level Ceiling): radius, aimed normal.</summary>
    Plane = 5,

    /// <summary>Carves a capsule along the aimed direction (read, not made by Tunnelheim 1.0.0).</summary>
    Stroke = 6,

    /// <summary>A round pickaxe dig: an ellipsoid along the aimed direction, <see cref="DigOp.Value"/> its size.</summary>
    Chip = 7,

    /// <summary>Carves steps down along the aimed direction (read, not made by Tunnelheim 1.0.0).</summary>
    Stairs = 8,

    /// <summary>A round dig made with Dig straight on: like <see cref="Chip"/>, leaving the rock below a floor height.</summary>
    ChipFloor = 9,

    /// <summary>A hoe stroke whose terrain operation's settings travel with it (<see cref="DigOp.Settings"/>).</summary>
    SizedHoe = 10,
}

/// <summary>
/// One of Tunnelheim's edits as a column's record keeps it (the mod replays a column's edits in order over its ground).
/// The fields mean what <see cref="Kind"/> says; the aimed kinds keep their direction in <see cref="Code"/> (pitch,
/// thousandths of a degree) and <see cref="Extra"/> (yaw, degrees).
/// </summary>
/// <param name="Kind">What the edit does.</param>
/// <param name="Position">Where: a centre, a hit point, an aim point (world metres).</param>
/// <param name="Value">A radius, half size or dig size.</param>
/// <param name="Code">The pitch, the hoe operation's prefab hash, or (<see cref="DigKind.ChipFloor"/>) the floor and pitch packed.</param>
/// <param name="Extra">The yaw or turn, in degrees.</param>
/// <param name="Author">The player id of whoever made it, 0 for none.</param>
/// <param name="Settings">The hoe operation's settings (<see cref="DigKind.SizedHoe"/> only).</param>
public readonly record struct DigOp(DigKind Kind, Vector3 Position, float Value, int Code = 0, float Extra = 0, long Author = 0, HoeSettings? Settings = null)
{
    /// <summary>Whether the edit carves rock away.</summary>
    public bool Carves => Kind is DigKind.Sphere or DigKind.Box or DigKind.Stroke or DigKind.Chip or DigKind.Stairs or DigKind.ChipFloor;
}

/// <summary>
/// The settings of one of the game's terrain operations (TerrainOp.Settings), in the order Tunnelheim writes them
/// after a <see cref="DigKind.SizedHoe"/> edit.
/// </summary>
/// <param name="LevelOffset">How far above the stroke's point the levelled ground goes.</param>
/// <param name="Level">Whether it levels.</param>
/// <param name="LevelRadius">How far it levels.</param>
/// <param name="Square">Whether its areas are squares rather than circles.</param>
/// <param name="Raise">Whether it raises.</param>
/// <param name="RaiseRadius">How far it raises.</param>
/// <param name="RaisePower">How the raise fades with distance.</param>
/// <param name="RaiseDelta">How much it raises (negative digs).</param>
/// <param name="Smooth">Whether it smooths.</param>
/// <param name="SmoothRadius">How far it smooths.</param>
/// <param name="SmoothPower">How the smoothing fades with distance.</param>
/// <param name="PaintCleared">Whether it paints.</param>
/// <param name="PaintType">The paint (the game's TerrainModifier.PaintType: 0 dirt, 1 cultivate, 2 paved, 3 reset, 4 clear vegetation, 5 deep snow).</param>
/// <param name="PaintRadius">How far it paints.</param>
public sealed record HoeSettings(
    float LevelOffset,
    bool Level,
    float LevelRadius,
    bool Square,
    bool Raise,
    float RaiseRadius,
    float RaisePower,
    float RaiseDelta,
    bool Smooth,
    float SmoothRadius,
    float SmoothPower,
    bool PaintCleared,
    int PaintType,
    float PaintRadius)
{
    /// <summary>
    /// How far the operation reaches: its largest enabled radius (TerrainOp.Settings.GetRadius, which counts the
    /// paint radius whenever it paints).
    /// </summary>
    public float Radius() =>
        MathF.Max(MathF.Max(Level ? LevelRadius : 0, Raise ? RaiseRadius : 0), MathF.Max(Smooth ? SmoothRadius : 0, PaintCleared ? PaintRadius : 0));
}
