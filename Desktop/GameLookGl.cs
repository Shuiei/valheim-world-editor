using System.Numerics;
using SkiaSharp;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The game's look, as the web editor's terrain/look.js draws it: Valheim's own terrain shader
// (heightmap.frag.glsl, taken from the game by the game-look copy) lit like the game's deferred
// renderer, the ocean and a sky with clouds, at midday in the default "Clear" weather. Inside the
// shaders everything is in Unity's left-handed world space: view (x, y, z) is Unity (x + Cx, y, Cz - z).
public sealed class GameLookGl
{
	// ---- Files, read on a worker thread (Read), then sent to the graphics card (Init).
	public sealed record Files(string Fragment, Image DiffuseArray, Image NormalArray, Image Noise, Image Cliff, Image MistCliff,
		Image Paved, Image Rock, Image Snow, Image Variety);
	public sealed record Image(int Width, int Height, byte[] Rgba);

	public static string? Folder()
	{
		foreach (string d in new[] { Path.Combine(GameLook.Dir, "terrain"), Path.Combine(AppContext.BaseDirectory, "wwwroot", "terrain") })
		{
			if (File.Exists(Path.Combine(d, "heightmap.frag.glsl")) && File.Exists(Path.Combine(d, "d_array.png")))
			{
				return d;
			}
		}
		return null;
	}

	// The terrain textures with row 0 at the bottom (Unity's origin); a texture array is a vertical
	// strip of square slices, each flipped on its own.
	public static Files? Read()
	{
		string? dir = Folder();
		if (dir == null)
		{
			return null;
		}
		Image Load(string name, bool flip = true, bool slices = false)
		{
			using var codec = SKCodec.Create(Path.Combine(dir, name)) ?? throw new InvalidOperationException($"{name} cannot be read");
			var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
			byte[] px = new byte[info.BytesSize];
			unsafe
			{
				fixed (byte* p = px)
				{
					codec.GetPixels(info, (IntPtr)p);
				}
			}
			int w = info.Width, h = info.Height, row = w * 4;
			int slice = slices ? w : h, n = h / slice;
			byte[] outPx = new byte[px.Length];
			for (int k = 0; k < n; k++)
			{
				for (int y = 0; y < slice; y++)
				{
					System.Buffer.BlockCopy(px, (k * slice + y) * row, outPx, (k * slice + (flip ? slice - 1 - y : y)) * row, row);
				}
			}
			return new Image(w, h, outPx);
		}
		return new Files(File.ReadAllText(Path.Combine(dir, "heightmap.frag.glsl")), Load("d_array.png", slices: true), Load("n_array.png", slices: true),
			Load("noise.png"), Load("cliff_n.png"), Load("mistcliff_n.png"), Load("paved_n.png"), Load("rock_n.png"), Load("snow_n.png"), Load("variety.png"));
	}

