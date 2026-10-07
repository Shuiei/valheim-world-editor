namespace TerrainEditor.Desktop;

// The world map shader: Valheim's own map shader (Custom/mapshader, GLSL taken from the game), as the
// web editor's mapview.js has it (the same text), with a 1 m detail window that includes the edits.
// The version line is added when compiled (GlView/MapView).
public static class MapShader
{
	public const string Vertex = """
		in vec2 pos;
		void main() { gl_Position = vec4(pos, 0.0, 1.0); }
		""";

	public const string Fragment = """
		uniform vec4 _Time;
		uniform vec4 _ForestColor, _WaterColor, _WaterColorDeep, _WaterColorAshlands, _WaterColorAshlandsDeep;
		uniform float _zoom, _normalIntensity, _quant;
		uniform vec3 _mapCenter, _lightColor, _ambientLightColor, _CloudOffset, _lavaColor1, _lavaColor2, _SunDir;
		uniform vec4 _SunFogColor, _SunColor, _AmbientColor;
		uniform sampler2D _BackgroundTex, _FogLayerTex, _WaterTex, _lavaTex, _MountainTex, _CloudTex, _ForestTex, _SpaceTex;
		uniform sampler2D gHeight, gMain, gMask, dHeight, dMain, dMask, dPaint;
		uniform vec2 viewCenter, canvasSize;
		uniform float metersPerPixel, mapMeters;
		uniform vec4 detailRect;      // x0, z0, size (m), enabled
		uniform float showPaint, showClouds, normalWidthM;
		out vec4 SV_Target0;

		vec2 worldOf(vec2 uv) { return uv * mapMeters - mapMeters * 0.5; }
		bool inDetail(vec2 w) { return detailRect.w > 0.5 && w.x >= detailRect.x && w.y >= detailRect.y && w.x <= detailRect.x + detailRect.z - 1.0 && w.y <= detailRect.y + detailRect.z - 1.0; }
		vec2 dUV(vec2 w) { return (w - detailRect.xy + 0.5) / detailRect.z; }
		vec4 sH(vec2 uv) { vec2 w = worldOf(uv); return inDetail(w) ? texture(dHeight, dUV(w)) : texture(gHeight, uv); }
		vec4 sMain(vec2 uv) { vec2 w = worldOf(uv); return inDetail(w) ? texture(dMain, dUV(w)) : texture(gMain, uv); }
		vec4 sMask(vec2 uv) { vec2 w = worldOf(uv); return inDetail(w) ? texture(dMask, dUV(w)) : texture(gMask, uv); }

		void main() {
		  vec2 frag = gl_FragCoord.xy;
		  vec2 world = viewCenter + (frag - canvasSize * 0.5) * metersPerPixel;
		  vec2 TEX = (world + mapMeters * 0.5) / mapMeters;
		  vec4 u_xlat0, u_xlat1, u_xlat2, u_xlat3, u_xlat4, u_xlat5, u_xlat6, u_xlat7, u_xlat8, u_xlat9, u_xlat10, u_xlat11, u_xlat12;
		  vec2 u_xlat13, u_xlat27; vec3 u_xlat16, u_xlat17, u_xlat18, u_xlat19;
		  float u_xlat14, u_xlat26, u_xlat29, u_xlat39, u_xlat42; bool u_xlatb0, u_xlatb14, u_xlatb39;
		  u_xlat0.x = _quant;
		  u_xlat13.xy = TEX * u_xlat0.xx + vec2(0.5, 0.5);
		  u_xlat13.xy = trunc(u_xlat13.xy);
		  u_xlat0.yz = u_xlat13.yx / u_xlat0.xx;
		  u_xlat1.xy = u_xlat0.zy * vec2(5.0, 5.0);
		  u_xlat2 = texture(_FogLayerTex, u_xlat1.xy);
		  u_xlat3 = sMask(u_xlat0.zy);
		  u_xlat4 = sH(u_xlat0.zy);
		  u_xlat5 = _Time.xxxx * vec4(20.0, 19.0824604, 16.8600006, 18.8824615);
		  u_xlat27.xy = u_xlat0.zy * vec2(70.0, 70.0);
		  // Normals: the game samples one texel width away (12 m); detail uses a finer step, scaled to match.
		  vec2 nw = vec2(normalWidthM / mapMeters);
		  float nscale = sqrt((0.001 * mapMeters) / normalWidthM);
		  u_xlat0.xw = u_xlat0.zy + (-nw);
		  u_xlat8 = sH(u_xlat0.xy);
		  u_xlat9 = sH(u_xlat0.zw);
		  u_xlat8.x = ((-u_xlat4.x) + u_xlat8.x) * nscale;
		  u_xlat8.z = ((-u_xlat4.x) + u_xlat9.x) * nscale;
		  u_xlatb0 = u_xlat4.x >= 29.5;
		  u_xlat8.y = _normalIntensity;
		  u_xlat39 = inversesqrt(dot(u_xlat8.xyz, u_xlat8.xyz));
		  u_xlat17.xyz = vec3(u_xlat39) * u_xlat8.xyz;
		  u_xlat17.xyz = u_xlatb0 ? u_xlat17.xyz : vec3(0.0, 1.0, 0.0);
		  u_xlat18.xyz = normalize(_SunDir.xyz);
		  u_xlat0.x = max(dot(u_xlat17.xyz, u_xlat18.xyz), 0.0);
		  u_xlat18.xyz = u_xlat0.xxx * _SunColor.xyz * _lightColor.xyz;
		  u_xlat18.xyz = _AmbientColor.xyz * _ambientLightColor.xyz + u_xlat18.xyz;
		  if (u_xlat4.x < 29.5) {
		    u_xlat8 = texture(_BackgroundTex, u_xlat1.xy);
		    u_xlat0.xw = u_xlat4.xx + vec2(-9.5, -29.0);
		    u_xlat0.xw = clamp(u_xlat0.xw * vec2(0.0500000007, -0.0714285746), 0.0, 1.0);
		    u_xlat9 = u_xlat0.xxxx * (_WaterColorAshlands - _WaterColorAshlandsDeep) + _WaterColorAshlandsDeep;
		    u_xlat10 = u_xlat0.xxxx * (_WaterColor - _WaterColorDeep) + _WaterColorDeep;
		    u_xlat0.x = clamp(u_xlat3.z * 20.0, 0.0, 1.0);
		    u_xlat0.x = u_xlat0.x * u_xlat0.x * (u_xlat0.x * -2.0 + 3.0);
		    u_xlat8 = (u_xlat8 - u_xlat2) * vec4(0.5) + u_xlat2;
		    u_xlat9 = u_xlat0.xxxx * (u_xlat9 - u_xlat10) + u_xlat10;
		    u_xlat10 = u_xlat8 * u_xlat9;
		    u_xlat0.x = sin(u_xlat0.y * u_xlat0.z * 4000.0 + u_xlat5.x);
		    u_xlat1.y = u_xlat0.x * 0.00999999978;
		    u_xlat1.x = _Time.x * 0.100000001;
		    u_xlat1.xy = u_xlat0.zy * vec2(80.0, 80.0) + u_xlat1.xy;
		    u_xlat11 = texture(_WaterTex, u_xlat1.xy);
		    u_xlat0.x = ((-u_xlat0.w) + 1.0) * u_xlat11.w;
		    u_xlat8 = (-u_xlat8) * u_xlat9 + u_xlat11;
		    u_xlat8 = u_xlat0.xxxx * u_xlat8 + u_xlat10;
		  } else {
		    u_xlat0.xw = u_xlat0.zy * vec2(40.0, 40.0);
		    u_xlat9 = texture(_BackgroundTex, u_xlat0.xw);
		    u_xlat10 = sMain(u_xlat0.zy);
		    u_xlat1.x = max(u_xlat9.z, max(u_xlat9.y, u_xlat9.x));
		    u_xlat14 = min(u_xlat9.z, min(u_xlat9.y, u_xlat9.x));
		    u_xlat14 = u_xlat1.x - u_xlat14;
		    u_xlatb14 = u_xlat14 >= 9.99999975e-05;
		    u_xlat9.xyz = u_xlatb14 ? u_xlat1.xxx : u_xlat9.xyz;
		    u_xlat9 = u_xlat10 * u_xlat9;
		    u_xlat8 = u_xlat9 * vec4(1.5);
		    u_xlat1.x = clamp((u_xlat4.x + -30.5) * 0.200000003, 0.0, 1.0);
		    u_xlat10 = texture(_lavaTex, u_xlat0.xw);
		    u_xlat0.xw = _Time.yy * vec2(0.000500000024, -0.000869999989);
		    u_xlat5.x = cos(u_xlat0.x);
		    u_xlat0.x = sin(u_xlat0.x);
		    u_xlat17.xz = u_xlat0.yz + vec2(-0.5, -0.5);
		    u_xlat19.xy = u_xlat0.xx * u_xlat17.xz;
		    u_xlat0.x = u_xlat5.x * u_xlat17.z + u_xlat19.x;
		    u_xlat11.x = u_xlat0.x + 0.5;
		    u_xlat0.x = u_xlat5.x * u_xlat17.x + (-u_xlat19.y);
		    u_xlat11.y = u_xlat0.x + 0.5;
		    u_xlat11 = texture(_lavaTex, u_xlat11.xy * vec2(80.0, 80.0));
		    u_xlat5.x = cos(u_xlat0.w);
		    u_xlat0.x = sin(u_xlat0.w);
		    u_xlat0.xw = u_xlat17.xz * u_xlat0.xx;
		    u_xlat0.x = u_xlat5.x * u_xlat17.z + u_xlat0.x;
		    u_xlat12.x = u_xlat0.x + 0.5;
		    u_xlat0.x = u_xlat5.x * u_xlat17.x + (-u_xlat0.w);
		    u_xlat12.y = u_xlat0.x + 0.5;
		    u_xlat12 = texture(_lavaTex, u_xlat12.xy * vec2(60.0, 60.0));
		    u_xlatb0 = 0.5 >= u_xlat11.x;
		    u_xlat39 = dot(u_xlat12.xx, u_xlat11.xx);
		    u_xlat14 = ((-u_xlat11.x) + 1.0) * 2.0;
		    u_xlat42 = (-u_xlat12.x) + 1.0;
		    u_xlat14 = (-u_xlat14) * u_xlat42 + 1.0;
		    u_xlat0.x = u_xlatb0 ? u_xlat39 : u_xlat14;
		    u_xlatb39 = 0.5 >= u_xlat10.x;
		    u_xlat14 = dot(u_xlat0.xx, u_xlat10.xx);
		    u_xlat42 = (-u_xlat10.x) + 1.0;
		    u_xlat17.x = u_xlat42 + u_xlat42;
		    u_xlat0.x = (-u_xlat0.x) + 1.0;
		    u_xlat0.x = (-u_xlat17.x) * u_xlat0.x + 1.0;
		    u_xlat0.x = u_xlatb39 ? u_xlat14 : u_xlat0.x;
		    u_xlat0.x = exp2(log2(max(u_xlat0.x, 1e-6)) * 2.5);
		    u_xlat19.xyz = u_xlat0.xxx * (_lavaColor2.xyz - _lavaColor1.xyz) + _lavaColor1.xyz;
		    u_xlat0.x = u_xlat1.x * u_xlat3.z;
		    u_xlatb39 = 0.5 >= u_xlat0.x;
		    u_xlat0.x = dot(u_xlat10.xx, u_xlat0.xx);
		    u_xlat1.x = (-u_xlat3.z) * u_xlat1.x + 1.0;
		    u_xlat1.x = dot(vec2(u_xlat42), u_xlat1.xx);
		    u_xlat1.x = (-u_xlat1.x) + 1.0;
		    u_xlat0.x = u_xlatb39 ? u_xlat0.x : u_xlat1.x;
		    u_xlat19.xyz = (-u_xlat9.xyz) * vec3(1.5) + u_xlat19.xyz;
		    u_xlat8.xyz = u_xlat0.xxx * u_xlat19.xyz + u_xlat8.xyz;
		    // Optional: painted ground (dirt, cultivated, paved) from the detail window.
		    vec2 w = worldOf(u_xlat0.zy);
		    if (showPaint > 0.5 && inDetail(w)) {
		      vec4 p = texture(dPaint, dUV(w));
		      if (p.a > 0.5) {
		        vec3 tint = p.r * vec3(0.55, 0.42, 0.28) + p.g * vec3(0.38, 0.27, 0.17) + p.b * vec3(0.62, 0.6, 0.56);
		        float k = clamp(p.r + p.g + p.b, 0.0, 1.0);
		        u_xlat8.xyz = mix(u_xlat8.xyz, tint * 1.25, k * 0.85);
		      }
		    }
		  }
		  u_xlat1 = texture(_MountainTex, u_xlat27.xy);
		  u_xlat4.xyw = u_xlat4.xxx + vec3(-70.0, -80.0, -29.5);
		  u_xlat0.xw = clamp(u_xlat4.xy * vec2(0.0399999991, 0.0500000007), 0.0, 1.0);
		  u_xlat0.x = u_xlat1.w * u_xlat0.x;
		  u_xlat1 = u_xlat0.xxxx * (u_xlat1 - u_xlat8) + u_xlat8;
		  u_xlat0.x = clamp((u_xlat17.y + -0.140000001) * 6.24999952, 0.0, 1.0);
		  u_xlat29 = clamp(dot(u_xlat1.xyz, vec3(1.0)) * 1.5, 0.0, 1.0);
		  u_xlat0.x = max(u_xlat0.w * u_xlat0.x + (-u_xlat29), 0.0);
		  u_xlat4.xyz = u_xlat0.xxx * (vec3(0.5) - u_xlat1.xyz) + u_xlat1.xyz;
		  u_xlat1.xyz = u_xlat18.xyz * u_xlat4.xyz;
		  u_xlat0.x = clamp(_zoom * 50.0, 2.0, 10.0);
		  u_xlat0.x = 1.0 - min(abs(u_xlat4.w) / u_xlat0.x, 1.0);
		  u_xlat1 = u_xlat0.xxxx * (vec4(0.0199999996, 0.00999999978, 0.00999999978, 1.0) - u_xlat1) + u_xlat1;
		  u_xlat4 = _Time.xxxx * vec4(5.0, 4.7706151, 5.0, 3.2706151);
		  u_xlat4 = u_xlat0.yzyz * vec4(850.0, 600.0, 750.0, 300.0) + u_xlat4;
		  u_xlat8.xy = sin(u_xlat4.xz);
		  u_xlat8.zw = cos(u_xlat4.yw);
		  u_xlat4 = u_xlat8.xzyw * vec4(0.00999999978);
		  u_xlat4 = u_xlat0.zyzy * vec4(15.0, 15.0, 20.0, 20.0) + u_xlat4;
		  u_xlat8 = texture(_CloudTex, u_xlat4.xy);
		  u_xlat4 = texture(_CloudTex, u_xlat4.zw);
		  u_xlat19.xyz = _SunColor.xyz + _AmbientColor.xyz;
		  u_xlat8.xyz = u_xlat19.xyz * vec3(0.699999988, 0.5, 1.0);
		  u_xlat4.xyz = u_xlat19.xyz * vec3(1.20000005, 0.699999988, 0.699999988);
		  u_xlat8 = u_xlat8 * u_xlat8.wwww + (-u_xlat1);
		  u_xlat1 = u_xlat3.yyyy * u_xlat8 + u_xlat1;
		  u_xlat0.x = u_xlat3.y * u_xlat4.w;
		  u_xlat1 = u_xlat0.xxxx * (u_xlat4 - u_xlat1) + u_xlat1;
		  u_xlat0.xw = u_xlat0.zy * vec2(150.0, 150.0);
		  u_xlat4 = texture(_ForestTex, u_xlat0.xw) * _ForestColor;
		  u_xlat16.xyz = u_xlat4.xyz * u_xlat18.xyz + (-u_xlat4.xyz);
		  u_xlat4.xyz = u_xlat16.xyz * vec3(0.800000012) + u_xlat4.xyz;
		  u_xlat0.x = u_xlat3.x * u_xlat4.w;
		  u_xlat1 = u_xlat0.xxxx * (u_xlat4 - u_xlat1) + u_xlat1;
		  if (showClouds > 0.5) {
		    u_xlat0.xw = u_xlat0.zy * vec2(7.0, 7.0) + (-_CloudOffset.xz);
		    u_xlat3 = texture(_CloudTex, u_xlat0.xw);
		    u_xlat4 = vec4(_lightColor.xyz * _SunColor.xyz, 1.0);
		    u_xlat1 = u_xlat3.wwww * (u_xlat4 - u_xlat1) + u_xlat1;
		  }
		  // Fog of war: everything is treated as explored. Space beyond the world edge as in the game.
		  u_xlat0.x = sqrt(dot(u_xlat0.zy - 0.5, u_xlat0.zy - 0.5));
		  u_xlat13.xy = _mapCenter.xz * vec2(9.99999975e-06);
		  u_xlat13.xy = frag * vec2(0.000699999975) + (-u_xlat13.xy);
		  u_xlat2 = texture(_SpaceTex, u_xlat13.xy);
		  u_xlat0.x = clamp((u_xlat0.x + -0.419999987) * 99.999794, 0.0, 1.0);
		  u_xlat0.x = u_xlat0.x * u_xlat0.x * (u_xlat0.x * -2.0 + 3.0);
		  vec3 linearColor = max((u_xlat0.xxxx * (u_xlat2 - u_xlat1) + u_xlat1).rgb, vec3(0.0));
		  // Unity (linear colour space) writes to an sRGB target; do the same encoding here.
		  vec3 srgb = mix(linearColor * 12.92, 1.055 * pow(linearColor, vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), linearColor));
		  SV_Target0 = vec4(srgb, 1.0);
		}
		""";
}
