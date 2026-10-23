namespace ValheimRAFT.WorldEditor;

/// <summary>
/// ValheimRAFT's data keys and prefabs the add-on reads, as the mod's code names them (the same names as in the
/// add-on's worldeditor.json).
/// </summary>
internal static class Keys
{
    /// <summary>Int: an object's own id, which the pieces built on it, ropes and switches name (the mod's ZdoWatcher keeps it).</summary>
    public const string Id = "PersistentID";

    /// <summary>Bytes: a rope anchor's ropes, each the id of what it is tied to and which of its tie points (RopeList).</summary>
    public const string Ropes = "MBRopeAnchor_Ropes";

    /// <summary>Int: how many corners a custom sail has (3 or 4).</summary>
    public const string SailCorners = "m_sailCornersCountHash";

    /// <summary>Vec3: a custom sail's corners, in the sail's frame (the name with 1 to 4 in place of the #).</summary>
    public const string SailCorner = "m_sailCorner#Hash";

    /// <summary>Bytes: a custom sail's colour, red, green, blue and alpha as four bytes.</summary>
    public const string SailColour = "m_mainColor";

    /// <summary>Bytes: the colour of the pattern drawn on a custom sail (as <see cref="SailColour"/>).</summary>
    public const string PatternColour = "m_patternColor";

    /// <summary>Bytes: the colour of the logo drawn on a custom sail (as <see cref="SailColour"/>).</summary>
    public const string LogoColour = "m_logoColor";

    /// <summary>The custom sail, its cloth woven by the mod between its corners while the game runs.</summary>
    public const string Sail = "MBSail";

    /// <summary>The rope anchor, its ropes strung by the mod while the game runs.</summary>
    public const string RopeAnchor = "MBRopeAnchor";

    /// <summary>The dock's tie point, a rope anchor too.</summary>
    public const string DockAnchor = "ValheimVehicles_DockAttachpoint";

    /// <summary>The swivel, whose platform the mod turns or moves with what is built on it.</summary>
    public const string Swivel = "ValheimVehicles_Swivel";

    /// <summary>The land vehicle (a vehicle on its own, or parked on a ship).</summary>
    public const string LandVehicle = "ValheimVehicles_VehicleLand";

    /// <summary>The rope ladder, its rope and rungs hung by the mod while the game runs.</summary>
    public const string RopeLadder = "MBRopeLadder";

    /// <summary>Int: a swivel's mode (0 none, 1 turns, 2 moves, 3 follows the wind).</summary>
    public const string SwivelMode = "swivel_mode";

    /// <summary>Int: where a swivel is (0 at its start, 1 going back to it, 2 at its target, 3 going to it).</summary>
    public const string SwivelState = "swivel_motionState";

    /// <summary>Int: the axes a turning swivel turns about (1 x, 2 y, 4 z; added).</summary>
    public const string SwivelHinges = "swivel_hingeAxes";

    /// <summary>Float: how far a turning swivel turns about each axis, in degrees (the name with X, Y or Z in place of the #).</summary>
    public const string SwivelMax = "swivel_max#";

    /// <summary>Float: how far a moving swivel moves along each axis, in metres (the name with X, Y or Z in place of the #).</summary>
    public const string SwivelOffset = "swivel_offset#";

    /// <summary>A custom sail's corner key (1 to 4).</summary>
    public static string SailCornerKey(int corner) => SailCorner.Replace("#", corner.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