	// ---- Midday in the game's default "Clear" environment (EnvMan.m_environments), like look.js DAY:
	// the game's gamma-space colours turned linear.
	private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
	private static Vector3 Srgb(float r, float g, float b) => new(Lin(r), Lin(g), Lin(b));
	public static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-0.42f, 0.72f, 0.55f));
	public static readonly Vector3 SunColor = Srgb(1.0f, 0.886f, 0.742f) * 1.7f;
	public static readonly Vector3 Ambient = Srgb(0.463f, 0.574f, 0.706f);
	public static readonly Vector3 FogColor = Srgb(0.302f, 0.581f, 0.747f);
	public static readonly Vector3 SunFogColor = Srgb(0.801f, 0.687f, 0.43f);
	public const float FogDensity = 0.003f;
	public static readonly Vector3 SkyTop = Srgb(0.2f, 0.42f, 0.68f);
	public static readonly Vector3 SkyHorizon = Srgb(0.55f, 0.72f, 0.84f);
	// The sun in view space (z mirrored), for the objects' simpler lighting.
	public static Vector3 SunDirView => new(SunDir.X, SunDir.Y, -SunDir.Z);

	public const string Common = """
		uniform vec3 uFogColor;
		uniform vec3 uSunFogColor;
		uniform float uFogDensity;
		// Unity exponential-squared fog; looking towards the sun the fog takes the sun fog colour.
		vec3 fogColorFor(vec3 viewDir, vec3 sunDir) {
		  return mix(uFogColor, uSunFogColor, pow(max(dot(viewDir, sunDir), 0.0), 6.0));
		}
		vec3 applyFogDir(vec3 c, float dist, vec3 viewDir, vec3 sunDir) {
		  float f = 1.0 - exp(-pow(dist * uFogDensity, 2.0));
		  return mix(c, fogColorFor(viewDir, sunDir), clamp(f, 0.0, 1.0));
		}
		vec3 toSRGB(vec3 c) {
		  c = max(c, vec3(0.0));
		  // Filmic shoulder so the sun-lit snow does not clip hard.
		  c = c / (1.0 + max(c - 0.8, 0.0));
		  return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
		}
		""";

	private const string TerrainVs = """
		layout(location = 0) in vec3 position;
		layout(location = 1) in vec3 normal;
		layout(location = 3) in vec4 biomeColor;
		layout(location = 4) in vec2 maskUV;
		layout(location = 5) in float oceanDepth;
		layout(location = 6) in float limitTint;
		uniform mat4 uViewProj;
		uniform vec3 uOffset;
		uniform vec3 _WorldSpaceCameraPos;
		out float vs_depth;
		out vec2 vs_maskUV;
		out vec4 vs_TEXCOORD1;
		out vec4 vs_TEXCOORD2;
		out vec4 vs_TEXCOORD3;
		out vec4 vs_COLOR0;
		out vec3 vs_TEXCOORD4;
		out vec3 vs_TEXCOORD6;
		out float vLimit;
		void main() {
		  vec3 wp = vec3(position.x + uOffset.x, position.y, uOffset.z - position.z);
		  vec3 n = normalize(vec3(normal.x, normal.y, -normal.z));
		  // Mesh.RecalculateTangents with uv = (x, z) / 64: tangent along +x, bitangent along +z.
		  vec3 t = normalize(vec3(1.0, 0.0, 0.0) - n * n.x);
		  vec3 b = -cross(n, t);
		  vs_TEXCOORD1 = vec4(t.x, b.x, n.x, wp.x);
		  vs_TEXCOORD2 = vec4(t.y, b.y, n.y, wp.y);
		  vs_TEXCOORD3 = vec4(t.z, b.z, n.z, wp.z);
		  vs_TEXCOORD4 = _WorldSpaceCameraPos - wp;
		  vs_TEXCOORD6 = vec3(0.0);
		  vs_COLOR0 = biomeColor;
		  vs_depth = oceanDepth;
		  vs_maskUV = maskUV;
		  vLimit = limitTint;
		  gl_Position = uViewProj * vec4(position, 1.0);
		}
		""";

	private const string TerrainMain = """
		uniform vec3 uSunDir;
		uniform vec3 uSunColor;
		uniform float uSlope;
		uniform float uContour;
		in float vLimit;
		out vec4 fragColor;
		void main() {
		  valheimGBuffer();
		  vec3 albedo = SV_Target0.rgb;
		  vec3 specColor = SV_Target1.rgb;
		  float smoothness = SV_Target1.a;
		  vec3 n = normalize(SV_Target2.xyz * 2.0 - 1.0);
		  vec3 ambient = -log2(max(SV_Target3.rgb, vec3(1e-6)));
		  vec3 wp = vec3(vs_TEXCOORD1.w, vs_TEXCOORD2.w, vs_TEXCOORD3.w);
		  vec3 v = normalize(_WorldSpaceCameraPos - wp);
		  vec3 l = uSunDir, h = normalize(l + v);
		  // Unity's BRDF1 (Standard shader, deferred lighting pass).
		  float nl = max(dot(n, l), 0.0), nv = abs(dot(n, v)) + 1e-4, nh = max(dot(n, h), 0.0), lh = max(dot(l, h), 0.0);
		  float rough = max((1.0 - smoothness) * (1.0 - smoothness), 0.002);
		  float lambdaV = nl * (nv * (1.0 - rough) + rough), lambdaL = nv * (nl * (1.0 - rough) + rough);
		  float vis = 0.5 / (lambdaV + lambdaL + 1e-5);
		  float a2 = rough * rough, d = (nh * a2 - nh) * nh + 1.0;
		  float spec = max(0.0, vis * (a2 / (d * d + 1e-7)) * nl);
		  vec3 fres = specColor + (1.0 - specColor) * pow(1.0 - lh, 5.0);
		  vec3 col = ambient + (albedo * nl + spec * fres) * uSunColor;
		  if (uSlope > 0.5) {
		    vec3 ng = normalize(vec3(vs_TEXCOORD1.z, vs_TEXCOORD2.z, vs_TEXCOORD3.z));
		    float deg = degrees(acos(clamp(ng.y, 0.0, 1.0)));
		    vec3 c = deg < 10.0 ? vec3(0.25, 0.75, 0.3) : deg < 20.0 ? vec3(0.7, 0.8, 0.2) : deg < 30.0 ? vec3(0.95, 0.7, 0.15) : deg < 45.0 ? vec3(0.95, 0.4, 0.1) : vec3(0.85, 0.1, 0.1);
		    col = mix(col, c * (0.45 + 0.55 * max(dot(ng, uSunDir), 0.0)), 0.6);
		  }
		  if (uContour > 0.0) {
		    float hh = wp.y, fw = fwidth(hh) + 1e-4;
		    float minor = abs(fract(hh / uContour + 0.5) - 0.5) * uContour;
		    float major = abs(fract(hh / (uContour * 5.0) + 0.5) - 0.5) * uContour * 5.0;
		    float line = max((1.0 - smoothstep(0.0, fw * 1.2, minor)) * 0.45, (1.0 - smoothstep(0.0, fw * 2.0, major)) * 0.8);
		    col = mix(col, vec3(0.02), line);
		  }
		  // Points at the game's +-8 m edit limit.
		  col = mix(col, vec3(0.75, 0.06, 0.04), vLimit * 0.55);
		  fragColor = vec4(toSRGB(applyFogDir(col, length(_WorldSpaceCameraPos - wp), -v, uSunDir)), 1.0);
		}
		""";

	private const string SkyVs = """
		layout(location = 0) in vec3 position;
		uniform mat4 uViewProj;
		uniform vec3 uEye;
		out vec3 vDir;
		void main() { vDir = position; gl_Position = (uViewProj * vec4(position + uEye, 1.0)).xyww; }
		""";

	private const string SkyFs = """
		uniform vec3 uTop, uHorizon, uSunDir, uSunColor;
		uniform vec4 _Time;
		in vec3 vDir;
		out vec4 fragColor;
		float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
		float vnoise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
		  return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
		float fbm(vec2 p) { float s = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { s += a * vnoise(p); p *= 2.03; a *= 0.5; } return s; }
		void main() {
		  vec3 d = normalize(vDir);
		  vec3 du = vec3(d.x, d.y, -d.z);
		  float up = max(d.y, 0.0);
		  vec3 col = mix(uHorizon, uTop, pow(up, 0.55));
		  vec3 fc = fogColorFor(du, uSunDir);
		  if (d.y < 0.0) col = mix(uHorizon, fc, min(1.0, -d.y * 4.0));
		  // Clouds on a flat layer.
		  if (d.y > 0.01) {
		    vec2 uv = du.xz / (d.y + 0.08) * 1.6 + vec2(_Time.y * 0.004, _Time.y * 0.002);
		    float c = smoothstep(0.5, 0.78, fbm(uv));
		    col = mix(col, vec3(0.92, 0.93, 0.95) * (0.75 + 0.25 * up), c * smoothstep(0.01, 0.2, d.y) * 0.85);
		  }
		  float s = max(dot(du, uSunDir), 0.0);
		  col += uSunColor * (pow(s, 1800.0) * 6.0 + pow(s, 12.0) * 0.12);
		  col = mix(col, fc, (1.0 - smoothstep(0.0, 0.12, abs(d.y))) * 0.7);
		  fragColor = vec4(toSRGB(col), 1.0);
		}
		""";

	private const string WaterVs = """
		layout(location = 0) in vec3 position;
		uniform mat4 uViewProj;
		uniform vec3 uOffset;
		out vec3 vWorld;
		void main() { vWorld = vec3(position.x + uOffset.x, position.y, uOffset.z - position.z); gl_Position = uViewProj * vec4(position, 1.0); }
		""";

	private const string WaterFs = """
		uniform sampler2D uHeight;
		uniform vec4 uRegion;
		uniform float uWater;
		uniform vec3 uTop, uHorizon, uSunDir, uSunColor, _WorldSpaceCameraPos;
		uniform vec4 _Time;
		in vec3 vWorld;
		out vec4 fragColor;
		vec2 wave(vec2 p, vec2 dir, float freq, float speed) { float x = dot(p, dir) * freq + _Time.y * speed; return dir * cos(x) * freq; }
		void main() {
		  vec2 p = vWorld.xz;
		  vec2 g = wave(p, normalize(vec2(1.0, 0.3)), 0.21, 1.1) * 0.22 + wave(p, normalize(vec2(-0.4, 1.0)), 0.37, 1.6) * 0.12
		         + wave(p, normalize(vec2(0.7, -0.7)), 0.83, 2.3) * 0.04 + wave(p, normalize(vec2(-0.9, -0.2)), 1.9, 3.1) * 0.015;
		  vec3 n = normalize(vec3(-g.x, 1.0, -g.y));
		  vec3 toCam = _WorldSpaceCameraPos - vWorld;
		  float dist = length(toCam);
		  vec3 v = toCam / dist;
		  vec2 uv = (p - uRegion.xy + 0.5) / uRegion.zw;
		  bool inside = all(greaterThan(uv, vec2(0.0))) && all(lessThan(uv, vec2(1.0)));
		  float ground = inside ? texture(uHeight, uv).r : uWater - 60.0;
		  float depth = max(uWater - ground, 0.0);
		  // Light travels down and back up: shallow water shows the bottom, deep water is dark blue.
		  float viewDepth = depth / max(v.y, 0.15);
		  vec3 shallow = vec3(0.23, 0.42, 0.36), deep = vec3(0.012, 0.055, 0.085);
		  vec3 body = mix(shallow, deep, 1.0 - exp(-viewDepth * 0.16));
		  body *= 0.55 + 0.45 * max(dot(n, uSunDir), 0.0);
		  vec3 r = reflect(-v, n); r.y = abs(r.y);
		  vec3 skyCol = mix(uHorizon, uTop, pow(max(r.y, 0.0), 0.55));
		  float fres = 0.02 + 0.98 * pow(1.0 - max(dot(n, v), 0.0), 5.0);
		  vec3 col = mix(body, skyCol, fres);
		  vec3 h = normalize(uSunDir + v);
		  col += uSunColor * (pow(max(dot(n, h), 0.0), 2000.0) * 1.5 + pow(max(dot(n, h), 0.0), 120.0) * 0.08);
		  // Foam where the water meets the shore.
		  float foam = (1.0 - smoothstep(0.0, 0.35, depth)) * (0.6 + 0.4 * sin(_Time.y * 1.3 + dot(p, vec2(0.6, 0.8)) * 1.7));
		  col = mix(col, vec3(0.8, 0.85, 0.85), foam * (inside ? 0.3 : 0.0));
		  float alpha = inside ? smoothstep(0.0, 0.35, depth) : 1.0;
		  fragColor = vec4(toSRGB(applyFogDir(col, dist, -v, uSunDir)), alpha);
		}
		""";

	// ---- On the graphics card.
	private GL _gl = null!;
	private uint _terrain, _sky, _water;
	private uint _skyVao, _skyCount, _waterVao;
	private readonly Dictionary<string, (uint Tex, TextureTarget Target)> _tex = new();
	private uint _maskTex, _heightTex;
	private WorldScene _scene = null!;

	public void Init(GL gl, Func<string, string, uint> program, Files f, WorldScene s)
	{
		_gl = gl;
		_scene = s;
		// The extracted shader brings its own precision lines (GLSL ES); desktop OpenGL ignores them.
		_terrain = program(TerrainVs, f.Fragment.Replace("#version 300 es", "") + Common + TerrainMain);
		_sky = program(SkyVs, Common + SkyFs);
		_water = program(WaterVs, Common + WaterFs);
		_tex["_DiffuseArrayTex"] = (Array2D(f.DiffuseArray, srgb: true), TextureTarget.Texture2DArray);
		_tex["_NormalArrayTex"] = (Array2D(f.NormalArray, srgb: false), TextureTarget.Texture2DArray);
		_tex["_NoiseTex"] = (Tex2D(f.Noise), TextureTarget.Texture2D);
		_tex["_CliffNormal"] = (Tex2D(f.Cliff), TextureTarget.Texture2D);
		_tex["_MistlandsCliffNormal"] = (Tex2D(f.MistCliff), TextureTarget.Texture2D);
		_tex["_PavedNormal"] = (Tex2D(f.Paved), TextureTarget.Texture2D);
		_tex["_RockNormal"] = (Tex2D(f.Rock), TextureTarget.Texture2D);
		_tex["_SnowNormal"] = (Tex2D(f.Snow), TextureTarget.Texture2D);
		_tex["_ColorVarietyNoise"] = (Tex2D(f.Variety), TextureTarget.Texture2D);
		_tex["_SkyAlphaTexture"] = (Tex2D(new Image(1, 1, new byte[] { 255, 255, 255, 255 })), TextureTarget.Texture2D);
		_tex["_CurlNoise"] = (Noise3D(32, 7), TextureTarget.Texture3D);
		_tex["_SnowGlintOffsetNoise"] = (Noise3D(32, 11), TextureTarget.Texture3D);
		_maskTex = Tex2D(new Image(s.W, s.H, s.Mask), mipmaps: false, repeat: false);
		_tex["_ClearedMaskTex"] = (_maskTex, TextureTarget.Texture2D);
		_heightTex = HeightTexture(s);
		BuildSky();
		BuildWater(s);
	}

	private unsafe uint Tex2D(Image img, bool mipmaps = true, bool repeat = true, bool srgb = false)
	{
		uint t = _gl.GenTexture();
		_gl.BindTexture(TextureTarget.Texture2D, t);
		fixed (byte* p = img.Rgba)
		{
			_gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
		}
		if (mipmaps)
		{
			_gl.GenerateMipmap(TextureTarget.Texture2D);
		}
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(mipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
		var wrap = (int)(repeat ? TextureWrapMode.Repeat : TextureWrapMode.ClampToEdge);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, wrap);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, wrap);
		return t;
	}

	private unsafe uint Array2D(Image img, bool srgb)
	{
		int s = img.Width, n = img.Height / s;
		uint t = _gl.GenTexture();
		_gl.BindTexture(TextureTarget.Texture2DArray, t);
		fixed (byte* p = img.Rgba)
		{
			_gl.TexImage3D(TextureTarget.Texture2DArray, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)s, (uint)s, (uint)n, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
		}
		_gl.GenerateMipmap(TextureTarget.Texture2DArray);
		_gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
		_gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
		_gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
		_gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
		return t;
	}

	// Stand-ins for the game's global 3D noise textures (_CurlNoise, _SnowGlintOffsetNoise): random
	// bytes, the same generator and seeds as look.js.
	private unsafe uint Noise3D(int size, uint seed)
	{
		byte[] data = new byte[size * size * size * 4];
		uint a = seed;
		for (int i = 0; i < data.Length; i++)
		{
			a += 0x6D2B79F5;
			uint t = a;
			t = (t ^ (t >> 15)) * (t | 1);
			t ^= t + (t ^ (t >> 7)) * (t | 61);
			data[i] = (byte)(((t ^ (t >> 14)) / 4294967296.0) * 256);
		}
		uint tex = _gl.GenTexture();
		_gl.BindTexture(TextureTarget.Texture3D, tex);
		fixed (byte* p = data)
		{
			_gl.TexImage3D(TextureTarget.Texture3D, 0, InternalFormat.Rgba8, (uint)size, (uint)size, (uint)size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
		}
		_gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
		_gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
		foreach (var w in new[] { TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT, TextureParameterName.TextureWrapR })
		{
			_gl.TexParameter(TextureTarget.Texture3D, w, (int)TextureWrapMode.Repeat);
		}
		return tex;
	}

	// The ground's height per grid point (half floats: filterable everywhere), for the water's depth.
	private unsafe uint HeightTexture(WorldScene s)
	{
		Half[] h = s.Heights.Select(v => (Half)v).ToArray();
		uint t = _gl.GenTexture();
		_gl.BindTexture(TextureTarget.Texture2D, t);
		fixed (Half* p = h)
		{
			_gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f, (uint)s.W, (uint)s.H, 0, PixelFormat.Red, PixelType.HalfFloat, p);
		}
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
		return t;
	}

	private unsafe void BuildSky()
	{
		// A sphere around the camera (drawn at the far plane), 48 × 24 segments like look.js.
		const int segW = 48, segH = 24;
		const float r = 2000;
		var v = new List<float>();
		var idx = new List<uint>();
		for (int y = 0; y <= segH; y++)
		{
			for (int x = 0; x <= segW; x++)
			{
				float u = x / (float)segW, vv = y / (float)segH;
				v.Add(-r * MathF.Cos(u * MathF.Tau) * MathF.Sin(vv * MathF.PI));
				v.Add(r * MathF.Cos(vv * MathF.PI));
				v.Add(r * MathF.Sin(u * MathF.Tau) * MathF.Sin(vv * MathF.PI));
			}
		}
		for (int y = 0; y < segH; y++)
		{
			for (int x = 0; x < segW; x++)
			{
				uint a = (uint)(y * (segW + 1) + x), b = a + segW + 1;
				idx.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
			}
		}
		_skyVao = Mesh(v.ToArray(), idx.ToArray());
		_skyCount = (uint)idx.Count;
	}

	private void BuildWater(WorldScene s)
	{
		float half = 4000, y = s.Water;
		_waterVao = Mesh(new[] { -half, y, -half, half, y, -half, half, y, half, -half, y, half }, new uint[] { 0, 2, 1, 0, 3, 2 });
	}

	private unsafe uint Mesh(float[] v, uint[] idx)
	{
		uint vao = _gl.GenVertexArray();
		_gl.BindVertexArray(vao);
		uint vbo = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
		fixed (float* p = v)
		{
			_gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(v.Length * 4), p, BufferUsageARB.StaticDraw);
		}
		uint ebo = _gl.GenBuffer();
		_gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
		fixed (uint* p = idx)
		{
			_gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idx.Length * 4), p, BufferUsageARB.StaticDraw);
		}
		_gl.EnableVertexAttribArray(0);
		_gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
		_gl.BindVertexArray(0);
		return vao;
	}

	// ---- Drawing. eye: the camera in view space; time: seconds since the start (water, clouds, glints).
	private unsafe void Shared(uint prog, Matrix4x4 vp, Vector3 eye, float time)
	{
		_gl.UseProgram(prog);
		void V3(string n, Vector3 v) => _gl.Uniform3(_gl.GetUniformLocation(prog, n), v.X, v.Y, v.Z);
		_gl.UniformMatrix4(_gl.GetUniformLocation(prog, "uViewProj"), 1, false, (float*)&vp);
		V3("uFogColor", FogColor);
		V3("uSunFogColor", SunFogColor);
		_gl.Uniform1(_gl.GetUniformLocation(prog, "uFogDensity"), FogDensity);
		V3("uSunDir", SunDir);
		V3("uSunColor", SunColor);
		V3("uOffset", new Vector3(_scene.Cx, 0, _scene.Cz));
		V3("_WorldSpaceCameraPos", new Vector3(eye.X + _scene.Cx, eye.Y, _scene.Cz - eye.Z));
		V3("uEye", eye);
		V3("uTop", SkyTop);
		V3("uHorizon", SkyHorizon);
		_gl.Uniform4(_gl.GetUniformLocation(prog, "_Time"), time / 20, time, time * 2, time * 3);
	}

	public unsafe void DrawSky(Matrix4x4 vp, Vector3 eye, float time)
	{
		Shared(_sky, vp, eye, time);
		_gl.DepthMask(false);
		_gl.Disable(EnableCap.CullFace);
		_gl.BindVertexArray(_skyVao);
		_gl.DrawElements(PrimitiveType.Triangles, _skyCount, DrawElementsType.UnsignedInt, (void*)0);
		_gl.DepthMask(true);
	}

	// Binds the terrain shader with its textures and settings; the caller draws the ground mesh.
	public void UseTerrain(Matrix4x4 vp, Vector3 eye, float time, bool slope, float contour)
	{
		Shared(_terrain, vp, eye, time);
		void F(string n, float v) => _gl.Uniform1(_gl.GetUniformLocation(_terrain, n), v);
		var shr = new[] { Ambient.X, Ambient.Y, Ambient.Z };
		string[] sh = { "unity_SHAr", "unity_SHAg", "unity_SHAb" };
		for (int c = 0; c < 3; c++)
		{
			_gl.Uniform4(_gl.GetUniformLocation(_terrain, sh[c]), 0, 0, 0, shr[c]);
		}
		_gl.Uniform3(_gl.GetUniformLocation(_terrain, "_SkyAlphaPosition"), 0, -1e5f, 0);
		F("_SnowGlintSize", 0.2f); F("_SnowGlintDensity", 0.15f); F("_SnowGlintStrength", 4);
		F("_SnowGlintNoiseDensity", 1); F("_SnowGlintNoiseAmount", 80); F("_SnowGlintViewAmount", 0.5f); F("_SnowGlintTimeAmount", 3);
		_gl.Uniform3(_gl.GetUniformLocation(_terrain, "_AshlandsVariationCol"), 0.4104f, 0.4838f, 0.5472f);
		F("_Glossiness", 0.1f); F("_SnowGloss", 1); F("_RockGloss", 0.7f); F("_Metallic", 0);
		F("_WaterLevel", _scene.Water); F("_LodHideDistance", 1e6f); F("_LodHideModifier", 0);
		F("_UVScale", 0.5f); F("_BumpScale", 1); F("_Wet", 0);
		F("uSlope", slope ? 1 : 0); F("uContour", contour);
		int unit = 0;
		foreach (var (name, (tex, target)) in _tex)
		{
			_gl.ActiveTexture(TextureUnit.Texture0 + unit);
			_gl.BindTexture(target, tex);
			_gl.Uniform1(_gl.GetUniformLocation(_terrain, name), unit);
			unit++;
		}
		_gl.ActiveTexture(TextureUnit.Texture0);
	}

	public unsafe void DrawWater(Matrix4x4 vp, Vector3 eye, float time)
	{
		Shared(_water, vp, eye, time);
		_gl.ActiveTexture(TextureUnit.Texture0);
		_gl.BindTexture(TextureTarget.Texture2D, _heightTex);
		_gl.Uniform1(_gl.GetUniformLocation(_water, "uHeight"), 0);
		_gl.Uniform4(_gl.GetUniformLocation(_water, "uRegion"), _scene.X0 * 64f - 32f, _scene.Z0 * 64f - 32f, _scene.W, _scene.H);
		_gl.Uniform1(_gl.GetUniformLocation(_water, "uWater"), _scene.Water);
		_gl.Disable(EnableCap.CullFace);
		_gl.Enable(EnableCap.Blend);
		_gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
		_gl.DepthMask(false);
		_gl.BindVertexArray(_waterVao);
		_gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
		_gl.DepthMask(true);
		_gl.Disable(EnableCap.Blend);
	}
}
