using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 3D view's shaders: GLSL that runs as OpenGL 3.3 and as OpenGL ES 3.0 (the version line is added
/// by GlView). Lighting is done in linear colours; the result is turned back to sRGB at the end.
/// </summary>
public static class Shaders
{
    /// <summary>The plain terrain's vertex shader: position, normal and vertex colour.</summary>
    public const string TerrainVs = """
		layout(location = 0) in vec3 aPos;
		layout(location = 1) in vec3 aNor;
		layout(location = 2) in vec3 aCol;
		layout(location = 7) in float aCut;
		uniform mat4 uViewProj;
		out vec3 vNor; out vec3 vCol; out float vY; out float vCut;
		void main() { vNor = aNor; vCol = aCol; vY = aPos.y; vCut = aCut; gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

    /// <summary>
    /// The plain terrain's fragment shader: vertex colour lit by the sun with a fixed ambient; nothing where a mod's
    /// extension cuts the ground (GroundCuts).
    /// </summary>
    public const string TerrainFs = """
		in vec3 vNor; in vec3 vCol; in float vY; in float vCut;
		uniform vec3 uSun;
		out vec4 frag;
		void main() {
			if (vCut > 0.5) discard;
			float l = 0.35 + 0.8 * max(dot(normalize(vNor), uSun), 0.0);
			frag = vec4(pow(vCol * l, vec3(1.0 / 2.2)), 1.0);
		}
		""";

    /// <summary>
    /// The objects' vertex shader, instanced: each instance's placement matrix comes as an attribute, with the
    /// support check's tint carried in its unused corner (zero: none).
    /// </summary>
    public const string ObjectVs = """
		layout(location = 0) in vec3 aPos;
		layout(location = 1) in vec3 aNor;
		layout(location = 2) in vec2 aUv;
		layout(location = 3) in mat4 aModel;
		uniform mat4 uViewProj;
		out vec3 vNor; out vec2 vUv; out vec3 vPos; out vec3 vTint;
		void main() {
			// The support check's tint rides in the placement's unused corner (zero: none).
			mat4 m = aModel;
			vTint = vec3(m[0][3], m[1][3], m[2][3]);
			m[0][3] = 0.0; m[1][3] = 0.0; m[2][3] = 0.0;
			vec4 w = m * vec4(aPos, 1.0);
			vNor = mat3(m) * aNor; vUv = aUv; vPos = w.xyz;
			gl_Position = uViewProj * w;
		}
		""";

    /// <summary>
    /// The objects' fragment shader. Lit like the game look's day (sun, flat ambient, fog), in view space (the
    /// sun's z mirrored); also draws see-through materials (glass) by their alpha, the Place tool's ghosts, see-through
    /// buildings, the Workshop's cut and the support check's tint.
    /// </summary>
    public const string ObjectFs = GameLookGl.Common + """
		in vec3 vNor; in vec2 vUv; in vec3 vPos; in vec3 vTint;
		uniform sampler2D uMap; uniform int uHasMap;
		uniform vec4 uColor; uniform float uCutoff; uniform vec4 uUv;
		// Ghosts (the Place tool's preview): see-through and tinted.
		uniform float uGhost;
		// See-through buildings (View): drawn faint.
		uniform float uSeeThrough;
		// A see-through material (glass): its alpha, the colour's times the texture's, is the fragment's (else opaque).
		uniform float uBlend;
		// The Workshop's cut: x on (1), y the height above which nothing is drawn (not the ghosts).
		uniform vec2 uCut;
		uniform vec3 uSun, uSunColor, uAmbient, uEye;
		out vec4 frag;
		void main() {
			if (uCut.x > 0.5 && uGhost < 0.5 && vPos.y > uCut.y) discard;
			vec4 c = uColor;
			if (uHasMap == 1) c *= texture(uMap, vUv * uUv.xy + uUv.zw);
			if (c.a < uCutoff) discard;
			vec3 n = normalize(vNor);
			if (!gl_FrontFacing) n = -n;
			// The support check, in the game's build-mode colours (WearNTear.Highlight): the colour tinted,
			// glowing a little (the game glows with 0.4 of it, which hides the texture), so the piece
			// still shows what it is.
			bool tinted = vTint != vec3(0.0);
			if (tinted) c.rgb *= mix(vec3(1.0), vTint, 0.75);
			vec3 col = c.rgb * (uAmbient + uSunColor * max(dot(n, uSun), 0.0));
			if (tinted) col += vTint * 0.12;
			vec3 toCam = uEye - vPos;
			vec3 v = normalize(toCam);
			col += vec3(0.15, 0.25, 0.35) * uGhost;
			frag = vec4(toSRGB(applyFogDir(col, length(toCam), -vec3(v.x, v.y, -v.z), vec3(uSun.x, uSun.y, -uSun.z))), (uBlend > 0.5 ? c.a : 1.0) * (1.0 - 0.5 * uGhost) * (1.0 - 0.6 * uSeeThrough));
		}
		""";

    /// <summary>The line drawing's vertex shader (3D view and map overlays): position only.</summary>
    public const string LineVs = """
		layout(location = 0) in vec3 aPos;
		uniform mat4 uViewProj;
		void main() { gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

    /// <summary>The lines' fragment shader: one flat colour.</summary>
    public const string LineFs = """
		uniform vec4 uColor;
		out vec4 frag;
		void main() { frag = uColor; }
		""";

    /// <summary>The new objects' markers: one point each, <c>uSize</c> pixels across.</summary>
    public const string MarkVs = """
		layout(location = 0) in vec3 aPos;
		uniform mat4 uViewProj;
		uniform float uSize;
		void main() { gl_Position = uViewProj * vec4(aPos, 1.0); gl_PointSize = uSize; }
		""";

    /// <summary>
    /// The markers' fragment shader: a round dot of one colour, its edge faded over about a pixel so a
    /// small dot stays round rather than jagged.
    /// </summary>
    public const string MarkFs = """
		uniform vec4 uColor;
		out vec4 frag;
		void main()
		{
			vec2 d = gl_PointCoord * 2.0 - 1.0;
			float r = length(d);
			float w = fwidth(r);
			float a = 1.0 - smoothstep(1.0 - w, 1.0, r);
			if (a <= 0.0) discard;
			frag = vec4(uColor.rgb, uColor.a * a);
		}
		""";

    /// <summary>The water plane's vertex shader: position only.</summary>
    public const string WaterVs = """
		layout(location = 0) in vec3 aPos;
		uniform mat4 uViewProj;
		void main() { gl_Position = uViewProj * vec4(aPos, 1.0); }
		""";

    /// <summary>The water plane's fragment shader: one fixed see-through blue.</summary>
    public const string WaterFs = """
		out vec4 frag;
		void main() { frag = vec4(0.16, 0.36, 0.52, 0.72); }
		""";

    /// <summary>A biome's ground colour (linear), close to the game's map colours; grey for an unknown biome.</summary>
    /// <param name="biome">A Heightmap.Biome value.</param>
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
