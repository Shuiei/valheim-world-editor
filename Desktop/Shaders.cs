using System.Numerics;

namespace TerrainEditor.Desktop;

// The 3D view's shaders: GLSL that runs as OpenGL 3.3 and as OpenGL ES 3.0 (the version line is added
// by GlView). Lighting is done in linear colours; the result is turned back to sRGB at the end.
public static class Shaders
{
	public const string TerrainVs = """
		layout(location = 0) in vec3 aPos;
		layout(location = 1) in vec3 aNor;
		layout(location = 2) in vec3 aCol;
		uniform mat4 uViewProj;
		out vec3 vNor; out vec3 vCol; out float vY;
		void main() { vNor = aNor; vCol = aCol; vY = aPos.y; gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

	public const string TerrainFs = """
		in vec3 vNor; in vec3 vCol; in float vY;
		uniform vec3 uSun;
		out vec4 frag;
		void main() {
			float l = 0.35 + 0.8 * max(dot(normalize(vNor), uSun), 0.0);
			frag = vec4(pow(vCol * l, vec3(1.0 / 2.2)), 1.0);
		}
		""";

	public const string ObjectVs = """
		layout(location = 0) in vec3 aPos;
		layout(location = 1) in vec3 aNor;
		layout(location = 2) in vec2 aUv;
		layout(location = 3) in mat4 aModel;
		uniform mat4 uViewProj;
		out vec3 vNor; out vec2 vUv; out vec3 vPos;
		void main() {
			vec4 w = aModel * vec4(aPos, 1.0);
			vNor = mat3(aModel) * aNor; vUv = aUv; vPos = w.xyz;
			gl_Position = uViewProj * w;
		}
		""";

	// Lit like the game look's day (sun, flat ambient, fog), in view space (the sun's z mirrored).
	public const string ObjectFs = GameLookGl.Common + """
		in vec3 vNor; in vec2 vUv; in vec3 vPos;
		uniform sampler2D uMap; uniform int uHasMap;
		uniform vec4 uColor; uniform float uCutoff; uniform vec4 uUv;
		// Ghosts (the Place tool's preview): see-through and tinted.
		uniform float uGhost;
		uniform vec3 uSun, uSunColor, uAmbient, uEye;
		out vec4 frag;
		void main() {
			vec4 c = uColor;
			if (uHasMap == 1) c *= texture(uMap, vUv * uUv.xy + uUv.zw);
			if (c.a < uCutoff) discard;
			vec3 n = normalize(vNor);
			if (!gl_FrontFacing) n = -n;
			vec3 col = c.rgb * (uAmbient + uSunColor * max(dot(n, uSun), 0.0));
			vec3 toCam = uEye - vPos;
			vec3 v = normalize(toCam);
			col += vec3(0.15, 0.25, 0.35) * uGhost;
			frag = vec4(toSRGB(applyFogDir(col, length(toCam), -vec3(v.x, v.y, -v.z), vec3(uSun.x, uSun.y, -uSun.z))), 1.0 - 0.5 * uGhost);
		}
		""";

	public const string LineVs = """
		layout(location = 0) in vec3 aPos;
		uniform mat4 uViewProj;
		void main() { gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

	public const string LineFs = """
		uniform vec4 uColor;
		out vec4 frag;
		void main() { frag = uColor; }
		""";

	public const string WaterVs = """
		layout(location = 0) in vec3 aPos;
		uniform mat4 uViewProj;
		void main() { gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

	public const string WaterFs = """
		out vec4 frag;
		void main() { frag = vec4(0.16, 0.36, 0.52, 0.72); }
		""";

	// A biome's ground colour (linear), close to the game's map colours.
	public static Vector3 BiomeColor(int biome)
	{
		Vector3 s = biome switch
		{
			1 => new(0.42f, 0.60f, 0.27f),     // Meadows
			2 => new(0.38f, 0.34f, 0.24f),     // Swamp
			4 => new(0.86f, 0.89f, 0.93f),     // Mountain
			8 => new(0.24f, 0.34f, 0.20f),     // Black Forest
			16 => new(0.74f, 0.69f, 0.38f),    // Plains
			32 => new(0.42f, 0.20f, 0.14f),    // Ashlands
			64 => new(0.90f, 0.93f, 0.97f),    // Deep North
			256 => new(0.70f, 0.64f, 0.48f),   // Ocean (sand)
			512 => new(0.34f, 0.32f, 0.40f),   // Mistlands
			_ => new(0.5f, 0.5f, 0.5f),
		};
		return new Vector3(MathF.Pow(s.X, 2.2f), MathF.Pow(s.Y, 2.2f), MathF.Pow(s.Z, 2.2f));
	}
}
