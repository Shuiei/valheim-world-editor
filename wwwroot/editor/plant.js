// Plant brush (like WorldPainter's tree layers): paint trees, rocks or bushes onto the ground under the
// brush with a density and a minimum spacing; Shift + drag removes the chosen kinds again. New objects
// are copies of an object of the same kind already in the world, made when the world is saved.
import { KINDS, KIND_LABEL } from './objects.js';

export function createPlant(ed) {
  const { $, W, H } = ed;
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

  let stroke = null, at = null, last = 0;
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
    const names = [...chosen];
    if (!names.length) return;
    // Attempts per frame: the brush area's share of the density, scaled by strength and time.
    const want = v('plDensity') / 100 * Math.PI * r * r * Math.min(dt, 0.1) * (0.5 + 2.5 * ed.strength);
    let n = Math.floor(want) + (Math.random() < want % 1 ? 1 : 0);
    const spacing = v('plSpacing'), smin = v('plSmin') / 100, smax = Math.max(smin, v('plSmax') / 100), tilt = v('plTilt');
    const list = [];
    for (let tries = 0; n > 0 && tries < n * 6; tries++) {
      const a = Math.random() * Math.PI * 2, d = Math.sqrt(Math.random()) * r;
      const gx = at.gx + Math.cos(a) * d, gz = at.gz + Math.sin(a) * d;
      if (gx < 1 || gz < 1 || gx > W - 2 || gz > H - 2) continue;
      if (!ed.mask(Math.round(gz) * W + Math.round(gx))) continue;
      if (!free(stroke.hash, stroke.cell, gx, gz, spacing)) continue;
      const k = `${Math.floor(gx / stroke.cell)},${Math.floor(gz / stroke.cell)}`;
      (stroke.hash.get(k) ?? stroke.hash.set(k, []).get(k)).push([gx, gz]);
      const y = heightAt(gx, gz);
      if (y < ed.WATER - 0.3 && !names.some(nm => /kelp|seaweed/i.test(nm))) continue;  // not under water
      list.push({ name: names[Math.floor(Math.random() * names.length)], x: ed.originX + gx, y: y - 0.05, z: ed.originZ + gz,
        rx: (Math.random() * 2 - 1) * tilt, ry: Math.random() * 360, rz: (Math.random() * 2 - 1) * tilt, scale: smin + Math.random() * (smax - smin) });
      n--;
    }
    if (list.length) ed.objects.add(list, { post: false }).then(ids => stroke?.added.push(...ids));
  }
  ed.handlers.plant = {
    down(e, hit) {
      if (!hit) return;
      ed.el.setPointerCapture(e.pointerId);
      const cell = Math.max(1, v('plSpacing'));
      stroke = { erase: e.shiftKey, added: [], deleted: [], unplanted: [], cell, hash: e.shiftKey ? null : buildHash(cell) };
      at = hit;
    },
    move(e, hit) { at = hit; ed.showStatusFor?.(hit); ed.updateRing?.(); },
    async up() {
      if (!stroke) return;
      const s = stroke; stroke = null;
      await ed.objects.flush();
      // Planted and removed again in the same stroke: nothing to remember.
      const added = s.added.filter(id => !s.unplanted.includes(id)), deleted = [...s.deleted, ...s.unplanted.filter(id => !s.added.includes(id))];
      if (added.length || deleted.length) ed.pushHistory({ ...(added.length ? { added } : {}), ...(deleted.length ? { deleted } : {}) });
      ed.msg(s.erase ? `Removed ${deleted.length} object(s).` : `Planted ${added.length} object(s). Ctrl+Z removes them; Save writes them to the world.`);
    }
  };
  ed.frame ??= [];
  ed.frame.push(step);
}
