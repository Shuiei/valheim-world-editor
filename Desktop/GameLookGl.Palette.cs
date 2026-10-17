using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's look colours at midday in the default Clear weather, as linear colours for the shaders.
/// </summary>
public sealed partial class GameLookGl
{
    // ---- Midday in the game's default "Clear" environment (EnvMan.m_environments), like look.js DAY:
    // the game's gamma-space colours turned linear.
    /// <summary>
    /// An sRGB (gamma-space) colour channel, 0..1, as linear.
    /// </summary>
    private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    /// <summary>
    /// An sRGB colour as linear.
    /// </summary>
    private static Vector3 Srgb(float r, float g, float b) => new(Lin(r), Lin(g), Lin(b));
    /// <summary>
    /// The direction towards the sun, in Unity's world space.
    /// </summary>
    public static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-0.42f, 0.72f, 0.55f));
    /// <summary>
    /// The sun's light (linear, past 1).
    /// </summary>
    public static readonly Vector3 SunColor = Srgb(1.0f, 0.886f, 0.742f) * 1.7f;
    /// <summary>
    /// The ambient light (linear), given to the terrain shader as its constant spherical harmonics term.
    /// </summary>
    public static readonly Vector3 Ambient = Srgb(0.463f, 0.574f, 0.706f);
    /// <summary>
    /// The fog's colour (linear).
    /// </summary>
    public static readonly Vector3 FogColor = Srgb(0.302f, 0.581f, 0.747f);
    /// <summary>
    /// The fog's colour looking towards the sun (linear).
    /// </summary>
    public static readonly Vector3 SunFogColor = Srgb(0.801f, 0.687f, 0.43f);
    /// <summary>
    /// The exponential-squared fog's density, per metre.
    /// </summary>
    public const float FogDensity = 0.003f;
    /// <summary>
    /// Where the fog starts, in metres from the camera (the view sets it each frame: the orbit's distance, so zooming
    /// out does not fog the area over; 0 walking or flying, as in game).
    /// </summary>
    public static float FogStart { get; set; }
    /// <summary>
    /// The sky's colour straight up (linear).
    /// </summary>
    public static readonly Vector3 SkyTop = Srgb(0.2f, 0.42f, 0.68f);
    /// <summary>
    /// The sky's colour at the horizon (linear).
    /// </summary>
    public static readonly Vector3 SkyHorizon = Srgb(0.55f, 0.72f, 0.84f);
    /// <summary>
    /// The sun in view space (z mirrored), for the objects' simpler lighting.
    /// </summary>
    public static Vector3 SunDirView => new(SunDir.X, SunDir.Y, -SunDir.Z);
}
