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

    /// <summary>A custom sail's corner key (1 to 4).</summary>
    public static string SailCornerKey(int corner) => SailCorner.Replace("#", corner.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
