// In-game look for the 3D editor: Valheim's own terrain shader (heightmap.frag.glsl, taken from the
// game and lit here as the game's deferred renderer would), a sky dome and the ocean.
// Everything inside the shaders works in Unity's left-handed world space: three.js (x, y, z) is
// Unity (x + offset.x, y, offset.z - z).
import * as THREE from 'three';

const base = new URL('./', import.meta.url);

function loadImage(name) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error(`could not load ${name}`));
    img.src = new URL(name, base).href;
  });
}

// RGBA pixels with row 0 at the bottom (Unity's texture origin).
function pixels(img, flip = true) {
  const c = document.createElement('canvas');
  c.width = img.width; c.height = img.height;
  const ctx = c.getContext('2d', { willReadFrequently: true });
  ctx.drawImage(img, 0, 0);
  const src = ctx.getImageData(0, 0, img.width, img.height).data;
  if (!flip) return new Uint8Array(src.buffer);
  const out = new Uint8Array(src.length), row = img.width * 4;
  for (let y = 0; y < img.height; y++) out.set(src.subarray(y * row, (y + 1) * row), (img.height - 1 - y) * row);
  return out;
}

async function texture2D(name, srgb = false) {
  const img = await loadImage(name);
  const t = new THREE.DataTexture(pixels(img), img.width, img.height, THREE.RGBAFormat);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.magFilter = THREE.LinearFilter; t.minFilter = THREE.LinearMipmapLinearFilter; t.generateMipmaps = true;
  t.anisotropy = 8;
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.needsUpdate = true;
  return t;
}

// A vertical strip of 256 x 256 slices -> texture array (each slice flipped to Unity's origin).
async function textureArray(name, srgb) {
  const img = await loadImage(name);
  const n = img.height / img.width, s = img.width, all = pixels(img, false), out = new Uint8Array(all.length), row = s * 4;
  for (let k = 0; k < n; k++) for (let y = 0; y < s; y++) out.set(all.subarray((k * s + y) * row, (k * s + y + 1) * row), (k * s + s - 1 - y) * row);
  const t = new THREE.DataArrayTexture(out, s, s, n);
  t.format = THREE.RGBAFormat;
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.magFilter = THREE.LinearFilter; t.minFilter = THREE.LinearMipmapLinearFilter; t.generateMipmaps = true;
  t.anisotropy = 8;
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.needsUpdate = true;
  return t;
}

// Stand-ins for the global 3D noise textures (_CurlNoise, _SnowGlintOffsetNoise): smooth value noise.
function noise3D(size, seed) {
  let a = seed >>> 0;
  const rnd = () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
  const data = new Uint8Array(size * size * size * 4);
  for (let i = 0; i < data.length; i++) data[i] = Math.floor(rnd() * 256);
  const t = new THREE.Data3DTexture(data, size, size, size);
  t.format = THREE.RGBAFormat;
  t.wrapS = t.wrapT = t.wrapR = THREE.RepeatWrapping;
  t.magFilter = t.minFilter = THREE.LinearFilter;
  t.needsUpdate = true;
  return t;
}

// Midday in the game's default "Clear" environment (EnvMan.m_environments): the colours are the
// game's gamma-space values, converted to linear like Unity does.
const srgb = (r, g, b) => new THREE.Color().setRGB(r, g, b, THREE.SRGBColorSpace);
export const DAY = {
  sunDir: new THREE.Vector3(-0.42, 0.72, 0.55).normalize(),
  // The game's colour grading takes much of the orange out of its sun; half-way to white matches it.
  sunColor: srgb(1.0, 0.886, 0.742).multiplyScalar(1.7),
  ambient: srgb(0.463, 0.574, 0.706),
  fogColor: srgb(0.302, 0.581, 0.747),
  sunFogColor: srgb(0.801, 0.687, 0.43),
  fogDensity: 0.003,
  skyTop: srgb(0.2, 0.42, 0.68),
  skyHorizon: srgb(0.55, 0.72, 0.84)
};

const commonGLSL = /* glsl */`
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
`;

