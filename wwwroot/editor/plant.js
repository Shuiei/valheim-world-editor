// Plant brush (like WorldPainter's tree layers): paint trees, rocks or bushes onto the ground under the
// brush with a density and a minimum spacing; Shift + drag removes the chosen kinds again. New objects
// are copies of an object of the same kind already in the world, made when the world is saved.
import { KINDS, KIND_LABEL } from './objects.js';

export function createPlant(ed) {
  const { $, W, H, THREE } = ed;
  const panel = document.createElement('div');
  panel.id = 'plantPanel';
  panel.innerHTML = `
    <input id="plSearch" type="search" placeholder="Search kinds (oak, rock, bush…)" style="width:100%;margin:2px 0 6px">
    <div id="plList" class="plList"></div>
    <div class="hint" id="plChosen"></div>
    <label class="field">Density <input id="plDensity" type="range" min="0.2" max="20" step="0.2" value="3"><span id="plDensityV"></span></label>
    <label class="field">Spacing <input id="plSpacing" type="range" min="0.5" max="15" step="0.5" value="4"><span id="plSpacingV"></span></label>
    <label class="field">Size <span class="pair"><input id="plSmin" type="number" min="10" max="300" step="5" value="80"><input id="plSmax" type="number" min="10" max="300" step="5" value="120"></span><span>%</span></label>
    <label class="field">Tilt <input id="plTilt" type="range" min="0" max="20" step="1" value="3"><span id="plTiltV"></span></label>
    <div class="hint" id="plPreview" style="color:var(--text)"></div>
    <div class="hint">Density is objects per 100 m². Spacing keeps them apart (also from what is already there). Shift + drag removes the chosen kinds. The Mask applies.</div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['plant'] });
  const style = document.createElement('style');
  style.textContent = `
    .plList { max-height: 210px; overflow-y: auto; border: 1px solid var(--line); border-radius: 7px; padding: 4px; }
    .plList h4 { margin: 6px 4px 2px; font-size: 10.5px; text-transform: uppercase; letter-spacing: .06em; color: var(--muted); font-weight: 600; }
    .plList label { display: flex; gap: 6px; align-items: center; padding: 2px 4px; border-radius: 5px; cursor: pointer; font-size: 12px; }
    .plList label:hover { background: rgba(255,255,255,.04); }`;
  document.head.appendChild(style);

  const chosen = new Set(['Beech1']);
  const v = id => +$(id).value;
  function syncLabels() {
    $('plDensityV').textContent = v('plDensity').toFixed(1);
    $('plSpacingV').textContent = `${v('plSpacing')} m`;
    $('plTiltV').textContent = `${v('plTilt')}°`;
    $('plChosen').textContent = chosen.size ? `Planting: ${[...chosen].join(', ')}` : 'Tick one or more kinds to plant.';
  }
  ['plDensity', 'plSpacing', 'plTilt'].forEach(id => $(id).addEventListener('input', syncLabels));
  function fillList() {
    const q = $('plSearch').value.trim().toLowerCase();
    // Nature kinds first; pieces and spoilers can be planted too but are listed last.
    const order = ['trees', 'rocks', 'bushes', 'pickables', 'ore', 'other', 'ruins', 'buildings'];
    const groups = {};
    for (const t of ed.objects.creatableTypes()) if (!q || t.name.toLowerCase().includes(q)) (groups[t.kind] ??= []).push(t);
    $('plList').innerHTML = order.filter(k => groups[k]).map(k => `<h4>${KIND_LABEL[k]}</h4>` +
      groups[k].map(t => `<label><input type="checkbox" value="${t.name}" ${chosen.has(t.name) ? 'checked' : ''}>${t.name}</label>`).join('')).join('') || '<div class="hint">Nothing matches.</div>';
    $('plList').querySelectorAll('input').forEach(c => c.onchange = () => { c.checked ? chosen.add(c.value) : chosen.delete(c.value); syncLabels(); });
  }
  $('plSearch').addEventListener('input', fillList);
  (ed.onObjects ??= []).push(fillList);
  syncLabels();

  // Ground height between grid points.
  function heightAt(fx, fz) {
    const x = Math.max(0, Math.min(W - 1.001, fx)), z = Math.max(0, Math.min(H - 1.001, fz));
    const x0 = Math.floor(x), z0 = Math.floor(z), tx = x - x0, tz = z - z0;
    const h = (a, b) => ed.height(b * W + a);
    return (h(x0, z0) * (1 - tx) + h(x0 + 1, z0) * tx) * (1 - tz) + (h(x0, z0 + 1) * (1 - tx) + h(x0 + 1, z0 + 1) * tx) * tz;
  }

  let stroke = null, at = null;
  // Spatial hash of every object's position (grid coordinates) for the spacing check.
  function buildHash(cell) {
    const map = new Map();
    for (const r of ed.objects.alive()) {
      const gx = r.x - ed.originX, gz = r.z - ed.originZ, k = `${Math.floor(gx / cell)},${Math.floor(gz / cell)}`;
      (map.get(k) ?? map.set(k, []).get(k)).push([gx, gz]);
    }
    return map;
  }
  function free(hash, cell, gx, gz, spacing) {
    const cx = Math.floor(gx / cell), cz = Math.floor(gz / cell);
    for (let dz = -1; dz <= 1; dz++) for (let dx = -1; dx <= 1; dx++) for (const [x, z] of hash.get(`${cx + dx},${cz + dz}`) ?? []) if (Math.hypot(x - gx, z - gz) < spacing) return false;
    return true;
  }
  const addToHash = (hash, cell, gx, gz) => { const k = `${Math.floor(gx / cell)},${Math.floor(gz / cell)}`; (hash.get(k) ?? hash.set(k, []).get(k)).push([gx, gz]); };
  const underwaterOk = names => names.some(nm => /kelp|seaweed/i.test(nm));
  // One placement at grid point (gx, gz) with the given random draws, or null when it may not go there.
  function placementAt(gx, gz, d, names, hash, cell) {
    if (gx < 1 || gz < 1 || gx > W - 2 || gz > H - 2) return null;
    if (!ed.mask(Math.round(gz) * W + Math.round(gx))) return null;
    if (!free(hash, cell, gx, gz, v('plSpacing'))) return null;
    const y = heightAt(gx, gz);
    if (y < ed.WATER - 0.3 && !underwaterOk(names)) return null;  // not under water
    const smin = v('plSmin') / 100, smax = Math.max(smin, v('plSmax') / 100), tilt = v('plTilt');
    return { name: names[Math.floor(d.t * names.length) % names.length], x: ed.originX + gx, y: y - 0.05, z: ed.originZ + gz,
      rx: d.rx * tilt, ry: d.ry * 360, rz: d.rz * tilt, scale: smin + d.s * (smax - smin), gx, gz };
  }
  const draw = () => ({ t: Math.random(), rx: Math.random() * 2 - 1, ry: Math.random(), rz: Math.random() * 2 - 1, s: Math.random() });

  // ---- Preview: the objects one click would place under the cursor, drawn as see-through ghosts.
  // The layout (offsets from the cursor) stays the same while the cursor moves; R shuffles it.
  let pattern = [], patternKey = '', hash = null, hashCell = 0, hashStale = true, preview = [];
  const ghostGroup = new THREE.Group(); ed.scene.add(ghostGroup);
  // Kinds without a model (or still loading) show as dots, so every placement is visible.
  const dots = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color: 0x9fe0ff, size: 7, sizeAttenuation: false, depthTest: false }));
  dots.renderOrder = 6; ghostGroup.add(dots);
  const ghosts = new Map();   // name -> { meshes, parts } (loading: null)
  const ghostMat = new Map(); // material -> see-through copy
  const m4 = new THREE.Matrix4();
  (ed.onObjectsChanged ??= []).push(() => { hashStale = true; });
  function settingsKey() { return [ed.radius, v('plDensity'), v('plSpacing'), [...chosen].join(',')].join('|'); }
  function makePattern() {
    const r = ed.radius, spacing = v('plSpacing');
    const n = Math.min(400, Math.round(v('plDensity') / 100 * Math.PI * r * r));
    pattern = [];
    // Dart throwing: random points in the disc, at least the spacing apart.
    for (let tries = 0; pattern.length < n && tries < n * 30; tries++) {
      const a = Math.random() * Math.PI * 2, d = Math.sqrt(Math.random()) * r;
      const dx = Math.cos(a) * d, dz = Math.sin(a) * d;
      if (pattern.some(p => Math.hypot(p.dx - dx, p.dz - dz) < spacing)) continue;
      pattern.push({ dx, dz, d: draw() });
    }
    patternKey = settingsKey();
  }
  function ghostFor(name) {
    if (ghosts.has(name)) return ghosts.get(name);
    ghosts.set(name, null);
    ed.pieceModel(name).then(parts => {
      if (!parts?.length) { ghosts.set(name, null); return; }
      const meshes = parts.map(part => {
        if (!ghostMat.has(part.material)) {
          const mm = part.material.clone();
          mm.transparent = true; mm.opacity = 0.5; mm.depthWrite = false;
          mm.emissive?.setRGB(0.15, 0.25, 0.35);
          ghostMat.set(part.material, mm);
        }
        const im = new THREE.InstancedMesh(part.geometry, ghostMat.get(part.material), 400);
        im.count = 0; im.frustumCulled = false; im.renderOrder = 5;
        ghostGroup.add(im);
        return im;
      });
      ghosts.set(name, { parts, meshes });
      updatePreview();
    }).catch(() => {});
    return null;
  }
  function updatePreview() {
    const show = ed.tool === 'plant' && at && !stroke && !held.shift && chosen.size > 0;
    ghostGroup.visible = show;
    if (!show) { preview = []; info(); return; }
    if (settingsKey() !== patternKey) makePattern();
    const cell = Math.max(1, v('plSpacing'));
    if (hashStale || cell !== hashCell) { hash = buildHash(cell); hashCell = cell; hashStale = false; }
    const names = [...chosen], local = new Map(), placed = [];
    for (const p of pattern) {
      const o = placementAt(at.gx + p.dx, at.gz + p.dz, p.d, names, hash, cell);
      if (o) placed.push(o);
    }
    preview = placed;
    const byName = new Map();
    for (const o of placed) (byName.get(o.name) ?? byName.set(o.name, []).get(o.name)).push(o);
    for (const [name, g] of ghosts) if (g && !byName.has(name)) for (const im of g.meshes) im.count = 0;
    const dotPts = [];
    for (const [name, list] of byName) {
      const g = ghostFor(name);
      if (!g) { for (const o of list) dotPts.push(new THREE.Vector3(o.gx - ed.cx, o.y + 0.6, -(o.gz - ed.cz))); continue; }
      g.parts.forEach((part, pi) => {
        const im = g.meshes[pi];
        list.forEach((o, k) => im.setMatrixAt(k, m4.multiplyMatrices(ed.objects.matrixFor(o, g.parts.rootScale, new THREE.Matrix4()), part.matrix)));
        im.count = list.length; im.instanceMatrix.needsUpdate = true;
      });
    }
    dots.geometry.setFromPoints(dotPts);
    info();
  }
  function info() {
    if (ed.tool !== 'plant') return;
    if (!chosen.size) { $('plPreview').textContent = 'Tick at least one kind to plant.'; return; }
    if (held.shift) { $('plPreview').textContent = 'Shift: drag to remove the chosen kinds under the brush.'; return; }
    $('plPreview').innerHTML = at ? `<b>${preview.length}</b> object(s) shown under the cursor. Click places exactly these; drag paints more. <kbd>R</kbd> new layout.` : 'Move over the ground to see what a click would place.';
  }
  const held = { shift: false };
  addEventListener('keydown', e => { if (e.key === 'Shift' && !held.shift) { held.shift = true; updatePreview(); } });
  addEventListener('keyup', e => { if (e.key === 'Shift') { held.shift = false; updatePreview(); } });
  ['plDensity', 'plSpacing', 'plSmin', 'plSmax', 'plTilt'].forEach(id => $(id).addEventListener('input', () => { if (id === 'plDensity' || id === 'plSpacing') patternKey = ''; else for (const p of pattern) p.d = { ...p.d }; updatePreview(); }));
  $('radius').addEventListener('input', () => updatePreview());
  $('plList').addEventListener('change', () => updatePreview());
  ed.onToolChange.push(t => { if (t === 'plant') hashStale = true; updatePreview(); });

  function step(dt) {
    if (!stroke || !at) return;
    const r = ed.radius;
    if (stroke.erase) {
      const ids = [];
      for (const o of ed.objects.alive()) {
        if (!chosen.has(o.name)) continue;
        const gx = o.x - ed.originX, gz = o.z - ed.originZ;
        if (Math.hypot(gx - at.gx, gz - at.gz) > r) continue;
        if (!ed.mask(Math.round(gz) * W + Math.round(gx))) continue;
        ids.push(o.id);
      }
      if (ids.length) { ed.setDeleted(ids, true); for (const id of ids) id < 0 ? stroke.unplanted.push(id) : stroke.deleted.push(id); }
      return;
    }
    // Painting starts once the mouse moves; a plain click places the preview instead.
    if (!stroke.dragging) return;
    const names = [...chosen];
    if (!names.length) return;
    // Attempts per frame: the brush area's share of the density, scaled by strength and time.
    const want = v('plDensity') / 100 * Math.PI * r * r * Math.min(dt, 0.1) * (0.5 + 2.5 * ed.strength);
    let n = Math.floor(want) + (Math.random() < want % 1 ? 1 : 0);
    const list = [];
    for (let tries = 0; n > 0 && tries < n * 6; tries++) {
      const a = Math.random() * Math.PI * 2, d = Math.sqrt(Math.random()) * r;
      const o = placementAt(at.gx + Math.cos(a) * d, at.gz + Math.sin(a) * d, draw(), names, stroke.hash, stroke.cell);
      if (!o) continue;
      addToHash(stroke.hash, stroke.cell, o.gx, o.gz);
      list.push(o);
      n--;
    }
    if (list.length) ed.objects.add(list, { post: false }).then(ids => stroke?.added.push(...ids));
  }
  ed.handlers.plant = {
    down(e, hit) {
      if (!hit) return;
      ed.el.setPointerCapture(e.pointerId);
      const cell = Math.max(1, v('plSpacing'));
      // The click's placements are taken now, from what the preview shows.
      stroke = { erase: e.shiftKey, added: [], deleted: [], unplanted: [], cell, hash: e.shiftKey ? null : buildHash(cell), x: e.clientX, y: e.clientY, dragging: false, stamp: preview.slice() };
      at = hit;
      updatePreview();
    },
    move(e, hit) {
      at = hit; ed.showStatusFor?.(hit); ed.updateRing?.();
      if (stroke && !stroke.dragging && Math.hypot(e.clientX - stroke.x, e.clientY - stroke.y) > 6) {
        stroke.dragging = true;
        // Painting: the ground under the click gets the preview too, then the brush adds more.
        if (!stroke.erase && stroke.stamp.length) { for (const o of stroke.stamp) addToHash(stroke.hash, stroke.cell, o.gx, o.gz); ed.objects.add(stroke.stamp, { post: false }).then(ids => stroke?.added.push(...ids)); stroke.stamp = []; }
      }
      if (!stroke) updatePreview();
    },
    async up() {
      if (!stroke) return;
      const s = stroke; stroke = null;
      if (!s.erase && !s.dragging && s.stamp.length) s.added.push(...await ed.objects.add(s.stamp, { post: false }));
      await ed.objects.flush();
      hashStale = true;
      // Planted and removed again in the same stroke: nothing to remember.
      const added = s.added.filter(id => !s.unplanted.includes(id)), deleted = [...s.deleted, ...s.unplanted.filter(id => !s.added.includes(id))];
      if (added.length || deleted.length) ed.pushHistory({ ...(added.length ? { added } : {}), ...(deleted.length ? { deleted } : {}) });
      ed.msg(s.erase ? `Removed ${deleted.length} object(s).` : `Planted ${added.length} object(s). Ctrl+Z removes them; Save writes them to the world.`);
      makePattern();
      updatePreview();
    },
    key(e) {
      if (e.key.toLowerCase() === 'r' && !e.ctrlKey) { makePattern(); updatePreview(); return true; }
      return false;
    }
  };
  ed.frame ??= [];
  ed.frame.push(step);
}
