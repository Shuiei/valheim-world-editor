// World map rendered with Valheim's own map shader (Custom/mapshader, GLSL taken from the game),
// ported to WebGL2. Inputs are the same textures the game builds in Minimap.GenerateWorldMap,
// plus an optional 1 m detail window that includes terrain edits.

const VERT = `#version 300 es
in vec2 pos;
void main() { gl_Position = vec4(pos, 0.0, 1.0); }`;

// Fragment shader: the body follows the game's compiled shader line by line; only the texture
// lookups go through helpers that can use the 1 m detail window, and fog of war is disabled.
const FRAG = `#version 300 es
precision highp float;
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
}`;

// Material values from the game's "minimap shader" material.
const MATERIAL = {
  _ForestColor: [0.9191, 0.8218, 0.6758, 1], _WaterColor: [0.8429, 0.9373, 1.0917, 1], _WaterColorDeep: [0.343, 0.5337, 0.7647, 1],
  _WaterColorAshlands: [0.2078, 0.2078, 0.2078, 1], _WaterColorAshlandsDeep: [0.2235, 0.251, 0.3686, 1],
  _lightColor: [1.2, 1.2, 1.2], _ambientLightColor: [0.8088, 0.8418, 1.0], _lavaColor1: [0.4157, 0.0235, 0.0], _lavaColor2: [4.5126, 0.2599, 0.0],
  _normalIntensity: 4.0
};
// Environment lighting set by the game's day cycle; these are clear-midday values.
const DAYLIGHT = { _SunDir: [-0.35, 0.86, 0.36], _SunColor: [1.0, 0.97, 0.9, 1], _AmbientColor: [0.36, 0.38, 0.44, 1], _SunFogColor: [0.9, 0.9, 1, 1] };

// Unity's GammaToLinearSpace for colour properties (values above 1 are HDR and follow the same curve).
// Plain stand-ins (RGBA) for the map textures when they were not extracted from the game.
const FALLBACK = {
  _BackgroundTex: [200, 186, 150, 255], _FogLayerTex: [255, 255, 255, 0], _WaterTex: [128, 128, 128, 255], _lavaTex: [128, 128, 128, 255],
  _MountainTex: [255, 255, 255, 255], _CloudTex: [0, 0, 0, 0], _ForestTex: [128, 128, 128, 0], _SpaceTex: [10, 10, 16, 255],
};
const toLinear = c => c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);

