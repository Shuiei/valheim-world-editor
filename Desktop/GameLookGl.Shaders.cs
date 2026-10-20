namespace TerrainEditor.Desktop;

/// <summary>
/// The GLSL the look's shaders are built from: the shared fog and colour code, and the terrain, sky and water programs.
/// </summary>
public sealed partial class GameLookGl
{
    /// <summary>
    /// GLSL shared by the terrain, sky, water and object shaders: the fog (applyFogDir, fogColorFor) and toSRGB (linear
    /// to the screen's sRGB with a soft shoulder).
    /// </summary>
    public const string Common = """
		uniform vec3 uFogColor;
		uniform vec3 uSunFogColor;
		uniform float uFogDensity;
		// Where the fog starts (m from the camera): the point the camera turns around, so the area
		// worked on stays clear however far the view is zoomed out; only what lies beyond it fades.
		uniform float uFogStart;
		// Unity exponential-squared fog; looking towards the sun the fog takes the sun fog colour.
		vec3 fogColorFor(vec3 viewDir, vec3 sunDir) {
		  return mix(uFogColor, uSunFogColor, pow(max(dot(viewDir, sunDir), 0.0), 6.0));
		}
		vec3 applyFogDir(vec3 c, float dist, vec3 viewDir, vec3 sunDir) {
		  float f = 1.0 - exp(-pow(max(dist - uFogStart, 0.0) * uFogDensity, 2.0));
		  return mix(c, fogColorFor(viewDir, sunDir), clamp(f, 0.0, 1.0));
		}
		vec3 toSRGB(vec3 c) {
		  c = max(c, vec3(0.0));
		  // Filmic shoulder so the sun-lit snow does not clip hard.
		  c = c / (1.0 + max(c - 0.8, 0.0));
		  return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
		}
		""";

    /// <summary>
    /// The terrain's vertex shader: hands the game's fragment shader its inputs as Unity names them (the paint mask's
    /// coordinates in vs_TEXCOORD0, tangent frame and world position in vs_TEXCOORD1..3, biome colour), and the ocean
    /// depth for the main to put in the shader's <c>_depth</c>.
    /// </summary>
    private const string TerrainVs = """
		layout(location = 0) in vec3 position;
		layout(location = 1) in vec3 normal;
		layout(location = 3) in vec4 biomeColor;
		layout(location = 4) in vec2 maskUV;
		layout(location = 5) in float oceanDepth;
		layout(location = 6) in float limitTint;
		layout(location = 7) in float cutAway;
		uniform mat4 uViewProj;
		uniform vec3 uOffset;
		uniform vec3 _WorldSpaceCameraPos;
		out float vs_depth;
		out vec2 vs_TEXCOORD0;
		out vec4 vs_TEXCOORD1;
		out vec4 vs_TEXCOORD2;
		out vec4 vs_TEXCOORD3;
		out vec4 vs_COLOR0;
		out vec3 vs_TEXCOORD4;
		out vec3 vs_TEXCOORD6;
		out float vLimit;
		out float vCut;
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
		  vs_TEXCOORD0 = maskUV;
		  vLimit = limitTint;
		  vCut = cutAway;
		  gl_Position = uViewProj * vec4(position, 1.0);
		}
		""";

    /// <summary>
    /// The terrain fragment shader's main, after the game's shader: nothing where the ground is cut, else its G-buffer
    /// pass, lit like Unity's deferred Standard BRDF, then the view's slope colours, contour lines and limit marks, and
    /// the fog.
    /// </summary>
    private const string TerrainMain = """
		uniform vec3 uSunDir;
		uniform vec3 uSunColor;
		uniform float uSlope;
		uniform float uContour;
		uniform float uLimit;
		in float vLimit;
		in float vCut;
		in float vs_depth;
		out vec4 fragColor;
		void main() {
		  // Where a mod's extension cuts the ground (GroundCuts): nothing.
		  if (vCut > 0.5) discard;
		  // The shader blends its zone's four corner depths across the zone; the area is drawn as one mesh, so each
		  // corner is given this point's depth, and any blend of them is that depth.
		  for (int i = 0; i < 4; i++) { _depth[i] = vs_depth; }
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
		  // Points at the game's +-8 m edit limit (View: Limit marks).
		  col = mix(col, vec3(0.75, 0.06, 0.04), vLimit * 0.55 * uLimit);
		  fragColor = vec4(toSRGB(applyFogDir(col, length(_WorldSpaceCameraPos - wp), -v, uSunDir)), 1.0);
		}
		""";

    /// <summary>
    /// The sky's vertex shader: the sphere follows the camera and is drawn at the far plane.
    /// </summary>
    private const string SkyVs = """
		layout(location = 0) in vec3 position;
		uniform mat4 uViewProj;
		uniform vec3 uEye;
		out vec3 vDir;
		void main() { vDir = position; gl_Position = (uViewProj * vec4(position + uEye, 1.0)).xyww; }
		""";

    /// <summary>
    /// The sky's fragment shader: the gradient, moving clouds on a flat layer, the sun and the fog at the horizon.
    /// </summary>
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

    /// <summary>
    /// The water's vertex shader: passes on the point in Unity's world space.
    /// </summary>
    private const string WaterVs = """
		layout(location = 0) in vec3 position;
		uniform mat4 uViewProj;
		uniform vec3 uOffset;
		out vec3 vWorld;
		void main() { vWorld = vec3(position.x + uOffset.x, position.y, uOffset.z - position.z); gl_Position = uViewProj * vec4(position, 1.0); }
		""";

    /// <summary>
    /// The water's fragment shader: moving waves, colour by depth over the area's ground (uHeight) between its shallow
    /// and deep colours (WaterTint), a tint near the shore, sky reflection (tinted), sun glints, shore foam and fog.
    /// Beyond the area the water is deep and opaque.
    /// </summary>
    private const string WaterFs = """
		uniform sampler2D uHeight;
		uniform vec4 uRegion;
		uniform float uWater;
		uniform vec3 uTop, uHorizon, uSunDir, uSunColor, _WorldSpaceCameraPos;
		uniform vec3 uShallow, uDeep, uSurface;
		uniform vec4 uShore;
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
		  vec3 body = mix(uShallow, uDeep, 1.0 - exp(-viewDepth * 0.16));
		  body = mix(body, uShore.rgb, uShore.a * (1.0 - smoothstep(0.0, 2.5, viewDepth)));
		  body *= 0.55 + 0.45 * max(dot(n, uSunDir), 0.0);
		  vec3 r = reflect(-v, n); r.y = abs(r.y);
		  vec3 skyCol = mix(uHorizon, uTop, pow(max(r.y, 0.0), 0.55)) * uSurface;
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
}
