using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's look files read for the view: the terrain shader and its textures, read from the game's files once and kept for the view.
/// </summary>
public sealed partial class GameLookGl
{
    // ---- Files, read on a worker thread (Read), then sent to the graphics card (Init).
    /// <summary>
    /// The terrain's shader and textures as read from the game's files.
    /// </summary>
    /// <param name="Fragment">The game's terrain fragment shader (GLSL ES), which writes its G-buffer in
    /// valheimGBuffer.</param>
    /// <param name="DiffuseArray">The ground textures' colours, a texture array (its square slices one above the
    /// other).</param>
    /// <param name="NormalArray">Their normal maps, a texture array likewise.</param>
    /// <param name="Noise">The shader's noise texture.</param>
    /// <param name="Cliff">The cliff normal map.</param>
    /// <param name="MistCliff">The Mistlands cliff normal map.</param>
    /// <param name="Paved">The paved ground normal map.</param>
    /// <param name="Rock">The rock normal map.</param>
    /// <param name="Snow">The snow normal map.</param>
    /// <param name="Variety">The colour variety noise.</param>
    public sealed record Files(string Fragment, Image DiffuseArray, Image NormalArray, Image Noise, Image Cliff, Image MistCliff,
        Image Paved, Image Rock, Image Snow, Image Variety);
    /// <summary>
    /// A picture of the game's files, rows bottom first (Unity's origin).
    /// </summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Rgba">Four bytes per pixel, row by row.</param>
    public sealed record Image(int Width, int Height, byte[] Rgba);

    /// <summary>
    /// The terrain's shader and textures, read from the game's files (rows bottom first, Unity's origin; a texture
    /// array is its square slices one above the other). Null when Valheim is not found. Read once per bundles reader
    /// (<see cref="Reads"/>): they do not depend on the area.
    /// </summary>
    public static Files? Read() => GameLook.Bundles is { } game ? Reads.Of(game) : null;

    /// <summary>The files as read already for the bundles reader in use, without reading; null when not read (yet).</summary>
    public static Files? Kept() => GameLook.Bundles is { } game && Reads.TryKept(game, out var files) ? files : null;

    /// <summary>The files read for the last bundles reader asked for (another Valheim folder: read again).</summary>
    private static readonly KeptRead<GameBundles, Files> Reads = new(ReadFrom);

    /// <summary>Reads the terrain's shader and textures from the game's bundles (see <see cref="Read"/>).</summary>
    private static Files ReadFrom(GameBundles game)
    {
        var t = GameLookData.ReadTerrain(game);
        Image Of(GameLookData.Picture p) => new(p.Width, p.Height, p.Rgba);
        Image Tex(string name) => Of(t.Textures[name]);
        return new Files(t.Fragment, Of(t.DiffuseArray), Of(t.NormalArray), Tex("noise"), Tex("cliff_n"), Tex("mistcliff_n"), Tex("paved_n"), Tex("rock_n"), Tex("snow_n"), Tex("variety"));
    }
}