const terrainVertex = /* glsl */`
precision highp float;
uniform mat4 modelViewMatrix, projectionMatrix;
uniform vec3 uOffset;
uniform vec3 _WorldSpaceCameraPos;
in vec3 position;
in vec3 normal;
in vec4 biomeColor;
in float oceanDepth;
in vec2 maskUV;
in float limitTint;
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
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;

const terrainMain = /* glsl */`
uniform vec3 uSunDir;
uniform vec3 uSunColor;
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
  // Points at the game's +-8 m edit limit.
  col = mix(col, vec3(0.75, 0.06, 0.04), vLimit * 0.55);
  fragColor = vec4(toSRGB(applyFogDir(col, length(_WorldSpaceCameraPos - wp), -v, uSunDir)), 1.0);
}`;

export async function createLook(renderer, { offset, waterLevel }) {
  const [frag, dArr, nArr, noise, cliff, mistCliff, paved, rock, snow, variety] = await Promise.all([
    fetch(new URL('heightmap.frag.glsl', base)).then(r => r.text()),
    textureArray('d_array.png', true), textureArray('n_array.png', false),
    texture2D('noise.png'), texture2D('cliff_n.png'), texture2D('mistcliff_n.png'), texture2D('paved_n.png'),
    texture2D('rock_n.png'), texture2D('snow_n.png'), texture2D('variety.png')
  ]);
  const white = new THREE.DataTexture(new Uint8Array([255, 255, 255, 255]), 1, 1); white.needsUpdate = true;
  // RenderSettings.ambientMode = Flat: only the constant spherical-harmonics term.
  const [shr, shg, shb] = ['r', 'g', 'b'].map(c => new THREE.Vector4(0, 0, 0, DAY.ambient[c]));
  const camUnity = new THREE.Vector3();
  const shared = {
    uFogColor: { value: DAY.fogColor }, uSunFogColor: { value: DAY.sunFogColor }, uFogDensity: { value: DAY.fogDensity },
    uSunDir: { value: DAY.sunDir }, uSunColor: { value: DAY.sunColor },
    uOffset: { value: new THREE.Vector3(offset.x, 0, offset.z) },
    _WorldSpaceCameraPos: { value: camUnity },
    _Time: { value: new THREE.Vector4() }
  };
  const terrain = new THREE.RawShaderMaterial({
    glslVersion: THREE.GLSL3,
    vertexShader: terrainVertex,
    fragmentShader: frag + commonGLSL + terrainMain,
    uniforms: {
      ...shared,
      unity_SHAr: { value: shr }, unity_SHAg: { value: shg }, unity_SHAb: { value: shb },
      _SkyAlphaPosition: { value: new THREE.Vector3(0, -1e5, 0) },
      _SnowGlintSize: { value: 0.2 }, _SnowGlintDensity: { value: 0.15 }, _SnowGlintStrength: { value: 4 },
      _SnowGlintNoiseDensity: { value: 1 }, _SnowGlintNoiseAmount: { value: 80 }, _SnowGlintViewAmount: { value: 0.5 },
      _SnowGlintTimeAmount: { value: 3 },
      _AshlandsVariationCol: { value: new THREE.Vector3(0.4104, 0.4838, 0.5472) },
      _Glossiness: { value: 0.1 }, _SnowGloss: { value: 1 }, _RockGloss: { value: 0.7 }, _Metallic: { value: 0 },
      _WaterLevel: { value: waterLevel }, _LodHideDistance: { value: 1e6 }, _LodHideModifier: { value: 0 },
      _UVScale: { value: 0.5 }, _BumpScale: { value: 1 }, _Wet: { value: 0 },
      _NoiseTex: { value: noise }, _SkyAlphaTexture: { value: white }, _ClearedMaskTex: { value: null },
      _RockNormal: { value: rock }, _SnowNormal: { value: snow }, _CliffNormal: { value: cliff },
      _MistlandsCliffNormal: { value: mistCliff }, _ColorVarietyNoise: { value: variety },
      _CurlNoise: { value: noise3D(32, 7) }, _SnowGlintOffsetNoise: { value: noise3D(32, 11) },
      _PavedNormal: { value: paved }, _DiffuseArrayTex: { value: dArr }, _NormalArrayTex: { value: nArr }
    },
    side: THREE.DoubleSide
  });

  const sky = createSky(shared);
  const t0 = performance.now();
  return {
    terrain, sky,
    createWater: (size, heightTex, region) => createWater(shared, size, heightTex, region, waterLevel),
    update(camera) {
      camUnity.set(camera.position.x + offset.x, camera.position.y, offset.z - camera.position.z);
      const t = (performance.now() - t0) / 1000;
      shared._Time.value.set(t / 20, t, t * 2, t * 3);
      sky.position.copy(camera.position);
    }
  };
}

function createSky(shared) {
  const mat = new THREE.ShaderMaterial({
    uniforms: { ...shared, uTop: { value: DAY.skyTop }, uHorizon: { value: DAY.skyHorizon } },
    vertexShader: /* glsl */`
      varying vec3 vDir;
      void main() { vDir = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); gl_Position.z = gl_Position.w; }`,
    fragmentShader: /* glsl */`
      uniform vec3 uTop, uHorizon, uSunDir, uSunColor;
      uniform vec4 _Time;
      ${commonGLSL}
      varying vec3 vDir;
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
        gl_FragColor = vec4(toSRGB(col), 1.0);
      }`,
    side: THREE.BackSide, depthWrite: false, fog: false
  });
  const m = new THREE.Mesh(new THREE.SphereGeometry(2000, 48, 24), mat);
  m.renderOrder = -10; m.frustumCulled = false;
  return m;
}

// Ocean surface. heightTex holds the ground height for the editing area (texel (gx, gz)); outside it
// the sea counts as deep.
function createWater(shared, size, heightTex, region, waterLevel) {
  const mat = new THREE.ShaderMaterial({
    uniforms: {
      ...shared, uHeight: { value: heightTex }, uRegion: { value: new THREE.Vector4(region.x, region.z, region.w, region.h) },
      uWater: { value: waterLevel }, uTop: { value: DAY.skyTop }, uHorizon: { value: DAY.skyHorizon }
    },
    vertexShader: /* glsl */`
      uniform vec3 uOffset;
      varying vec3 vWorld;
      void main() { vec4 w = modelMatrix * vec4(position, 1.0); vWorld = vec3(w.x + uOffset.x, w.y, uOffset.z - w.z);
        gl_Position = projectionMatrix * viewMatrix * w; }`,
    fragmentShader: /* glsl */`
      uniform sampler2D uHeight;
      uniform vec4 uRegion;
      uniform float uWater;
      uniform vec3 uTop, uHorizon, uSunDir, uSunColor, _WorldSpaceCameraPos;
      uniform vec4 _Time;
      ${commonGLSL}
      varying vec3 vWorld;
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
        float ground = inside ? texture2D(uHeight, uv).r : uWater - 60.0;
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
        gl_FragColor = vec4(toSRGB(applyFogDir(col, dist, -v, uSunDir)), alpha);
      }`,
    transparent: true, depthWrite: false, fog: false
  });
  const m = new THREE.Mesh(new THREE.PlaneGeometry(size, size, 1, 1), mat);
  m.rotation.x = -Math.PI / 2;
  return m;
}
