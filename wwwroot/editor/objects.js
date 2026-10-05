// World objects in the editing area (trees, rocks, pieces...): one record per object, drawn as one
// batch of instanced meshes per prefab. New objects (planted, pasted, replacements) get negative ids
// and are copied from an existing object of the same prefab when the world is saved.

// Valheim's string hash (String.GetStableHashCode), used for prefab ids.
export function stableHash(s) {
  let a = 5381 | 0, b = a;
  for (let i = 0; i < s.length; i += 2) {
    a = (Math.imul(a, 33) ^ s.charCodeAt(i)) | 0;
    if (i === s.length - 1) break;
    b = (Math.imul(b, 33) ^ s.charCodeAt(i + 1)) | 0;
  }
  return (a + Math.imul(b, 1566083941)) | 0;
}

export const KINDS = ['buildings', 'ruins', 'trees', 'rocks', 'ore', 'bushes', 'pickables', 'other'];
export const KIND_LABEL = { buildings: 'Buildings', ruins: 'Ruins & structures', trees: 'Trees & logs', rocks: 'Rocks', ore: 'Ore & deposits', bushes: 'Bushes & shrubs', pickables: 'Pickables', other: 'Other' };

export function objectKind(name, pieceNames) {
  if (pieceNames.has(name) || /^goblin_|^shipwreck_|^Statue|^BossStone_|^dungeon_|crypt_gate|^CastleKit_|^StartPlatform|^Beehive|^CargoCrate|^barrell|^RockDolmen|^TreasureChest/i.test(name)) return 'ruins';
  if (/^Pickable_|^Pickable|mushroom|Dandelion|Thistle/i.test(name)) return 'pickables';
  if (/tree|beech|birch|oak|^fir|pine|stub|log|root|yggdrasil|ashwood|sapling/i.test(name)) return 'trees';
  if (/tombstone/i.test(name)) return 'other';
  if (/minerock|copper|tin|silver|vein|obsidian|mudpile|guck|leviathan|tar|flametal/i.test(name)) return 'ore';
  if (/rock|stone|ice|boulder|cliff|pillar/i.test(name)) return 'rocks';
  if (/bush|shrub|fern|ormbunke|vines|cloudberry/i.test(name)) return 'bushes';
  return 'other';
}

