using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// The locations' icons on the map, as the game's map shows them (the start, the traders...): the minimap's own icons
/// from the player's Valheim, in one texture, drawn over the map at a fixed size on screen.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>An icon's place in the icons' texture (top row first) and its picture's size in pixels.</summary>
    /// <param name="U0">Its left edge.</param>
    /// <param name="V0">Its top edge.</param>
    /// <param name="U1">Its right edge.</param>
    /// <param name="V1">Its bottom edge.</param>
    /// <param name="Width">Its width.</param>
    /// <param name="Height">Its height.</param>
    internal readonly record struct IconSlot(float U0, float V0, float U1, float V1, int Width, int Height);

    /// <summary>An icon's height on screen at its file's size 1, in device pixels.</summary>
    private const float IconPixels = 40f;

    /// <summary>The minimap's location icons as decoded for the last bundles reader asked for.</summary>
    private static readonly KeptRead<GameBundles, IReadOnlyDictionary<string, GameLookData.Picture>> IconPictures = new(GameLookData.ReadLocationIcons);

    /// <summary>Each icon's slot in the icons' texture, by name; none until the textures are loaded.</summary>
    private Dictionary<string, IconSlot> _iconSlots = [];

    /// <summary>The shown world's location icons.</summary>
    private List<LocationIcons.Icon> _icons = [];

    /// <summary>The icons' program, its vertex array and buffer.</summary>
    private uint _iconProg, _iconVao, _iconVbo;

    /// <summary>
    /// Shows a world's icons once the game's location rules are read (the kinds say which have icons): none until then.
    /// </summary>
    private void ShowIcons(WorldSession session)
    {
        _icons = [];
        GameLocations.UseGameRules(GameLook.Bundles).ContinueWith(t =>
        {
            if (t.Exception is { } e)
            {
                Log.Caught(e.GetBaseException(), "reading the game's location rules for the map's icons (only the world's own rules give icons)");
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (_session == session)
                {
                    _icons = IconsOf(session);
                    RequestNextFrameRendering();
                }
            });
        }, TaskScheduler.Default);
    }

    /// <summary>The icons a world shows: its location instances' (its zone file), by its kinds' rules.</summary>
    private static List<LocationIcons.Icon> IconsOf(WorldSession session) =>
        session.World.Zones is { } zones ? LocationIcons.Of(zones.Locations, SeedLocations.RulesFor(session.Terrain.Generator).Locations) : [];

    /// <summary>
    /// Puts the minimap's icons side by side in one texture (none when they cannot be read: no icon is drawn), and
    /// keeps where each one is.
    /// </summary>
    private unsafe void LoadIconTexture()
    {
        IReadOnlyDictionary<string, GameLookData.Picture> pictures = new Dictionary<string, GameLookData.Picture>();
        try
        {
            if (GameLook.Bundles is { } game)
            {
                pictures = IconPictures.Of(game);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
        {
            Log.Caught(e, "reading the game's location icons");
        }
        int width = Math.Max(1, pictures.Values.Sum(p => p.Width)), height = Math.Max(1, pictures.Values.Select(p => p.Height).DefaultIfEmpty(1).Max());
        byte[] all = new byte[width * height * 4];
        var slots = new Dictionary<string, IconSlot>(StringComparer.Ordinal);
        int x = 0;
        foreach (var (name, p) in pictures)
        {
            for (int r = 0; r < p.Height; r++)
            {
                Array.Copy(p.Rgba, r * p.Width * 4, all, (r * width + x) * 4, p.Width * 4);
            }
            slots[name] = new IconSlot((float)x / width, 0f, (float)(x + p.Width) / width, (float)p.Height / height, p.Width, p.Height);
            x += p.Width;
        }
        _iconSlots = slots;
        Texture("uIcons", _ =>
        {
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* px = all)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Srgb8Alpha8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, px);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        });
    }

    /// <summary>
    /// The icons as quads (two triangles each; x, z, u, v per corner), centred on their locations: their file's size
    /// times <see cref="IconPixels"/> on screen under 5, else that many metres wide on the map; an icon of no slot
    /// (the minimap has no icon of that name) is left out.
    /// </summary>
    /// <param name="icons">The icons.</param>
    /// <param name="slots">The icons' slots, by name.</param>
    /// <param name="mpp">Metres per device pixel.</param>
    internal static float[] IconQuads(IReadOnlyList<LocationIcons.Icon> icons, IReadOnlyDictionary<string, IconSlot> slots, float mpp)
    {
        var data = new List<float>(icons.Count * 24);
        foreach (var icon in icons)
        {
            if (!slots.TryGetValue(icon.Name, out var s))
            {
                continue;
            }
            float aspect = (float)s.Width / s.Height;
            float halfH = icon.Size >= 5f ? icon.Size / 2f / aspect : IconPixels * icon.Size * mpp / 2f, halfW = halfH * aspect;
            float x0 = icon.X - halfW, x1 = icon.X + halfW, z0 = icon.Z - halfH, z1 = icon.Z + halfH;
            // The top of the picture (V0) at the top of the map (the larger z).
            data.AddRange([x0, z1, s.U0, s.V0, x1, z1, s.U1, s.V0, x1, z0, s.U1, s.V1, x0, z1, s.U0, s.V0, x1, z0, s.U1, s.V1, x0, z0, s.U0, s.V1]);
        }
        return [.. data];
    }

    /// <summary>Draws the locations' icons over the map (alpha blended).</summary>
    private unsafe void DrawIcons(int w, int h, float mpp)
    {
        float[] data = IconQuads(_icons, _iconSlots, mpp);
        if (data.Length == 0 || !_tex.TryGetValue("uIcons", out var t))
        {
            return;
        }
        _gl.UseProgram(_iconProg);
        _gl.Uniform4(_u.At(_iconProg, "uView"), Center.X, Center.Y, 2 / (w * mpp), 2 / (h * mpp));
        _gl.Uniform1(_u.At(_iconProg, "uIcons"), t.Unit);
        _gl.BindVertexArray(_iconVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _iconVbo);
        fixed (float* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.StreamDraw);
        }
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(data.Length / 4));
    }

    /// <summary>Makes the icons' program and its vertex array (x, z then u, v per corner).</summary>
    private unsafe void InitIcons()
    {
        _iconProg = Program(IconVs, IconFs);
        _iconVao = _own.VertexArray();
        _iconVbo = _own.Buffer();
        _gl.BindVertexArray(_iconVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _iconVbo);
        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 16, (void*)8);
        _gl.BindVertexArray(0);
    }

    /// <summary>An icon's corners in world metres, with their place in the icons' texture.</summary>
    private const string IconVs = """
		layout(location = 0) in vec2 aPos;
		layout(location = 1) in vec2 aUv;
		uniform vec4 uView;
		out vec2 vUv;
		void main() { gl_Position = vec4((aPos - uView.xy) * uView.zw, 0.0, 1.0); vUv = aUv; }
		""";

    /// <summary>An icon's pixels: the texture's colour, sRGB encoded as the map's own.</summary>
    private const string IconFs = """
		in vec2 vUv;
		uniform sampler2D uIcons;
		out vec4 frag;
		void main() {
		  vec4 c = texture(uIcons, vUv);
		  vec3 srgb = mix(c.rgb * 12.92, 1.055 * pow(c.rgb, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c.rgb));
		  frag = vec4(srgb, c.a);
		}
		""";
}
