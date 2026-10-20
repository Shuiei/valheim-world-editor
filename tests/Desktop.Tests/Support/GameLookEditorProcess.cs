using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
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

    /// <summary>The kinds the Place tool starts with chosen (written in its memory): the chest, which has a model.</summary>
    private static readonly string[] ChosenKinds = ["piece_chest_wood"];

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
        : base([], setup.Env, setup.Data)
    {
        _dir = setup.Dir;
        _game = setup.Game;
    }

    /// <summary>
    /// A made-up terrain fragment program shaped as Unity writes one for OpenGL (Unity's names: the zone corners' ocean
    /// depths, the paint mask read at vs_TEXCOORD0), with a small body of its own: what GameShader adapts and the view
    /// links with its own main.
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
        "void main()",
        "{",
        "    vec4 mask = texture(_ClearedMaskTex, vs_TEXCOORD0);",
        "    vec3 ground = texture(_DiffuseArrayTex, vec3(vs_TEXCOORD1.w, vs_TEXCOORD3.w, 0.0) * 0.1).rgb;",
        "    float depth = (_depth[0] + _depth[1] + _depth[2] + _depth[3]) * 0.25;",
        "    SV_Target0 = vec4(mix(mask.rgb, ground, 0.5), 1.0);",
        "    SV_Target1 = vec4(0.04, 0.04, 0.04, 0.2);",
        "    SV_Target2 = vec4(vs_TEXCOORD1.z, vs_TEXCOORD2.z, vs_TEXCOORD3.z, 0.0) * 0.5 + 0.5;",
        "    SV_Target3 = vec4(vec3(0.6) + depth * 0.0, 1.0);",
        "}",
        "#endif",
        "");

    /// <summary>
    /// What a process starts from: the test folder (the world's copy, the home whose Steam library has the game), the
    /// made-up game, the editor's environment and its data folder (the Place tool's chosen kind written in it).
    /// </summary>
    private sealed record Setup(string Dir, TestGame Game, Dictionary<string, string> Env, string Data)
    {
        /// <summary>Makes the test folder and the game, with this terrain shader.</summary>
        public static Setup Make(string fragment)
        {
            string dir = TestWorldCopy.Make("vwe-visual-look-");
            AddTrees(Path.Combine(dir, "CITest"));
            string home = Path.Combine(dir, "home");
            // The Place tool starts with the chest chosen: its model's box is worked out for the panel's fit hint.
            string data = Path.Combine(dir, "data");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "place.json"), JsonSerializer.Serialize(new { Chosen = ChosenKinds }));
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
            return new Setup(dir, game, env, data);
        }
    }

    /// <summary>
    /// Puts 2100 fir trees into the world's first zone, a metre apart north of the middle (clear of the chest the tests
    /// point at), and a row of 60 more in the next zone east: more instances of one model than are drawn at once, in two
    /// of the cells of an area three zones across, so the view draws them by the cells in view.
    /// </summary>
    /// <param name="world">The world's folder.</param>
    private static void AddTrees(string world)
    {
        int fir = StableHash.Of("FirTree");
        var added = new List<NewObject>();
        for (int i = 0; i < 60; i++)
        {
            for (int j = 0; j < 35; j++)
            {
                // At about the ground's height there (the test world's first zone lies around 45 m).
                added.Add(new NewObject(0, fir, new Vector3(-29.5f + i, 46f, -5f + j), Vector3.Zero, 1f));
            }
            // The row 60 m east, past the cell edge of an area three zones across (32 m east of the middle).
            added.Add(new NewObject(0, fir, new Vector3(60f, 46f, -29.5f + i), Vector3.Zero, 1f));
        }
        var result = WorldWriter.Save(WorldSave.Load(world), [], added: added);
        if (!result.Saved)
        {
            throw new InvalidOperationException("the trees could not be written into the test world: " + result.Message);
        }
    }

    /// <summary>
    /// A game with the terrain material (its shader's OpenGL program and its textures), the terrain's texture arrays,
    /// the world map's material and its textures, and models for three kinds of object the test world's first zone
    /// holds and for the game's dungeon rooms (<see cref="Models"/>).
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
        // The world map's material: a one-pixel texture of its own for each picture the map draws with.
        var map = GameLookData.MapTextures.Keys
            .Select((p, i) => (p, Fields.Ref(TestAssets.Texture(b, p.Trim('_'), 1, 1, Rgba32, 1, [(byte)(40 + (i * 25)), 90, (byte)(200 - (i * 20)), 255])), (1.0, 1.0), (0.0, 0.0)));
        b.Contents("assets/map/minimap.mat", TestAssets.Material(b, "minimap", textures: map));
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
        // Unity's Standard shader in its transparent mode: drawn as glass (any other shader in that mode cuts out at half).
        long standard = TestAssets.Shader(b, "Standard", new Dictionary<int, byte[]>());
        long glass = TestAssets.Material(b, "chest_glass", Fields.Ref(standard), floats: [("_Mode", 3)], colors: [("_Color", 0.6, 0.8, 1.0, 0.5)]);
        long cutOut = TestAssets.Material(b, "fir_cutout", textures: [("_MainTex", Fields.Ref(bark), (1, 1), (0, 0))], floats: [("_Mode", 1), ("_Cutoff", 0.4)]);
        // The rock's material is drawn from both sides (_Cull 0), as the game's foliage is.
        long plain = TestAssets.Material(b, "rock_plain", floats: [("_Cull", 0)], colors: [("_Color", 0.5, 0.5, 0.5, 1.0)]);
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
