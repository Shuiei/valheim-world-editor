using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// Where ValheimRAFT builds what its data alone does not place (the editor's ILinkPlacer): the pieces on a swivel the
/// mod has turned or moved, and a land vehicle parked on a ship. Checked against the mod on a server.
/// </summary>
internal sealed class VehiclePlacer : ILinkPlacer
{
    /// <summary>The swivel's own settings, as its prefab has them: what the mod uses for a value the swivel does not keep.</summary>
    private const int DefaultMode = 1, DefaultHinges = 1;

    /// <summary>How far a turning swivel turns by default about x, y and z, in degrees (its prefab's).</summary>
    private static readonly Vector3 DefaultMax = new(45, 90, 45);

    /// <inheritdoc/>
    public LinkFrame? FrameOf(IObjectView obj) => obj.Prefab == Keys.Swivel ? SwivelFrame(obj) : null;

    /// <summary>
    /// <inheritdoc/> A land vehicle parked on a ship: the mod builds it again at the ship's place, with its own turn
    /// (the offset it keeps is not used).
    /// </summary>
    public LinkFrame? PlaceOf(IObjectView part, IObjectView parent) =>
        part.Prefab == Keys.LandVehicle ? new LinkFrame(Vector3.Zero, Quaternion.Inverse(parent.Orientation) * part.Orientation) : null;

    /// <summary>
    /// The frame a swivel's pieces hang in: its platform as the mod leaves it. A turning swivel at its target (or going
    /// there) is turned by its largest turn about each of its axes, a moving one moved by its offset; at its start (or
    /// going back), or following the wind, its platform is drawn at rest.
    /// </summary>
    public static LinkFrame? SwivelFrame(IObjectView swivel)
    {
        int mode = swivel.TryGetInt(Keys.SwivelMode, out int m) ? m : DefaultMode;
        bool atTarget = swivel.TryGetInt(Keys.SwivelState, out int s) && s is 2 or 3;
        if (!atTarget)
        {
            return null;
        }
        if (mode == 1)
        {
            int hinges = swivel.TryGetInt(Keys.SwivelHinges, out int h) ? h : DefaultHinges;
            var max = Axes(swivel, Keys.SwivelMax, DefaultMax);
            var euler = new Vector3((hinges & 1) != 0 ? max.X : 0, (hinges & 2) != 0 ? max.Y : 0, (hinges & 4) != 0 ? max.Z : 0);
            return new LinkFrame(Vector3.Zero, Euler(euler));
        }
        return mode == 2 ? new LinkFrame(Axes(swivel, Keys.SwivelOffset, Vector3.Zero), Quaternion.Identity) : null;
    }

    /// <summary>A swivel's three values of a kind (x, y, z), each its default when it keeps none.</summary>
    private static Vector3 Axes(IObjectView swivel, string key, Vector3 fallback)
    {
        float A(string axis, float d) => swivel.TryGetFloat(key.Replace("#", axis), out float v) ? v : d;
        return new Vector3(A("X", fallback.X), A("Y", fallback.Y), A("Z", fallback.Z));
    }

    /// <summary>A turn from Unity Euler degrees (about z, then x, then y), as the game makes it.</summary>
    public static Quaternion Euler(Vector3 degrees)
    {
        const float Rad = (float)(Math.PI / 180);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees.Y * Rad)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, degrees.X * Rad)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees.Z * Rad);
    }
}
