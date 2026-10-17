using System.Text;
using TerrainEditor.App;
using WorldEditor.Tests;
using WorldEditor.Tests.Unity;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor with a made-up Valheim it finds in a home of its own (HOME and XDG_DATA_HOME point to a test folder, where
/// Steam's library holds the game): its terrain material, with a small fragment shader written as Unity compiles the
/// game's for OpenGL, its textures and texture arrays. So the game look is read and drawn (GameLookGl) where there is
/// no Valheim (CI). Avalonia's warnings go to its console too (VWE_TRACE=1). It starts on the start page; a copy of the
/// test world is ready to open (World).
/// </summary>
public sealed class GameLookEditorProcess() : EditorProcess([], Home())
{
    /// <summary>
    /// RGBA32 (a texture's format) and RGBA8 (a texture array's graphics format).
    /// </summary>
    private const int Rgba32 = 4, Rgba8 = 8;

    /// <summary>
    /// The test folder: the home, the world's copy.
    /// </summary>
    private static string? _dir;

    /// <summary>
    /// The made-up game.
    /// </summary>
    private static TestGame? _game;

    /// <summary>
    /// The copy of the test world.
    /// </summary>
    public static string World => Path.Combine(_dir!, "CITest");

    /// <summary>
    /// The terrain's fragment program as the game has it for OpenGL (Unity's names, the per-zone ocean depth block,
    /// the paint mask read at vs_TEXCOORD0), small: what GameShader adapts and the view links with its own main.
    /// </summary>
    private static readonly string Fragment = string.Join(
        "\n",
        "#ifdef FRAGMENT",
        "#version 150",
        "uniform \tvec4 _Time;",
        "uniform \tvec3 _WorldSpaceCameraPos;",
        "uniform \tfloat _depth[4];",
        "UNITY_LOCATION(0) uniform  sampler2DArray _DiffuseArrayTex;",
        "UNITY_LOCATION(1) uniform  sampler2D _ClearedMaskTex;",
        "in  vec2 vs_TEXCOORD0;",
        "in  vec4 vs_TEXCOORD1;",
        "in  vec4 vs_TEXCOORD2;",
        "in  vec4 vs_TEXCOORD3;",
        "layout(location = 0) out vec4 SV_Target0;",
        "layout(location = 1) out vec4 SV_Target1;",
        "layout(location = 2) out vec4 SV_Target2;",
        "layout(location = 3) out vec4 SV_Target3;",
        "float u_xlat66;",
        "float u_xlat68;",
        "void main()",
        "{",
        "    u_xlat66 = _depth[2] + (-_depth[3]);",
        "    u_xlat66 = vs_TEXCOORD0.x * u_xlat66 + _depth[3];",
        "    u_xlat68 = (-_depth[0]) + _depth[1];",
        "    u_xlat68 = vs_TEXCOORD0.x * u_xlat68 + _depth[0];",
        "    u_xlat68 = (-u_xlat66) + u_xlat68;",
        "    u_xlat66 = vs_TEXCOORD0.y * u_xlat68 + u_xlat66;",
        "    SV_Target0 = texture(_ClearedMaskTex, vs_TEXCOORD0.xy);",
        "    SV_Target0.rgb = SV_Target0.rgb * 0.5 + texture(_DiffuseArrayTex, vec3(vs_TEXCOORD1.w * 0.1, vs_TEXCOORD3.w * 0.1, 0.0)).rgb * 0.5;",
        "    SV_Target1 = vec4(0.04, 0.04, 0.04, 0.2);",
        "    SV_Target2 = vec4(vs_TEXCOORD1.z, vs_TEXCOORD2.z, vs_TEXCOORD3.z, 0.0) * 0.5 + 0.5;",
        "    SV_Target3 = vec4(vec3(0.6 + u_xlat66 * 0.0), 1.0);",
        "}",
        "#endif",
        "");

    /// <summary>
    /// Makes the test folder (the world's copy, the game, the home whose Steam library has it) and gives the editor's
    /// environment.
    /// </summary>
    private static Dictionary<string, string> Home()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-look-" + Guid.NewGuid().ToString("N")[..8]);
        string world = Path.Combine(_dir, "CITest"), home = Path.Combine(_dir, "home");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        _game = MadeUpGame();
        // Steam's library in the home, as on Linux (elsewhere the game is not looked for in the home: the tests skip).
        string common = Path.Combine(home, ".steam", "steam", "steamapps", "common");
        Directory.CreateDirectory(common);
        if (OperatingSystem.IsLinux())
        {
            Directory.CreateSymbolicLink(Path.Combine(common, "Valheim"), _game.Root);
        }
        var env = new Dictionary<string, string>
        {
            ["HOME"] = home,
            // The bundles' index and the game data's caches: in the test home too, never the user's.
            ["XDG_DATA_HOME"] = Path.Combine(home, ".local", "share"),
            ["VWE_TRACE"] = "1",
        };
        // X's key file is looked for in the home when not given: the user's own kept.
        string key = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Xauthority");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XAUTHORITY")) && File.Exists(key))
        {
            env["XAUTHORITY"] = key;
        }
        return env;
    }

    /// <summary>
    /// A game with the terrain material (its shader's OpenGL program and its textures) and the terrain's texture
    /// arrays.
    /// </summary>
    private static TestGame MadeUpGame()
    {
        var game = new TestGame();
        var b = game.Bundle("look");
        var blobs = new Dictionary<int, byte[]> { [GameShader.OpenGlCore] = Encoding.Latin1.GetBytes(Fragment) };
        long shader = TestAssets.Shader(b, "Custom/Heightmap", blobs, [new(0, 0, GameShader.SpirvProgram, 1)]);
        // Each texture 1 x 2, its rows two colours.
        var textures = GameLookData.TerrainTextures.Keys
            .Select(p => (p, Fields.Ref(TestAssets.Texture(b, p, 1, 2, Rgba32, 1, [90, 120, 60, 255, 140, 110, 80, 255])), (1.0, 1.0), (0.0, 0.0)));
        b.Contents("assets/terrain/Heightmap_basematerial.mat", TestAssets.Material(b, "Heightmap_basematerial", Fields.Ref(shader), textures));
        b.Contents("assets/terrain/terrain_d_array.texture2darray", TestAssets.TextureArray(b, "terrain_d_array", 1, 1, Rgba8, 3, [.. Enumerable.Range(0, 12).Select(i => (byte)(60 + (i * 15)))]));
        b.Contents("assets/terrain/terrain_n_array.texture2darray", TestAssets.TextureArray(b, "terrain_n_array", 1, 1, Rgba8, 2, [128, 128, 255, 255, 128, 128, 255, 255]));
        game.Save();
        return game;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        _game?.Dispose();
        try
        {
            Directory.Delete(_dir!, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }
}
