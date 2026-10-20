namespace TerrainEditor.Desktop;

/// <summary>
/// The world map shader, the editor's own: it gives the map the game's look from the game's map textures and material
/// values (read from the player's Valheim, MapView), in terms of the ground's height, biome colour and masks (forest,
/// mist, Ashlands), with a 1 m detail window that includes the edits. The version line is added when compiled
/// (GlView/MapView).
/// </summary>
public static class MapShader
{
    /// <summary>
    /// The vertex shader: a full-screen quad, positions already in clip space.
    /// </summary>
    public const string Vertex = """
		in vec2 pos;
		void main() { gl_Position = vec4(pos, 0.0, 1.0); }
		""";

    /// <summary>
    /// The fragment shader: works out each pixel's world point from the view centre and scale, and samples the
    /// whole-world textures (g*) or, inside the detail window, the 1 m detail textures (d*), with optional paint and
    /// clouds. The sea is everything below <c>waterLevel</c>.
    /// </summary>
    public const string Fragment = """
		uniform vec4 _Time;
		uniform vec4 _ForestColor, _WaterColor, _WaterColorDeep, _WaterColorAshlands, _WaterColorAshlandsDeep;
		uniform float _zoom, _normalIntensity, _quant;
		uniform vec3 _mapCenter, _lightColor, _ambientLightColor, _CloudOffset, _lavaColor1, _lavaColor2, _SunDir;
		uniform vec4 _SunFogColor, _SunColor, _AmbientColor;
		uniform sampler2D _BackgroundTex, _FogLayerTex, _WaterTex, _lavaTex, _MountainTex, _CloudTex, _ForestTex, _SpaceTex;
		uniform sampler2D gHeight, gMain, gMask, dHeight, dMain, dMask, dPaint;
		uniform vec2 viewCenter, canvasSize;
		uniform float metersPerPixel, mapMeters, waterLevel;
		uniform vec4 detailRect;      // x0, z0, size (m), enabled
		uniform float showPaint, showClouds, normalWidthM;
		out vec4 SV_Target0;

		// ---- Where: the map's texture space (0..1 over the whole map) and the 1 m detail window.
		vec2 worldOf(vec2 uv) { return uv * mapMeters - mapMeters * 0.5; }
		bool inDetail(vec2 w) { return detailRect.w > 0.5 && w.x >= detailRect.x && w.y >= detailRect.y && w.x <= detailRect.x + detailRect.z - 1.0 && w.y <= detailRect.y + detailRect.z - 1.0; }
		vec2 dUV(vec2 w) { return (w - detailRect.xy + 0.5) / detailRect.z; }
		vec4 sampleOf(sampler2D whole, sampler2D detail, vec2 uv) { vec2 w = worldOf(uv); return inDetail(w) ? texture(detail, dUV(w)) : texture(whole, uv); }
		float heightAt(vec2 uv) { return sampleOf(gHeight, dHeight, uv).r; }

		float lerpTo(float from, float to, float x) { return clamp((x - from) / (to - from), 0.0, 1.0); }
		float maxOf(vec3 c) { return max(c.r, max(c.g, c.b)); }
		// The overlay blend of one grey level over another.
		float overlay(float under, float over) { return under <= 0.5 ? 2.0 * under * over : 1.0 - 2.0 * (1.0 - under) * (1.0 - over); }
		vec2 turnedAboutMiddle(vec2 uv, float angle) { vec2 d = uv - 0.5; float c = cos(angle), s = sin(angle); return vec2(c * d.x + s * d.y, c * d.y - s * d.x) + 0.5; }

		// ---- Light: the ground's slope from its height one step west and south (flat under the sea), lit by the sun.
		vec3 groundNormal(vec2 uv, float h) {
		  float step = normalWidthM / mapMeters;
		  // The game measures over one texel (12 m); a finer step is scaled so the relief keeps its strength.
		  float k = sqrt((0.001 * mapMeters) / normalWidthM);
		  vec3 n = vec3((heightAt(uv - vec2(step, 0.0)) - h) * k, _normalIntensity, (heightAt(uv - vec2(0.0, step)) - h) * k);
		  return h >= waterLevel ? normalize(n) : vec3(0.0, 1.0, 0.0);
		}
		vec3 lightOn(vec3 n) {
		  float sun = max(dot(n, normalize(_SunDir)), 0.0);
		  return _AmbientColor.rgb * _ambientLightColor + sun * _SunColor.rgb * _lightColor;
		}

		// ---- The sea: from deep to shallow water over 20 m, the Ashlands' own water where the mask says, and the
		// moving water texture strongest near the shore.
		vec4 seaColor(vec2 uv, float h, float ashlands) {
		  vec4 fog = texture(_FogLayerTex, uv * 5.0);
		  vec4 ground = mix(fog, texture(_BackgroundTex, uv * 5.0), 0.5);
		  float shallow = lerpTo(waterLevel - 20.0, waterLevel, h);
		  vec4 water = mix(mix(_WaterColorDeep, _WaterColor, shallow), mix(_WaterColorAshlandsDeep, _WaterColorAshlands, shallow), smoothstep(0.0, 1.0, clamp(ashlands * 20.0, 0.0, 1.0)));
		  vec2 drift = vec2(_Time.x * 0.1, 0.01 * sin(uv.x * uv.y * 4000.0 + _Time.x * 20.0));
		  vec4 waves = texture(_WaterTex, uv * 80.0 + drift);
		  float nearShore = 1.0 - lerpTo(0.0, 14.0, waterLevel - 0.5 - h);
		  return mix(ground * water, waves, nearShore * waves.a);
		}

		// ---- The Ashlands' lava: three samples of the lava texture (two turning slowly about the middle) overlaid.
		vec3 lavaColor(vec2 uv) {
		  float still = texture(_lavaTex, uv * 40.0).r;
		  float a = texture(_lavaTex, turnedAboutMiddle(uv, _Time.y * 0.0005) * 80.0).r;
		  float b = texture(_lavaTex, turnedAboutMiddle(uv, _Time.y * -0.00087) * 60.0).r;
		  float glow = pow(max(overlay(still, overlay(a, b)), 1e-6), 2.5);
		  return mix(_lavaColor1, _lavaColor2, glow);
		}

		// ---- Land: the biome's colour on a grey ground texture, lava over the Ashlands' high ground, the edits' paint.
		vec4 landColor(vec2 uv, float h, float ashlands) {
		  vec4 ground = texture(_BackgroundTex, uv * 40.0);
		  vec4 land = sampleOf(gMain, dMain, uv) * vec4(vec3(maxOf(ground.rgb)), ground.a) * 1.5;
		  float lava = overlay(lerpTo(waterLevel + 1.0, waterLevel + 6.0, h) * ashlands, texture(_lavaTex, uv * 40.0).r);
		  land.rgb = mix(land.rgb, lavaColor(uv), lava);
		  vec2 w = worldOf(uv);
		  if (showPaint > 0.5 && inDetail(w)) {
		    vec4 p = texture(dPaint, dUV(w));
		    if (p.a > 0.5) {
		      vec3 tint = p.r * vec3(0.55, 0.42, 0.28) + p.g * vec3(0.38, 0.27, 0.17) + p.b * vec3(0.62, 0.6, 0.56);
		      land.rgb = mix(land.rgb, tint * 1.25, clamp(p.r + p.g + p.b, 0.0, 1.0) * 0.85);
		    }
		  }
		  return land;
		}

		// ---- High ground: the mountain texture from 70 m, snow on its flatter parts from 80 m (less on bright colours).
		vec4 highGround(vec4 color, vec2 uv, float h, vec3 n) {
		  vec4 rock = texture(_MountainTex, uv * 70.0);
		  color = mix(color, rock, lerpTo(70.0, 95.0, h) * rock.a);
		  float snow = max(lerpTo(80.0, 100.0, h) * lerpTo(0.14, 0.3, n.y) - clamp(dot(color.rgb, vec3(1.0)) * 1.5, 0.0, 1.0), 0.0);
		  color.rgb = mix(color.rgb, vec3(0.5), snow);
		  return color;
		}

		// ---- Mist over the mist mask: two drifting cloud layers.
		vec4 misted(vec4 color, vec2 uv, float mist) {
		  vec4 wobble = 0.01 * vec4(sin(uv.y * 850.0 + _Time.x * 5.0), cos(uv.x * 600.0 + _Time.x * 4.7706151), sin(uv.y * 750.0 + _Time.x * 5.0), cos(uv.x * 300.0 + _Time.x * 3.2706151));
		  vec4 near = texture(_CloudTex, uv * 15.0 + wobble.xy), far = texture(_CloudTex, uv * 20.0 + wobble.zw);
		  vec3 sky = _SunColor.rgb + _AmbientColor.rgb;
		  color = mix(color, vec4(sky * vec3(0.7, 0.5, 1.0) * near.a, near.a * near.a), mist);
		  return mix(color, vec4(sky * vec3(1.2, 0.7, 0.7), far.a), mist * far.a);
		}

		// ---- Forest over the forest mask: the forest texture in the forest colour, mostly lit.
		vec4 forested(vec4 color, vec2 uv, float forest, vec3 light) {
		  vec4 trees = texture(_ForestTex, uv * 150.0) * _ForestColor;
		  trees.rgb = mix(trees.rgb, trees.rgb * light, 0.8);
		  return mix(color, trees, forest * trees.a);
		}

		void main() {
		  vec2 frag = gl_FragCoord.xy;
		  vec2 world = viewCenter + (frag - canvasSize * 0.5) * metersPerPixel;
		  // The map's own pixels: each texture point snapped to its cell (the game's ~3.5 m, the close-up 1 m).
		  vec2 uv = floor(((world + mapMeters * 0.5) / mapMeters) * _quant + 0.5) / _quant;
		  vec4 mask = sampleOf(gMask, dMask, uv);
		  float h = heightAt(uv);
		  vec3 n = groundNormal(uv, h);
		  vec3 light = lightOn(n);
		  vec4 color = h < waterLevel ? seaColor(uv, h, mask.b) : landColor(uv, h, mask.b);
		  color = highGround(color, uv, h, n);
		  color.rgb *= light;
		  // The shore drawn as a thin dark line, wider when zoomed out.
		  float shore = 1.0 - min(abs(h - waterLevel) / clamp(_zoom * 50.0, 2.0, 10.0), 1.0);
		  color = mix(color, vec4(0.02, 0.01, 0.01, 1.0), shore);
		  color = misted(color, uv, mask.g);
		  color = forested(color, uv, mask.r, light);
		  if (showClouds > 0.5) {
		    vec4 cloud = texture(_CloudTex, uv * 7.0 - _CloudOffset.xz);
		    color = mix(color, vec4(_lightColor * _SunColor.rgb, 1.0), cloud.a);
		  }
		  // Everything is explored. Space beyond the world's edge.
		  float edge = smoothstep(0.0, 1.0, clamp((length(uv - 0.5) - 0.42) * 100.0, 0.0, 1.0));
		  vec4 space = texture(_SpaceTex, frag * 0.0007 - _mapCenter.xz * 0.00001);
		  vec3 linearColor = max(mix(color, space, edge).rgb, vec3(0.0));
		  // Written to an sRGB target, as the game's map is drawn: the linear colour encoded.
		  vec3 srgb = mix(linearColor * 12.92, 1.055 * pow(linearColor, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), linearColor));
		  SV_Target0 = vec4(srgb, 1.0);
		}
		""";
}
