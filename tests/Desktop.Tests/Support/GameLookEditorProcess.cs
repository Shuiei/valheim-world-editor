using System.Text;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using WorldEditor.Tests;
using WorldEditor.Tests.Unity;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor with a made-up Valheim it finds in a home of its own (HOME and XDG_DATA_HOME point to a test folder, where
/// Steam's library holds the game): its terrain material, with a small fragment shader written as Unity compiles the
/// game's for OpenGL, its textures and texture arrays, and models for three kinds of object the test world's first zone
/// holds and for every dungeon room of the game. So the game look is read and drawn (GameLookGl), and the models with
/// it, where there is no Valheim (CI). Avalonia's warnings go to its console too (VWE_TRACE=1). It starts on the start
/// page; a copy of the test world is ready to open (World). A subclass gives the game another terrain shader
/// (BrokenLookEditorProcess).
/// </summary>
public class GameLookEditorProcess : EditorProcess
{
    /// <summary>
    /// RGBA32 (a texture's format) and RGBA8 (a texture array's graphics format).
    /// </summary>
    private const int Rgba32 = 4, Rgba8 = 8;

    /// <summary>
    /// The test folder: the home, the world's copy.
    /// </summary>
    private readonly string _dir;

    /// <summary>
    /// The made-up game.
    /// </summary>
    private readonly TestGame _game;

    /// <summary>
    /// The copy of the test world.
    /// </summary>
    public string World => Path.Combine(_dir, "CITest");

    /// <summary>The editor with the usual made-up game: the terrain's fragment program as the game has it.</summary>
    public GameLookEditorProcess()
        : this(Setup.Make(Fragment))
    {
    }

    /// <summary>The editor with a made-up game whose terrain shader is <paramref name="fragment"/>.</summary>
    protected GameLookEditorProcess(string fragment)
        : this(Setup.Make(fragment))
    {
    }

    private GameLookEditorProcess(Setup setup)
        : base([], setup.Env)
    {
        _dir = setup.Dir;
        _game = setup.Game;
    }

    /// <summary>
    /// The terrain's fragment program as the game has it for OpenGL (Unity's names, the per-zone ocean depth block,
    /// the paint mask read at vs_TEXCOORD0), small: what GameShader adapts and the view links with its own main.
    /// </summary>
    protected static readonly string Fragment = string.Join(
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
    /// What a process starts from: the test folder (the world's copy, the home whose Steam library has the game), the
    /// made-up game, and the editor's environment.
    /// </summary>
    private sealed record Setup(string Dir, TestGame Game, Dictionary<string, string> Env)
    {
        /// <summary>Makes the test folder and the game, with this terrain shader.</summary>
        public static Setup Make(string fragment)
        {
            string dir = TestWorldCopy.Make("vwe-visual-look-");
            string home = Path.Combine(dir, "home");
            var game = MadeUpGame(fragment);
            // Steam's library in the home, as on Linux (elsewhere the game is not looked for in the home: the tests skip).
            string common = Path.Combine(home, ".steam", "steam", "steamapps", "common");
            Directory.CreateDirectory(common);
            if (OperatingSystem.IsLinux())
            {
                Directory.CreateSymbolicLink(Path.Combine(common, "Valheim"), game.Root);
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
            return new Setup(dir, game, env);
        }
    }

    /// <summary>
    /// A game with the terrain material (its shader's OpenGL program and its textures), the terrain's texture arrays,
    /// and models for three kinds of object the test world's first zone holds and for the game's dungeon rooms
    /// (<see cref="Models"/>).
    /// </summary>
    /// <param name="fragment">The terrain shader's fragment program.</param>
    private static TestGame MadeUpGame(string fragment)
    {
        var game = new TestGame();
        var b = game.Bundle("look");
        var blobs = new Dictionary<int, byte[]> { [GameShader.OpenGlCore] = Encoding.Latin1.GetBytes(fragment) };
        long shader = TestAssets.Shader(b, "Custom/Heightmap", blobs, [new(0, 0, GameShader.SpirvProgram, 1)]);
        // Each texture 1 x 2, its rows two colours.
        var textures = GameLookData.TerrainTextures.Keys
            .Select(p => (p, Fields.Ref(TestAssets.Texture(b, p, 1, 2, Rgba32, 1, [90, 120, 60, 255, 140, 110, 80, 255])), (1.0, 1.0), (0.0, 0.0)));
        b.Contents("assets/terrain/Heightmap_basematerial.mat", TestAssets.Material(b, "Heightmap_basematerial", Fields.Ref(shader), textures));
        b.Contents("assets/terrain/terrain_d_array.texture2darray", TestAssets.TextureArray(b, "terrain_d_array", 1, 1, Rgba8, 3, [.. Enumerable.Range(0, 12).Select(i => (byte)(60 + (i * 15)))]));
        b.Contents("assets/terrain/terrain_n_array.texture2darray", TestAssets.TextureArray(b, "terrain_n_array", 1, 1, Rgba8, 2, [128, 128, 255, 255, 128, 128, 255, 255]));
        Models(game.Bundle("models"));
        game.Save();
        return game;
    }

    /// <summary>
    /// Models for three kinds of saved object in the test world's first zone, so the editor draws them from the game's
    /// files (and picks them by their meshes): a chest drawn see-through (glass), a fir tree with a cut-out texture, a
    /// rock plain. And one for every dungeon room the game has (the tests' copy of its rooms), plain, so a dungeon of
    /// the game's rooms is drawn solid. Each is one upright 3 m square standing on the ground.
    /// </summary>
    /// <param name="b">The bundle the models go into.</param>
    private static void Models(TestBundle b)
    {
        long square = new TestMesh { Name = "square", Positions = [-1.5f, 0, 0, 1.5f, 0, 0, 1.5f, 3, 0, -1.5f, 3, 0], Submeshes = [[0, 1, 2, 0, 2, 3]] }.Add(b);
        // 2 x 1: an opaque green pixel and a transparent one, cut out.
        long bark = TestAssets.Texture(b, "bark", 2, 1, Rgba32, 1, [60, 120, 40, 255, 0, 0, 0, 0]);
        long glass = TestAssets.Material(b, "chest_glass", floats: [("_Mode", 3)], colors: [("_Color", 0.6, 0.8, 1.0, 0.5)]);
        long cutOut = TestAssets.Material(b, "fir_cutout", textures: [("_MainTex", Fields.Ref(bark), (1, 1), (0, 0))], floats: [("_Mode", 1), ("_Cutoff", 0.4)]);
        long plain = TestAssets.Material(b, "rock_plain", colors: [("_Color", 0.5, 0.5, 0.5, 1.0)]);
        foreach (var (prefab, material) in new[] { ("piece_chest_wood", glass), ("FirTree", cutOut), ("Rock_4", plain) })
        {
            b.Prefab(prefab, r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Child("body", c => c.Renderer(Fields.Ref(square), [Fields.Ref(material)]));
            });
        }
        foreach (string room in Rooms())
        {
            b.Prefab(room, r => r.Child("walls", c => c.Renderer(Fields.Ref(square), [Fields.Ref(plain)])));
        }
    }

    /// <summary>The names of the game's dungeon rooms, from the tests' copy of them (what the editor's dungeons are made of).</summary>
    private static IEnumerable<string> Rooms() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Fixtures.Root, "dungeon-rooms.json")))!["rooms"]!.AsObject().Select(r => r.Key);

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        _game.Dispose();
        TestWorldCopy.Remove(_dir);
        GC.SuppressFinalize(this);
    }
}
