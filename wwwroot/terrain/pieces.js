// Real models for player-built pieces, exported from the game's asset bundles into models/:
// pieces/<name>.json lists the parts (mesh, submesh, material, matrix in three.js space), meshes/<id>.bin
// holds interleaved position, normal, uv (float32) followed by the uint32 indices of each submesh.
import * as THREE from 'three';

const root = new URL('../models/', import.meta.url);
const meshCache = new Map(), texCache = new Map(), matCache = new Map();
let materialsPromise = null, meshInfoPromise = null;
const loader = new THREE.TextureLoader();

const json = path => fetch(new URL(path, root)).then(r => { if (!r.ok) throw new Error(`${path}: ${r.status}`); return r.json(); });

function texture(name, srgb) {
  const key = name + (srgb ? ':s' : '');
  if (!texCache.has(key)) {
    texCache.set(key, new Promise(resolve => loader.load(new URL(`tex/${name}`, root).href, t => {
      t.flipY = false; // exported bottom row first, like Unity
      t.wrapS = t.wrapT = THREE.RepeatWrapping;
      t.colorSpace = srgb ? THREE.SRGBColorSpace : THREE.NoColorSpace;
      t.anisotropy = 4;
      resolve(t);
    }, undefined, () => resolve(null))));
  }
  return texCache.get(key);
}

function material(id) {
  if (!matCache.has(id)) {
    matCache.set(id, (async () => {
      const all = await (materialsPromise ??= json('materials.json'));
      const m = all[id] ?? {};
      const mat = new THREE.MeshStandardMaterial({ roughness: 1 - (m.glossiness ?? 0.1) * 0.8, metalness: m.metallic ?? 0 });
      if (m.color) mat.color.setRGB(m.color[0], m.color[1], m.color[2], THREE.SRGBColorSpace);
      const [map, normal] = await Promise.all([m.map ? texture(m.map, true) : null, m.normal ? texture(m.normal, false) : null]);
      const uv = t => { if (t && m.uv) { t = t.clone(); t.repeat.set(m.uv[0], m.uv[1]); t.offset.set(m.uv[2], m.uv[3]); t.needsUpdate = true; } return t; };
      if (map) mat.map = uv(map);
      if (normal) { mat.normalMap = uv(normal); }
      if (m.cutoff != null) mat.alphaTest = m.cutoff;
      if (m.doubleSided) mat.side = THREE.DoubleSide;
      // Blended materials are nearly all foliage: alpha testing sorts correctly and looks the same.
      if (m.transparent && m.cutoff == null) mat.alphaTest = 0.5;
      mat.userData.baseTransparent = mat.transparent; mat.userData.baseOpacity = mat.opacity;
      return mat;
    })());
  }
  return matCache.get(id);
}

function geometries(id) {
  if (!meshCache.has(id)) {
    meshCache.set(id, (async () => {
      const info = (await (meshInfoPromise ??= json('meshinfo.json')))[id];
      const buf = await fetch(new URL(`meshes/${id}.bin`, root)).then(r => r.arrayBuffer());
      const v = new THREE.InterleavedBuffer(new Float32Array(buf, 0, info.v * 8), 8);
      const pos = new THREE.InterleavedBufferAttribute(v, 3, 0), nor = new THREE.InterleavedBufferAttribute(v, 3, 3), uv = new THREE.InterleavedBufferAttribute(v, 2, 6);
      let off = info.v * 32;
      return info.sub.map(count => {
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', pos); g.setAttribute('normal', nor); g.setAttribute('uv', uv);
        // Mirroring z (left- to right-handed) turns Unity's front faces into back faces: swap two corners.
        const idx = new Uint32Array(buf, off, count);
        for (let t = 0; t < count; t += 3) { const k = idx[t + 1]; idx[t + 1] = idx[t + 2]; idx[t + 2] = k; }
        g.setIndex(new THREE.BufferAttribute(idx, 1));
        off += count * 4;
        g.computeBoundingSphere();
        return g;
      });
    })());
  }
  return meshCache.get(id);
}

// Loads one piece type: [{ geometry, material, matrix }], or null if there is no model for it.
export async function pieceModel(name) {
  let desc;
  try { desc = await json(`pieces/${encodeURIComponent(name)}.json`); } catch { return null; }
  if (!desc.parts.length) return null;
  const parts = await Promise.all(desc.parts.map(async p => {
    const [geos, mat] = await Promise.all([geometries(p.mesh), material(p.mat)]);
    return { geometry: geos[p.sub], material: mat, matrix: new THREE.Matrix4().fromArray(p.m) };
  }));
  const result = parts.filter(p => p.geometry);
  result.rootScale = desc.rootScale ?? [1, 1, 1];
  return result;
}

// See-through mode for every loaded piece material.
export async function setSeeThrough(on) {
  for (const p of matCache.values()) {
    const m = await p;
    m.transparent = on || m.userData.baseTransparent;
    m.opacity = on ? 0.35 : m.userData.baseOpacity;
    m.depthWrite = !m.transparent;
    m.needsUpdate = true;
  }
}