export class MapView {
  constructor(canvas, world) {
    this.canvas = canvas;
    this.world = world;
    this.mapMeters = world.mapSize * world.mapPixel;
    const gl = this.gl = canvas.getContext('webgl2', { antialias: false });
    if (!gl) throw new Error('This browser does not support WebGL2.');
    this.prog = this.program(VERT, FRAG);
    const buf = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buf);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
    const loc = gl.getAttribLocation(this.prog, 'pos');
    gl.enableVertexAttribArray(loc);
    gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
    this.units = {};
    this.detail = null;
    this.showPaint = true;
    this.showClouds = false;
    this.start = performance.now();
    this.ready = this.load();
  }

  program(vs, fs) {
    const gl = this.gl, p = gl.createProgram();
    for (const [type, src] of [[gl.VERTEX_SHADER, vs], [gl.FRAGMENT_SHADER, fs]]) {
      const s = gl.createShader(type);
      gl.shaderSource(s, src); gl.compileShader(s);
      if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
      gl.attachShader(p, s);
    }
    gl.linkProgram(p);
    if (!gl.getProgramParameter(p, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(p));
    return p;
  }

  texture(name, setup) {
    const gl = this.gl;
    if (!(name in this.units)) this.units[name] = Object.keys(this.units).length;
    const unit = this.units[name];
    gl.activeTexture(gl.TEXTURE0 + unit);
    const tex = this[`tex_${name}`] ??= gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    setup(gl);
    gl.useProgram(this.prog);
    gl.uniform1i(gl.getUniformLocation(this.prog, name), unit);
  }

  async loadImage(name, url, srgb = true, repeat = true) {
    const img = new Image();
    img.src = url;
    try {
      await img.decode();
    } catch {
      // The map textures come from the game (tools/asset-export) and are not in git: without them
      // the map is drawn with one plain colour per texture instead of failing.
      this.missingTextures = (this.missingTextures ?? 0) + 1;
      const c = FALLBACK[name] ?? [128, 128, 128, 255];
      this.texture(name, gl => {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(c));
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      });
      return;
    }
    this.texture(name, gl => {
      gl.texImage2D(gl.TEXTURE_2D, 0, srgb ? gl.SRGB8_ALPHA8 : gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, img);
      gl.generateMipmap(gl.TEXTURE_2D);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      const wrap = repeat ? gl.REPEAT : gl.CLAMP_TO_EDGE;
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrap); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, wrap);
    });
  }

  // A map reply: heights (2 bytes a point) and 4-byte colour layers (2 global, 3 detail). A reply
  // cut short (the session stopped, e.g. Worlds was pressed while it loaded) is an error, not data.
  async fetchLayers(url, size, layers) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`the map data did not arrive (${res.status})`);
    const buf = await res.arrayBuffer();
    if (buf.byteLength < size * size * (2 + 4 * layers)) throw new Error('the map data arrived incomplete');
    return buf;
  }

  uploadLayers(prefix, buffer, size) {
    const n = size * size;
    const heights = new Uint16Array(buffer, 0, n);
    const rgba = (i) => new Uint8Array(buffer, n * 2 + n * 4 * i, n * 4);
    const set = (name, internal, format, type, data) => this.texture(name, gl => {
      gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
      gl.texImage2D(gl.TEXTURE_2D, 0, internal, size, size, 0, format, type, data);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    });
    const gl = this.gl;
    set(prefix + 'Height', gl.R16F, gl.RED, gl.HALF_FLOAT, heights);
    set(prefix + 'Main', gl.SRGB8_ALPHA8, gl.RGBA, gl.UNSIGNED_BYTE, rgba(0));
    set(prefix + 'Mask', gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, rgba(1));
    if (prefix === 'd') set('dPaint', gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, rgba(2));
  }

  async load() {
    const textures = ['Background:background', 'FogLayer:foglayer', 'Water:water', 'lava:lava', 'Mountain:mountain', 'Cloud:cloud', 'Forest:forest', 'Space:space'];
    // The lava mask is data, every other map texture is a colour texture.
    await Promise.all(textures.map(t => { const [u, f] = t.split(':'); return this.loadImage(`_${u}Tex`, `maptex/${f}.png`, u !== 'lava'); }));
    const buf = await this.fetchLayers('/api/map/global', this.world.mapSize, 2);
    this.uploadLayers('g', buf, this.world.mapSize);
    // Placeholders until a detail window is loaded.
    const empty = new ArrayBuffer(16 * 16 * 14);
    this.uploadLayers('d', empty, 16);
  }

  async loadDetail(x0, z0, size, version) {
    const key = `${x0},${z0},${size},${version}`;
    if (this.detailKey === key || this.pendingKey === key) return;
    this.pendingKey = key;
    let buf;
    try {
      buf = await this.fetchLayers(`/api/map/detail?x0=${x0}&z0=${z0}&size=${size}`, size, 3);
    } catch {
      // Only the sharper close-up is missing: the next view change asks again.
      if (this.pendingKey === key) this.pendingKey = null;
      return;
    }
    if (this.pendingKey !== key) return;
    this.uploadLayers('d', buf, size);
    this.detail = { x0, z0, size };
    this.detailKey = key;
    this.pendingKey = null;
  }

  render(view) {
    const gl = this.gl, p = this.prog, u = n => gl.getUniformLocation(p, n);
    const w = this.canvas.width, h = this.canvas.height;
    gl.viewport(0, 0, w, h);
    gl.useProgram(p);
    const t = (performance.now() - this.start) / 1000;
    gl.uniform4f(u('_Time'), t / 20, t, t * 2, t * 3);
    for (const [k, raw] of Object.entries({ ...MATERIAL, ...DAYLIGHT })) {
      const loc = u(k);
      if (!loc) continue;
      if (typeof raw === 'number') { gl.uniform1f(loc, raw); continue; }
      const v = k === '_SunDir' ? raw : raw.map((c, i) => i < 3 ? toLinear(c) : c);
      if (v.length === 3) gl.uniform3fv(loc, v); else gl.uniform4fv(loc, v);
    }
    const mpp = view.metersPerPixel;
    gl.uniform1f(u('_zoom'), (w * mpp) / this.mapMeters);
    // The game quantizes to ~3.5 m map "pixels"; detail areas go down to 1 m.
    gl.uniform1f(u('_quant'), this.detail && mpp < 3.5 ? this.mapMeters : 7000);
    gl.uniform1f(u('normalWidthM'), this.detail && mpp < 6 ? Math.max(1, mpp) : this.world.mapPixel * 2.048);
    gl.uniform3f(u('_mapCenter'), view.x, 0, view.z);
    gl.uniform3f(u('_CloudOffset'), t * 0.0015, 0, t * 0.001);
    gl.uniform2f(u('viewCenter'), view.x, view.z);
    gl.uniform2f(u('canvasSize'), w, h);
    gl.uniform1f(u('metersPerPixel'), mpp);
    gl.uniform1f(u('mapMeters'), this.mapMeters);
    const d = this.detail;
    gl.uniform4f(u('detailRect'), d ? d.x0 : 0, d ? d.z0 : 0, d ? d.size : 1, d ? 1 : 0);
    gl.uniform1f(u('showPaint'), this.showPaint ? 1 : 0);
    gl.uniform1f(u('showClouds'), this.showClouds ? 1 : 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
  }
}