export function createObjects(ed) {
  const { THREE } = ed;
  const records = new Map();   // id -> { id, prefab, name, kind, x, y, z, rx, ry, rz, scale, deleted, added }
  const batches = new Map();   // name -> { name, kind, parts, ids, meshes }
  let nextId = -1;
  const state = { names: {}, pieceNames: new Set(), templates: new Set(), byName: new Map() };
  const D = Math.PI / 180;
  const e = new THREE.Euler(), q = new THREE.Quaternion(), pos = new THREE.Vector3(), sc = new THREE.Vector3(), place = new THREE.Matrix4(), m = new THREE.Matrix4();
  const ZERO = new THREE.Matrix4().makeScale(0, 0, 0);

  // Unity world -> three.js (z mirrored, centred on the area). Rotations: Unity Euler (z, x, y order);
  // mirroring z negates the quaternion's x and y.
  function placement(r, rootScale) {
    q.setFromEuler(e.set(r.rx * D, r.ry * D, r.rz * D, 'YXZ'));
    q.set(-q.x, -q.y, q.z, q.w);
    pos.set(r.x - ed.originX - ed.cx, r.y, -(r.z - ed.originZ - ed.cz));
    if (r.scale > 0) sc.set(r.scale, r.scale, r.scale); else sc.fromArray(rootScale);
    return place.compose(pos, q, sc);
  }

  // Objects added in the editor are drawn in their own group, shown whatever the View switches say,
  // so newly placed bushes or pickables never vanish and get placed twice.
  function groupFor(kind, isNew) { return isNew ? ed.newGroup : kind === 'buildings' ? ed.buildings : ed.objectGroups[kind]; }

  // Green markers on new objects that are not saved / applied yet.
  const markers = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color: 0x5dff8a, size: 9, sizeAttenuation: false, depthTest: false }));
  markers.renderOrder = 22; markers.frustumCulled = false; ed.newGroup.add(markers);
  function refreshMarkers() {
    const pts = [];
    for (const r of records.values()) if (r.added && !r.deleted && !r.applied) pts.push(new THREE.Vector3(r.x - ed.originX - ed.cx, r.y + 0.4, -(r.z - ed.originZ - ed.cz)));
    markers.geometry.setFromPoints(pts);
  }

  async function batchFor(name, kind, isNew = false) {
    const key = isNew ? name + '#new' : name;
    if (batches.has(key)) return batches.get(key);
    const b = { name, kind, isNew, parts: null, ids: [], meshes: [], ready: null };
    batches.set(key, b);
    b.ready = ed.pieceModel(name).catch(() => null).then(parts => { b.parts = parts?.length ? parts : null; });
    await b.ready;
    return b;
  }

  function rebuild(b) {
    const group = groupFor(b.kind, b.isNew);
    for (const im of b.meshes) { group.remove(im); im.dispose(); }
    b.meshes = [];
    ed.unregister(b.ids);
    if (!b.parts || !b.ids.length) return;
    for (const part of b.parts) {
      const im = new THREE.InstancedMesh(part.geometry, part.material, b.ids.length);
      b.ids.forEach((id, k) => {
        const r = records.get(id);
        im.setMatrixAt(k, r.deleted ? ZERO : m.multiplyMatrices(placement(r, b.parts.rootScale), part.matrix));
      });
      im.instanceMatrix.needsUpdate = true; im.computeBoundingSphere();
      ed.register(im, b.ids, b.name);
      group.add(im);
      b.meshes.push(im);
    }
  }

  // Objects from /api/objects (stride 11: x, y, z, rx, ry, rz, sx, sy, sz, type, id).
  async function load(objs, names, pieceNames, templates) {
    Object.assign(state, { names, pieceNames, templates: new Set(templates) });
    for (const [hash, name] of Object.entries(names)) state.byName.set(name, +hash);
    const d = objs.data, byName = new Map();
    for (let i = 0; i < d.length; i += 11) {
      const prefab = objs.types[d[i + 9]], name = names[prefab];
      if (!name) continue;
      const id = d[i + 10];
      const kind = id < 0 && pieceNames.has(name) ? 'buildings' : objectKind(name, pieceNames);
      records.set(id, { id, prefab, name, kind, x: d[i], y: d[i + 1], z: d[i + 2], rx: d[i + 3], ry: d[i + 4], rz: d[i + 5], scale: d[i + 6], deleted: false, added: id < 0 });
      if (id <= nextId) nextId = id - 1;
      const key = id < 0 ? name + '#new' : name;
      if (!byName.has(key)) byName.set(key, []);
      byName.get(key).push(id);
    }
    await Promise.all([...byName].map(async ([key, ids]) => {
      const r0 = records.get(ids[0]);
      const b = await batchFor(r0.name, r0.kind, r0.added);
      b.ids.push(...ids);
      rebuild(b);
    }));
    refreshMarkers();
    return counts();
  }

  function counts() {
    const c = {};
    for (const r of records.values()) if (!r.deleted && r.kind !== 'buildings' && batches.get(r.name)?.parts) c[r.kind] = (c[r.kind] ?? 0) + 1;
    return c;
  }

  // Player pieces drawn by the buildings code: records only (for area tools and copy).
  function addPieceRecord(id, name, x, y, z, ry) {
    records.set(id, { id, prefab: stableHash(name), name, kind: 'buildings', x, y, z, rx: 0, ry, rz: 0, scale: 0, deleted: false, added: false, external: true });
  }

  // New objects: [{ name, x, y, z, rx, ry, rz, scale }] in Unity world coordinates. Returns their ids.
  // post = false keeps them local until flush() (the plant brush sends a whole stroke at once).
  const unsent = [];
  async function add(list, { post = true } = {}) {
    const ids = [], touched = new Set();
    for (const o of list) {
      const prefab = state.byName.get(o.name) ?? stableHash(o.name);
      if (!state.templates.has(prefab)) continue;
      const id = nextId--;
      const kind = state.pieceNames.has(o.name) ? 'buildings' : objectKind(o.name, state.pieceNames);
      const r = { id, prefab, name: o.name, kind, x: o.x, y: o.y, z: o.z, rx: o.rx ?? 0, ry: o.ry ?? 0, rz: o.rz ?? 0, scale: o.scale ?? 0, deleted: false, added: true };
      records.set(id, r);
      ids.push(id);
      unsent.push(r);
      const b = await batchFor(o.name, kind, true);
      b.ids.push(id);
      touched.add(b);
    }
    for (const b of touched) rebuild(b);
    refreshMarkers();
    if (post) await flush();
    return ids;
  }

  async function flush() {
    if (!unsent.length) return;
    const body = unsent.splice(0).map(r => ({ id: r.id, prefab: r.prefab, x: r.x, y: r.y, z: r.z, rx: r.rx, ry: r.ry, rz: r.rz, scale: r.scale }));
    try {
      const res = await (await fetch('/api/objects/add', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).json();
      ed.showPendingFrom(res.pending);
      ed.changed?.();
    } catch (err) {
      ed.msg(`Could not send the new objects to the editor: ${err.message}`, true);
    }
  }

  // Mark records deleted or not (the editor's setDeleted hides the instances and tells the server).
  function markDeleted(ids, deleted) { for (const id of ids) { const r = records.get(id); if (r) r.deleted = deleted; } refreshMarkers(); }

  // Live mode: the game has the new objects now; they stay drawn but lose their marker.
  function markApplied() { for (const r of records.values()) if (r.added) r.applied = true; refreshMarkers(); }

  function alive() { return [...records.values()].filter(r => !r.deleted); }

  // Types that can be created (an object of the prefab exists in the world to copy) and have a model.
  function creatableTypes() {
    const out = [];
    for (const [hash, name] of Object.entries(state.names)) {
      if (!state.templates.has(+hash)) continue;
      out.push({ name, prefab: +hash, kind: objectKind(name, state.pieceNames) });
    }
    return out.sort((a, b) => a.name.localeCompare(b.name));
  }

  // Placement matrix (three.js space) of a record-like { x, y, z, rx, ry, rz, scale }, for previews.
  const matrixFor = (r, rootScale, out) => out.copy(placement(r, rootScale));

  return { records, load, add, flush, counts, addPieceRecord, markDeleted, alive, creatableTypes, state, stableHash, matrixFor, markApplied };
}
