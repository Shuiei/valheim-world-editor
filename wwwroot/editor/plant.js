// Plant brush (like WorldPainter's tree layers): paint trees, rocks or bushes onto the ground under the
// brush with a density and a minimum spacing; Shift + drag removes the chosen kinds again. New objects
// are copies of an object of the same kind already in the world, made when the world is saved.
import { KINDS, KIND_LABEL } from './objects.js';

export function createPlant(ed) {
  const { $, W, H, THREE } = ed;
  const panel = document.createElement('div');
  panel.id = 'plantPanel';
  panel.innerHTML = `
    <div class="seg" id="plModes"><button data-m="brush" class="on" title="Paint under the brush">Brush</button><button data-m="line" title="Objects along a line you draw">Line</button><button data-m="grid" title="One object in the middle of each grid cell">Grid</button><button data-m="zone" title="Fill a shape you draw freely">Zone</button></div>
    <input id="plSearch" type="search" placeholder="Search kinds (oak, rock, bush…)" style="width:100%;margin:2px 0 6px">
    <div id="plList" class="plList"></div>
    <div class="hint" id="plChosen"></div>
    <label class="field">Density <input id="plDensity" type="range" min="0.2" max="20" step="0.2" value="3"><span id="plDensityV"></span></label>
    <label class="field">Spacing <input id="plSpacing" type="range" min="0.5" max="15" step="0.5" value="4"><span id="plSpacingV"></span></label>
    <label class="field">Size <span class="pair"><input id="plSmin" type="number" min="10" max="300" step="5" value="80"><input id="plSmax" type="number" min="10" max="300" step="5" value="120"></span><span>%</span></label>
    <label class="field">Tilt <input id="plTilt" type="range" min="0" max="20" step="1" value="3"><span id="plTiltV"></span></label>
    <label class="field">Rotation <input id="plRot" type="range" min="-180" max="180" step="1" value="0"><span id="plRotV"></span></label>
    <label class="check"><input type="checkbox" id="plRandomYaw" checked> Random facing (off: all face the rotation)</label>
    <label class="check"><input type="checkbox" id="plSingle"> One at a time, exactly at the cursor</label>
    <label class="check" title="Saplings and crops need free space around them to grow (their grow radius in the game)"><input type="checkbox" id="plGrow" checked> Leave saplings room to grow</label>
    <div id="plLineBox" hidden>
      <div class="seg" id="plLineShape"><button data-ls="points" class="on" title="Click points along the way, or hold and drag to draw freely">Points</button><button data-ls="circle" title="Press at the centre and drag out to the size you want">Circle</button><button data-ls="rect" title="Press at one corner and drag to the opposite corner">Rectangle</button></div>
      <label class="check" title="Each piece starts where the last one ends, at the game's snap points, like the hammer snaps them"><input type="checkbox" id="plSnap"> End to end (snap together, like in game)</label>
      <label class="check" id="plLoopRow"><input type="checkbox" id="plLoop"> Close the loop (back to the first point)</label>
      <label class="field">Every <input id="plEvery" type="range" min="0.5" max="30" step="0.5" value="4"><span id="plEveryV"></span></label>
      <label class="field">Wiggle <input id="plWiggle" type="range" min="0" max="5" step="0.25" value="0"><span id="plWiggleV"></span></label>
      <label class="check"><input type="checkbox" id="plAlong"> Face along the line (plus the rotation; replaces random facing)</label>
      <label class="check"><input type="checkbox" id="plCurve" checked> Smooth curve through the points</label>
      <div class="hint" id="plLineHint">Click points along the route, or hold and drag. Backspace removes the last point.</div>
    </div>
    <div id="plZoneBox" hidden>
      <div class="seg" id="plFill"><button data-f="scatter" class="on" title="Random spots, using Density and Spacing">Scatter</button><button data-f="grid" title="One object in the middle of each cell inside the shape">Grid</button></div>
      <div class="hint">Click points around the zone, or hold and drag to draw it freely; the shape closes by itself. Backspace removes the last point; <kbd>,</kbd> <kbd>.</kbd> or Alt+wheel turn it.</div>
    </div>
    <label class="field" id="plCellRow" hidden>Cell <input id="plCell" type="range" min="1" max="30" step="0.5" value="4"><span id="plCellV"></span></label>
    <div id="plGridBox" hidden>
      <div class="hint">Drag a box on the ground; one object goes in the middle of each cell. <kbd>,</kbd> <kbd>.</kbd> or Alt+wheel turn it.</div>
    </div>
    <div class="row" id="plPlaceRow" hidden><button id="plPlace" class="primary">Place <kbd>Enter</kbd></button><button id="plClearShape">Clear <kbd>Esc</kbd></button></div>
    <div class="row"><button id="plNewLayout">New layout <kbd>R</kbd></button></div>
    <div class="hint" id="plPreview" style="color:var(--text)"></div>
    <div class="hint" id="plBrushHint">Density is objects per 100 m². Spacing keeps them apart (also from what is already there). Shift + drag removes the chosen kinds. The Mask applies.</div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['plant'] });
  const style = document.createElement('style');
  style.textContent = `
    .plList { max-height: 300px; overflow-y: auto; border: 1px solid var(--line); border-radius: 7px; padding: 4px; }
    .plList details + details { border-top: 1px solid var(--line); }
    .plList summary { display: flex; align-items: center; gap: 6px; padding: 5px 4px; cursor: pointer; list-style: none; font-size: 11px; text-transform: uppercase; letter-spacing: .06em; color: var(--muted); font-weight: 600; border-radius: 5px; }
    .plList summary::-webkit-details-marker { display: none; }
    .plList summary::before { content: '▸'; font-size: 10px; transition: transform .12s; }
    .plList details[open] > summary::before { transform: rotate(90deg); }
    .plList summary:hover { background: rgba(255,255,255,.04); color: var(--text); }
    .plList summary .n { margin-left: auto; text-transform: none; letter-spacing: 0; font-weight: 500; }
    .plList summary .n.sel { color: var(--accent); }
    .plList details > div { padding: 0 0 4px 10px; }
    .plList label { display: flex; gap: 6px; align-items: center; padding: 2px 4px; border-radius: 5px; cursor: pointer; font-size: 12px; }
    .plList label:hover { background: rgba(255,255,255,.04); }`;
  document.head.appendChild(style);

  // Saplings' grow radius and cultivated-ground need, by name (filled from /api/grow below).
  let grow = {};
  // Ticked kinds, remembered in this browser.
  let chosen = new Set(['Beech1']);
  try { const c = JSON.parse(localStorage.getItem('plantChosen') ?? 'null'); if (Array.isArray(c)) chosen = new Set(c); } catch { }
  const saveChosen = () => { try { localStorage.setItem('plantChosen', JSON.stringify([...chosen])); } catch { } };
  const v = id => +$(id).value;
  function syncLabels() {
    $('plDensityV').textContent = v('plDensity').toFixed(1);
    $('plSpacingV').textContent = `${v('plSpacing')} m`;
    $('plTiltV').textContent = `${v('plTilt')}°`;
    $('plRotV').textContent = `${v('plRot')}°`;
    $('plChosen').textContent = chosen.size ? `Planting: ${[...chosen].join(', ')}` : 'Tick one or more kinds to plant.';
    // Crops only grow on cultivated ground in the game.
    const cult = [...chosen].filter(n => grow[n]?.[1]);
    if (cult.length) $('plChosen').textContent += ` · ${cult.join(', ')} only grow${cult.length > 1 ? '' : 's'} on cultivated ground (paint it with Cultivate first).`;
  }
  ['plDensity', 'plSpacing', 'plTilt', 'plRot'].forEach(id => $(id).addEventListener('input', syncLabels));
  function fillList() {
    const q = $('plSearch').value.trim().toLowerCase();
    // Nature kinds first; pieces and spoilers can be planted too but are listed last.
    const order = ['trees', 'rocks', 'bushes', 'pickables', 'ore', 'other', 'ruins', 'buildings'];
    const groups = {};
    for (const t of ed.objects.creatableTypes()) if (!q || t.name.toLowerCase().includes(q)) (groups[t.kind] ??= []).push(t);
    // One collapsible section per category; searching opens every category with a match.
    $('plList').innerHTML = order.filter(k => groups[k]).map(k => `<details data-k="${k}" ${q || openKinds.has(k) ? 'open' : ''}>
      <summary>${KIND_LABEL[k]}<span class="n"></span></summary>
      <div>${groups[k].map(t => `<label><input type="checkbox" value="${t.name}" ${chosen.has(t.name) ? 'checked' : ''}>${t.name}</label>`).join('')}</div></details>`).join('') || '<div class="hint">Nothing matches.</div>';
    $('plList').querySelectorAll('input').forEach(c => c.onchange = () => { c.checked ? chosen.add(c.value) : chosen.delete(c.value); saveChosen(); syncLabels(); countKinds(); });
    $('plList').querySelectorAll('details').forEach(d => d.addEventListener('toggle', () => {
      if (q) return;   // while searching, opening and closing is not remembered
      d.open ? openKinds.add(d.dataset.k) : openKinds.delete(d.dataset.k);
      try { localStorage.setItem('plantOpenKinds', JSON.stringify([...openKinds])); } catch { }
    }));
    countKinds();
  }
  // "ticked / total" on each category header.
  function countKinds() {
    $('plList').querySelectorAll('details').forEach(d => {
      const boxes = d.querySelectorAll('input'), on = [...boxes].filter(b => b.checked).length, n = d.querySelector('.n');
      n.textContent = on ? `${on} / ${boxes.length}` : `${boxes.length}`;
      n.classList.toggle('sel', on > 0);
    });
  }
  let openKinds = new Set(['trees']);
  try { const o = JSON.parse(localStorage.getItem('plantOpenKinds') ?? 'null'); if (Array.isArray(o)) openKinds = new Set(o); } catch { }
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
    const cx = Math.floor(gx / cell), cz = Math.floor(gz / cell), n = Math.max(1, Math.ceil(spacing / cell));
    for (let dz = -n; dz <= n; dz++) for (let dx = -n; dx <= n; dx++) for (const [x, z] of hash.get(`${cx + dx},${cz + dz}`) ?? []) if (Math.hypot(x - gx, z - gz) < spacing) return false;
    return true;
  }
  // Saplings and crops: the game only lets them grow with nothing within their grow radius
  // (name -> [radius m, needs cultivated ground], from the game's prefabs).
  fetch('/api/grow').then(r => r.json()).then(g => { grow = g; syncLabels(); updatePreview(); }).catch(() => { });
  const growNeed = name => $('plGrow').checked ? (grow[name]?.[0] ?? 0) : 0;
  // Keeps the grow radius between the placements themselves too (rocks or other objects included).
  function roomToGrow(list) {
    if (!$('plGrow').checked || !list.some(o => growNeed(o.name) > 0)) return list;
    const h = new Map(), cell = 2, out = [];
    for (const o of list) {
      const need = growNeed(o.name), cx = Math.floor(o.gx / cell), cz = Math.floor(o.gz / cell);
      let ok = true;
      for (let dz = -2; dz <= 2 && ok; dz++) for (let dx = -2; dx <= 2 && ok; dx++) for (const [x, z, nd] of h.get(`${cx + dx},${cz + dz}`) ?? []) if (Math.hypot(x - o.gx, z - o.gz) < Math.max(need, nd)) { ok = false; break; }
      if (!ok) continue;
      const k = `${cx},${cz}`; (h.get(k) ?? h.set(k, []).get(k)).push([o.gx, o.gz, need]);
      out.push(o);
    }
    return out;
  }
  const addToHash = (hash, cell, gx, gz) => { const k = `${Math.floor(gx / cell)},${Math.floor(gz / cell)}`; (hash.get(k) ?? hash.set(k, []).get(k)).push([gx, gz]); };
  const underwaterOk = names => names.some(nm => /kelp|seaweed/i.test(nm));
  // One placement at grid point (gx, gz) with the given random draws, or null when it may not go there.
  // Rotation of the preview (degrees, Unity yaw: clockwise seen from above).
  // Zone and grid shapes can be turned as a whole; their objects turn with them.
  const rotation = () => v('plRot') + (turnable() ? shapeTurn : 0);
  // yaw: an extra facing (degrees); null leaves the usual one. A fixed yaw replaces the random facing.
  function placementAt(gx, gz, d, names, hash, cell, minDist = null, yaw = null) {
    if (gx < 1 || gz < 1 || gx > W - 2 || gz > H - 2) return null;
    if (!ed.mask(Math.round(gz) * W + Math.round(gx))) return null;
    // One at a time: you pick the spot, so only an object right on top (0.3 m) blocks it.
    const name = names[Math.floor(d.t * names.length) % names.length];
    if (!free(hash, cell, gx, gz, Math.max(minDist ?? ($('plSingle').checked ? 0.3 : v('plSpacing')), growNeed(name)))) return null;
    const y = heightAt(gx, gz);
    if (y < ed.WATER - 0.3 && !underwaterOk(names)) return null;  // not under water
    const smin = v('plSmin') / 100, smax = Math.max(smin, v('plSmax') / 100), tilt = v('plTilt');
    return { name, x: ed.originX + gx, y: y - 0.05, z: ed.originZ + gz,
      rx: d.rx * tilt, ry: (yaw != null ? yaw : $('plRandomYaw').checked ? d.ry * 360 : 0) + rotation(), rz: d.rz * tilt, scale: smin + d.s * (smax - smin), gx, gz };
  }
  const draw = () => ({ t: Math.random(), rx: Math.random() * 2 - 1, ry: Math.random(), rz: Math.random() * 2 - 1, s: Math.random() });

  // ---- Preview: the objects one click would place under the cursor, drawn as see-through ghosts.
  // The layout (offsets from the cursor) stays the same while the cursor moves; R shuffles it.
  let pattern = [], patternKey = '', hash = null, hashCell = 0, hashStale = true, preview = [];
  const ghostGroup = new THREE.Group(); ed.scene.add(ghostGroup);
  // Kinds without a model (or still loading) show as dots, so every placement is visible.
  const dots = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color: 0x9fe0ff, size: 7, sizeAttenuation: false, depthTest: false }));
  dots.renderOrder = 6; dots.frustumCulled = false; ghostGroup.add(dots);
  const MAXSHAPE = 2000;      // most objects one line, grid or zone places at once
  const ghosts = new Map();   // name -> { meshes, parts } (loading: null)
  const ghostMat = new Map(); // material -> see-through copy
  const m4 = new THREE.Matrix4();
  (ed.onObjectsChanged ??= []).push(() => { hashStale = true; });
  function settingsKey() { return [ed.radius, v('plDensity'), v('plSpacing'), $('plSingle').checked, [...chosen].join(',')].join('|'); }
  function makePattern() {
    const r = ed.radius, spacing = v('plSpacing');
    const n = Math.min(400, Math.round(v('plDensity') / 100 * Math.PI * r * r));
    pattern = [];
    if ($('plSingle').checked) { pattern.push({ dx: 0, dz: 0, d: draw() }); patternKey = settingsKey(); return; }
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
          // three.js tests the cut-out after applying the opacity: scale it, or leaves vanish.
          if (mm.alphaTest) mm.alphaTest *= mm.opacity;
          mm.emissive?.setRGB(0.15, 0.25, 0.35);
          ghostMat.set(part.material, mm);
        }
        const im = new THREE.InstancedMesh(part.geometry, ghostMat.get(part.material), MAXSHAPE);
        im.count = 0; im.frustumCulled = false; im.renderOrder = 5;
        ghostGroup.add(im);
        return im;
      });
      ghosts.set(name, { parts, meshes });
      updatePreview();
    }).catch(() => {});
    return null;
  }
  // ---- Line and grid modes: placements follow a drawn line or fill the cells of a box.
  let mode = 'brush', linePts = [], drawingLine = false, gridA = null, gridB = null, fill = 'scatter';
  // Zone mode uses linePts as the outline of a closed shape.
  const pointed = () => mode === 'line' || mode === 'zone';
  // Zone outlines and grid boxes are kept unturned ("local"); xf turns a local point by shapeTurn
  // around a fixed pivot (clockwise from above, like the objects' facing), inv undoes it.
  var shapeTurn = 0, pivot = null;
  function turnable() { return mode === 'zone' || mode === 'grid'; }
  function xf(p) {
    if (!shapeTurn || !pivot) return p;
    const t = shapeTurn * Math.PI / 180, c = Math.cos(t), sn = Math.sin(t), dx = p.gx - pivot.gx, dz = p.gz - pivot.gz;
    return { gx: pivot.gx + dx * c + dz * sn, gz: pivot.gz - dx * sn + dz * c };
  }
  function inv(p) {
    if (!shapeTurn || !pivot) return { gx: p.gx, gz: p.gz };
    const t = -shapeTurn * Math.PI / 180, c = Math.cos(t), sn = Math.sin(t), dx = p.gx - pivot.gx, dz = p.gz - pivot.gz;
    return { gx: pivot.gx + dx * c + dz * sn, gz: pivot.gz - dx * sn + dz * c };
  }
  const local = p => mode === 'zone' ? inv(p) : { gx: p.gx, gz: p.gz };
  function turnShape(deg) {
    const pts = mode === 'zone' ? linePts : gridA && gridB ? [gridA, gridB] : [];
    if (!pts.length) { ed.msg(mode === 'zone' ? 'Draw a zone first, then turn it.' : 'Drag a box first, then turn it.', true); return; }
    if (!pivot) {
      let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
      for (const p of pts) { x0 = Math.min(x0, p.gx); x1 = Math.max(x1, p.gx); z0 = Math.min(z0, p.gz); z1 = Math.max(z1, p.gz); }
      pivot = { gx: (x0 + x1) / 2, gz: (z0 + z1) / 2 };
    }
    shapeTurn = ((shapeTurn + deg + 180) % 360 + 360) % 360 - 180;
    drawShape(); updatePreview();
  }
  function inside(poly, x, z) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if ((a.gz > z) !== (b.gz > z) && x < (b.gx - a.gx) * (z - a.gz) / (b.gz - a.gz) + a.gx) c = !c;
    }
    return c;
  }
  // Scatter spots inside the zone: kept while the shape and settings stay the same (R makes new ones).
  let scatter = null, scatterSeed = 0;
  function zoneSpots() {
    const key = [scatterSeed, v('plDensity'), v('plSpacing'), linePts.map(p => `${p.gx.toFixed(2)},${p.gz.toFixed(2)}`).join(' ')].join('|');
    if (scatter?.key === key) return scatter.pts;
    let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity, area = 0;
    for (let i = 0, j = linePts.length - 1; i < linePts.length; j = i++) {
      const a = linePts[i], b = linePts[j];
      x0 = Math.min(x0, a.gx); x1 = Math.max(x1, a.gx); z0 = Math.min(z0, a.gz); z1 = Math.max(z1, a.gz);
      area += (b.gx - a.gx) * (b.gz + a.gz) / 2;
    }
    const spacing = v('plSpacing'), n = Math.min(MAXSHAPE, Math.round(v('plDensity') / 100 * Math.abs(area)));
    // Dart throwing with a grid of the spacing, so large zones stay fast.
    const pts = [], h = new Map(), cs = Math.max(0.5, spacing);
    for (let tries = 0; pts.length < n && tries < n * 30; tries++) {
      const gx = x0 + Math.random() * (x1 - x0), gz = z0 + Math.random() * (z1 - z0);
      if (!inside(linePts, gx, gz) || !free(h, cs, gx, gz, spacing)) continue;
      addToHash(h, cs, gx, gz);
      pts.push({ gx, gz });
    }
    scatter = { key, pts };
    return pts;
  }
  const draws = [];   // random draws per placement index, stable until R
  const drawAt = i => draws[i] ??= { ...draw(), w: Math.random() };
  const shape = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0x9fe0ff, depthTest: false }));
  shape.renderOrder = 15; shape.frustumCulled = false; ed.scene.add(shape);
  const v3 = (gx, gz) => new THREE.Vector3(gx - ed.cx, ed.sampleHeight(gx, gz) + 0.3, -(gz - ed.cz));
  function lineCurve() {
    const p = linePts;
    if (p.length < 2 || !$('plCurve').checked || p.length < 3) return p.slice();
    const out = [];
    for (let i = 0; i < p.length - 1; i++) {
      const p0 = p[Math.max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.min(p.length - 1, i + 2)];
      const steps = Math.max(1, Math.ceil(Math.hypot(p2.gx - p1.gx, p2.gz - p1.gz)));
      for (let k = 0; k < steps; k++) {
        const t = k / steps, t2 = t * t, t3 = t2 * t;
        const cr = (a, b, c, d) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        out.push({ gx: cr(p0.gx, p1.gx, p2.gx, p3.gx), gz: cr(p0.gz, p1.gz, p2.gz, p3.gz) });
      }
    }
    out.push(p[p.length - 1]);
    return out;
  }
  function shapePlacements(names, hash, cell) {
    const out = [];
    if (mode === 'zone') {
      if (linePts.length < 3) return out;
      if (fill === 'scatter') {
        zoneSpots().forEach((q, i) => { const p = xf(q), o = placementAt(p.gx, p.gz, drawAt(i), names, hash, cell, v('plSpacing')); if (o) out.push(o); });
        return out;
      }
      const size = v('plCell');
      let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
      for (const p of linePts) { x0 = Math.min(x0, p.gx); x1 = Math.max(x1, p.gx); z0 = Math.min(z0, p.gz); z1 = Math.max(z1, p.gz); }
      let i = 0;
      for (let gz = z0 + size / 2; gz < z1; gz += size) for (let gx = x0 + size / 2; gx < x1; gx += size) {
        if (!inside(linePts, gx, gz)) continue;
        if (i >= MAXSHAPE) return out;
        const p = xf({ gx, gz }), o = placementAt(p.gx, p.gz, drawAt(i++), names, hash, cell, 0.3);
        if (o) out.push(o);
      }
      return out;
    }
    if (mode === 'line' && $('plSnap').checked) return snappedLine(names);
    if (mode === 'line') {
      const c = linePath();
      if (c.length < 2) return out;
      const every = v('plEvery'), wiggle = v('plWiggle');
      let walked = 0, next = 0, i = 0;
      for (let s = 1; s < c.length; s++) {
        const a = c[s - 1], b = c[s], len = Math.hypot(b.gx - a.gx, b.gz - a.gz);
        while (next <= walked + len + 1e-6) {
          const t = len > 0 ? (next - walked) / len : 0, dx = (b.gx - a.gx) / (len || 1), dz = (b.gz - a.gz) / (len || 1);
          const d = drawAt(i), side = (d.w - 0.5) * 2 * wiggle;
          const gx = a.gx + (b.gx - a.gx) * t - dz * side, gz = a.gz + (b.gz - a.gz) * t + dx * side;
          // Unity yaw: clockwise from north (+z), so the heading of (dx, dz) is atan2(dx, dz).
          const yaw = $('plAlong').checked ? Math.atan2(dx, dz) * 180 / Math.PI : null;
          const o = placementAt(gx, gz, d, names, hash, cell, 0.3, yaw);
          if (o) out.push(o);
          i++; next += every;
          if (i >= MAXSHAPE) return out;
        }
        walked += len;
      }
    } else if (mode === 'grid' && gridA && gridB) {
      const size = v('plCell');
      const x0 = Math.min(gridA.gx, gridB.gx), x1 = Math.max(gridA.gx, gridB.gx), z0 = Math.min(gridA.gz, gridB.gz), z1 = Math.max(gridA.gz, gridB.gz);
      const nx = Math.floor((x1 - x0) / size), nz = Math.floor((z1 - z0) / size);
      let i = 0;
      for (let iz = 0; iz < nz; iz++) for (let ix = 0; ix < nx; ix++) {
        if (i >= MAXSHAPE) return out;
        const p = xf({ gx: x0 + (ix + 0.5) * size, gz: z0 + (iz + 0.5) * size }), o = placementAt(p.gx, p.gz, drawAt(i++), names, hash, cell, 0.3);
        if (o) out.push(o);
      }
    }
    return out;
  }
  // ---- End to end: pieces placed so that each one's end meets the next one's start, like the game's
  // hammer snaps them. A piece's two ends are the middles of the faces along its longer side, from its
  // snap points (walls, fences, floors...) or, for kinds without any, from its model's box.
  let snapPoints = null;
  fetch('/api/snappoints').then(r => r.json()).then(d => { snapPoints = d; updatePreview(); }).catch(() => { snapPoints = {}; });
  const endsCache = new Map();
  function endsFrom(xs, ys, zs) {
    const x0 = Math.min(...xs), x1 = Math.max(...xs), z0 = Math.min(...zs), z1 = Math.max(...zs), y = Math.min(...ys);
    const alongX = x1 - x0 >= z1 - z0 - 1e-3, cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
    const a = alongX ? [x0, y, cz] : [cx, y, z0], b = alongX ? [x1, y, cz] : [cx, y, z1];
    const len = Math.hypot(b[0] - a[0], b[2] - a[2]);
    return len > 0.05 ? { a, b, len, heading: Math.atan2(b[0] - a[0], b[2] - a[2]) } : null;
  }
  function endsOf(name) {
    if (endsCache.has(name)) return endsCache.get(name);
    const sp = snapPoints?.[name];
    if (sp?.length >= 2) { const e = endsFrom(sp.map(p => p[0]), sp.map(p => p[1]), sp.map(p => p[2])); if (e) { endsCache.set(name, e); return e; } }
    endsCache.set(name, null);
    // No snap points: the model's box (three.js space has z mirrored).
    ed.pieceModel(name).then(parts => {
      if (!parts?.length) return;
      const box = new THREE.Box3(), b = new THREE.Box3();
      for (const part of parts) { if (!part.geometry.boundingBox) part.geometry.computeBoundingBox(); box.union(b.copy(part.geometry.boundingBox).applyMatrix4(part.matrix)); }
      const sc = parts.rootScale ?? [1, 1, 1];
      endsCache.set(name, endsFrom([box.min.x * sc[0], box.max.x * sc[0]], [box.min.y * sc[1], box.max.y * sc[1]], [-box.max.z * sc[2], -box.min.z * sc[2]]));
      updatePreview();
    }).catch(() => { });
    return null;
  }
  // ---- Line shapes: points (clicked or drawn freely), a circle (centre, then drag the size) or a
  // rectangle (a corner, then drag the opposite one). Circles and rectangles are closed.
  let lineShape = 'points', figA = null, figB = null;
  // End to end: the size snaps so that whole pieces close the shape.
  function snapFigure(a, b) {
    const e = $('plSnap').checked && chosen.size ? endsOf([...chosen][0]) : null;
    if (!e) return b;
    if (lineShape === 'rect') {
      const fit = d => Math.sign(d || 1) * Math.max(1, Math.round(Math.abs(d) / e.len)) * e.len;
      return { gx: a.gx + fit(b.gx - a.gx), gz: a.gz + fit(b.gz - a.gz) };
    }
    const r = Math.hypot(b.gx - a.gx, b.gz - a.gz), n = Math.max(3, Math.round(2 * Math.PI * r / e.len)), R = e.len / (2 * Math.sin(Math.PI / n));
    const t = Math.atan2(b.gz - a.gz, b.gx - a.gx);
    return { gx: a.gx + Math.cos(t) * R, gz: a.gz + Math.sin(t) * R };
  }
  // The line as runs of points: one run, or the four sides of a rectangle each on their own when
  // placing end to end (so its corners stay exact).
  function lineRuns() {
    if (lineShape === 'points') {
      const c = lineCurve();
      if ($('plLoop').checked && c.length >= 3) c.push(c[0]);
      return c.length >= 2 ? [c] : [];
    }
    if (!figA || !figB) return [];
    const b = snapFigure(figA, figB);
    if (lineShape === 'rect') {
      const c = [{ gx: figA.gx, gz: figA.gz }, { gx: b.gx, gz: figA.gz }, { gx: b.gx, gz: b.gz }, { gx: figA.gx, gz: b.gz }];
      if (Math.abs(b.gx - figA.gx) < 0.5 || Math.abs(b.gz - figA.gz) < 0.5) return [];
      return $('plSnap').checked ? c.map((p, i) => [p, c[(i + 1) % 4]]) : [[...c, c[0]]];
    }
    const R = Math.hypot(b.gx - figA.gx, b.gz - figA.gz);
    if (R < 0.5) return [];
    // End to end: one point per piece, so the pieces meet at the corners of a regular polygon.
    const e = $('plSnap').checked && chosen.size ? endsOf([...chosen][0]) : null;
    const n = e ? Math.max(3, Math.round(2 * Math.PI * R / e.len)) : Math.max(16, Math.ceil(2 * Math.PI * R / 1.5));
    const t0 = Math.atan2(b.gz - figA.gz, b.gx - figA.gx), c = [];
    for (let i = 0; i <= n; i++) { const t = t0 + i / n * Math.PI * 2; c.push({ gx: figA.gx + Math.cos(t) * R, gz: figA.gz + Math.sin(t) * R }); }
    return [c];
  }
  const linePath = () => lineRuns().flat();
  let snapGap = null;
  function snappedLine(names) {
    const out = [];
    snapGap = 0;
    // Kinds whose length is not known (no snap points, no model yet) are left out.
    names = names.filter(n => endsOf(n));
    if (!names.length) return out;
    let k = 0;
    for (const pts of lineRuns()) k = snappedRun(pts, names, k, out);
    return out;
  }
  // The point along the run at a straight distance dist from s, searching from segment seg at
  // fraction t0 (the run is walked forwards only), or null past its end.
  function along(pts, s, seg, t0, dist) {
    for (; seg < pts.length - 1; seg++, t0 = 0) {
      const a = pts[seg], b = pts[seg + 1], dx = b.gx - a.gx, dz = b.gz - a.gz, fx = a.gx - s.gx, fz = a.gz - s.gz;
      const A = dx * dx + dz * dz, B = 2 * (fx * dx + fz * dz), C = fx * fx + fz * fz - dist * dist, disc = B * B - 4 * A * C;
      if (A < 1e-9 || disc < 0) continue;
      const t = (-B + Math.sqrt(disc)) / (2 * A);
      if (t >= t0 - 1e-6 && t <= 1 + 1e-6) return { q: { gx: a.gx + dx * t, gz: a.gz + dz * t }, seg, t };
    }
    return null;
  }
  // Places pieces end to end along one run of points, from its start. k: pieces placed so far.
  // Pieces stay level, each on the ground where it stands (the lowest of its ends and middle, so it
  // never floats): on a slope each one is a little higher or lower than the last, touching it.
  function snappedRun(pts, names, k, out) {
    if (pts.length < 2) return k;
    let s = { gx: pts[0].gx, gz: pts[0].gz }, seg = 0, t0 = 0, sSeg = 0;
    for (; k < MAXSHAPE; k++) {
      const name = names[k % names.length], e = endsOf(name);
      sSeg = seg;
      if (!e) break;
      const hit = along(pts, s, seg, t0, e.len);
      if (!hit) break;
      const q = hit.q;
      // Turn so that the piece's start-to-end runs along s -> q (Unity yaw, clockwise seen from above).
      const yaw = (Math.atan2(q.gx - s.gx, q.gz - s.gz) - e.heading) * 180 / Math.PI;
      const r = yaw * Math.PI / 180, c = Math.cos(r), sn = Math.sin(r);
      const ox = s.gx - (e.a[0] * c + e.a[2] * sn), oz = s.gz - (-e.a[0] * sn + e.a[2] * c);
      const bottom = Math.min(heightAt(s.gx, s.gz), heightAt(q.gx, q.gz), heightAt((s.gx + q.gx) / 2, (s.gz + q.gz) / 2));
      if (ox >= 1 && oz >= 1 && ox <= W - 2 && oz <= H - 2 && ed.mask(Math.round(oz) * W + Math.round(ox)))
        out.push({ name, x: ed.originX + ox, y: bottom - e.a[1], z: ed.originZ + oz, rx: 0, ry: ((yaw % 360) + 360) % 360, rz: 0, scale: 0, gx: ox, gz: oz });
      s = q; seg = hit.seg; t0 = hit.t;
    }
    // What is left of the line once no whole piece fits any more.
    for (let i = sSeg; i < pts.length - 1; i++) { const a = i === sSeg ? s : pts[i], b = pts[i + 1]; snapGap += Math.hypot(b.gx - a.gx, b.gz - a.gz); }
    return k;
  }

  function drawShape() {
    const pts = [];
    if (mode === 'line' && (linePts.length || figA)) {
      const c = linePath();
      for (let s = 0; s < c.length; s++) {
        if (s === 0) { pts.push(v3(c[0].gx, c[0].gz)); continue; }
        const a = c[s - 1], b = c[s], n = Math.max(1, Math.ceil(Math.hypot(b.gx - a.gx, b.gz - a.gz)));
        for (let k = 1; k <= n; k++) pts.push(v3(a.gx + (b.gx - a.gx) * k / n, a.gz + (b.gz - a.gz) * k / n));
      }
    } else if (mode === 'zone' && linePts.length) {
      const ring = [...linePts, linePts[0]].map(xf);
      for (let s = 0; s < ring.length; s++) {
        if (s === 0) { pts.push(v3(ring[0].gx, ring[0].gz)); continue; }
        const a = ring[s - 1], b = ring[s], n = Math.max(1, Math.ceil(Math.hypot(b.gx - a.gx, b.gz - a.gz)));
        for (let k = 1; k <= n; k++) pts.push(v3(a.gx + (b.gx - a.gx) * k / n, a.gz + (b.gz - a.gz) * k / n));
      }
    } else if (mode === 'grid' && gridA && gridB) {
      const ring = [[gridA.gx, gridA.gz], [gridB.gx, gridA.gz], [gridB.gx, gridB.gz], [gridA.gx, gridB.gz], [gridA.gx, gridA.gz]].map(([gx, gz]) => { const p = xf({ gx, gz }); return [p.gx, p.gz]; });
      for (let s = 1; s < ring.length; s++) {
        const [ax, az] = ring[s - 1], [bx, bz] = ring[s], n = Math.max(1, Math.ceil(Math.hypot(bx - ax, bz - az)));
        for (let k = 0; k <= n; k++) pts.push(v3(ax + (bx - ax) * k / n, az + (bz - az) * k / n));
      }
    }
    shape.geometry.setFromPoints(pts);
    shape.visible = ed.tool === 'plant' && mode !== 'brush';
  }
  function setMode(m) {
    mode = m;
    $('plModes').querySelectorAll('[data-m]').forEach(b => b.classList.toggle('on', b.dataset.m === m));
    $('plLineBox').hidden = m !== 'line'; syncSnap(); $('plGridBox').hidden = m !== 'grid'; $('plZoneBox').hidden = m !== 'zone'; $('plPlaceRow').hidden = m === 'brush'; $('plBrushHint').hidden = m !== 'brush';
    syncFill();
  }
  // End to end: the piece's own length sets the spacing, facing and size are fixed.
  function syncSnap() {
    const on = mode === 'line' && $('plSnap').checked;
    for (const id of ['plEvery', 'plWiggle', 'plAlong']) $(id).closest('label').hidden = on;
    for (const id of ['plSmin', 'plTilt', 'plRot', 'plRandomYaw']) $(id).closest('label').hidden = on;
    $('plLoopRow').hidden = mode !== 'line' || lineShape !== 'points';
    $('plCurve').closest('label').hidden = lineShape !== 'points';
  }
  ['plSnap', 'plLoop'].forEach(id => $(id).addEventListener('input', () => { syncSnap(); drawShape(); updatePreview(); }));
  $('plLineShape').querySelectorAll('[data-ls]').forEach(b => b.onclick = () => {
    lineShape = b.dataset.ls; clearShape(); syncSnap();
    $('plLineShape').querySelectorAll('[data-ls]').forEach(x => x.classList.toggle('on', x === b));
    $('plLineHint').textContent = { points: 'Click points along the route, or hold and drag to draw freely. Backspace removes the last point.', circle: 'Press at the centre and drag out to the size; let go to see it, Enter places it. With End to end the size snaps so whole pieces close the ring.', rect: 'Press at one corner and drag to the opposite one; Enter places it. With End to end the sides snap to whole pieces.' }[lineShape];
  });
  function syncFill() {
    const scatterFill = mode === 'zone' && fill === 'scatter';
    $('plFill').querySelectorAll('[data-f]').forEach(b => b.classList.toggle('on', b.dataset.f === fill));
    $('plSingle').closest('label').hidden = mode !== 'brush';
    for (const id of ['plDensity', 'plSpacing']) $(id).closest('label').hidden = mode !== 'brush' && !scatterFill;
    $('plCellRow').hidden = !(mode === 'grid' || (mode === 'zone' && fill === 'grid'));
    drawShape(); updatePreview();
  }
  $('plFill').querySelectorAll('[data-f]').forEach(b => b.onclick = () => { fill = b.dataset.f; syncFill(); });
  $('plModes').querySelectorAll('[data-m]').forEach(b => b.onclick = () => setMode(b.dataset.m));
  const syncShapeLabels = () => { $('plEveryV').textContent = `${v('plEvery')} m`; $('plWiggleV').textContent = `${v('plWiggle')} m`; $('plCellV').textContent = `${v('plCell')} m`; };
  ['plEvery', 'plWiggle', 'plCell', 'plAlong', 'plCurve'].forEach(id => $(id).addEventListener('input', () => { syncShapeLabels(); drawShape(); updatePreview(); }));
  syncShapeLabels();
  function clearShape() { linePts = []; figA = figB = null; gridA = gridB = null; shapeTurn = 0; pivot = null; drawingLine = false; drawShape(); updatePreview(); }
  async function placeShape() {
    if (!preview.length) { ed.msg(mode === 'line' ? 'Draw a line first (click points on the ground).' : mode === 'zone' ? 'Draw a zone first (click points around it, or drag).' : 'Drag a box on the ground first.', true); return; }
    const list = preview.slice();
    const added = await ed.objects.add(list.map(o => ({ ...o, fresh: true })));
    hashStale = true;
    ed.pushHistory({ added, label: `Planted ${added.length} ${mode === 'line' ? 'along a line' : mode === 'zone' ? 'in a zone' : 'in a grid'}` });
    ed.msg(`Placed ${added.length} object(s). Ctrl+Z removes them.`);
    updatePreview();
  }
  $('plPlace').onclick = placeShape; $('plClearShape').onclick = clearShape;

  function updatePreview() {
    const shaped = mode !== 'brush';
    const show = ed.tool === 'plant' && !stroke && (shaped || !held.shift) && chosen.size > 0 && (shaped || at);
    ghostGroup.visible = show;
    if (!show) { preview = []; info(); return; }
    if (settingsKey() !== patternKey) makePattern();
    const cell = Math.max(1, v('plSpacing'));
    if (hashStale || cell !== hashCell) { hash = buildHash(cell); hashCell = cell; hashStale = false; }
    const names = [...chosen], placed = shaped ? shapePlacements(names, hash, cell) : [];
    // Turn the layout with the rotation (clockwise from above, like the objects' facing).
    const t = rotation() * Math.PI / 180, c = Math.cos(t), sn = Math.sin(t);
    if (!shaped) for (const p of pattern) {
      const o = placementAt(at.gx + p.dx * c + p.dz * sn, at.gz - p.dx * sn + p.dz * c, p.d, names, hash, cell);
      if (o) placed.push(o);
    }
    preview = roomToGrow(placed);
    const byName = new Map();
    for (const o of preview) (byName.get(o.name) ?? byName.set(o.name, []).get(o.name)).push(o);
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
    if (held.shift && mode === 'brush') { $('plPreview').textContent = 'Shift: drag to remove the chosen kinds under the brush.'; return; }
    if (mode !== 'brush') { $('plPreview').innerHTML = `<b>${preview.length}</b> object(s) ${mode === 'line' ? 'along the line' : mode === 'zone' ? 'in the zone' : 'in the grid'}${mode === 'line' && figA && figB ? (() => { const b = snapFigure(figA, figB); return lineShape === 'rect' ? ` (${Math.abs(b.gx - figA.gx).toFixed(1)} × ${Math.abs(b.gz - figA.gz).toFixed(1)} m)` : ` (radius ${Math.hypot(b.gx - figA.gx, b.gz - figA.gz).toFixed(1)} m)`; })() : ''}${mode === 'line' && $('plSnap').checked && snapGap > 0.05 && preview.length ? `, end to end (${snapGap.toFixed(1)} m of the line left at the end: move a point to close it)` : ''}. <kbd>Enter</kbd> places them${pointed() ? ' (or double-click)' : ''}${turnable() ? ` · <kbd>,</kbd> <kbd>.</kbd> or <kbd>Alt</kbd>+wheel turn the ${mode === 'zone' ? 'zone' : 'grid'}${shapeTurn ? ` (now ${shapeTurn}°)` : ''}` : ''} · <kbd>R</kbd> new random choices.`; return; }
    $('plPreview').innerHTML = at ? `<b>${preview.length}</b> object(s) shown under the cursor. Click places exactly these; drag paints more. <kbd>R</kbd> new layout · <kbd>Alt</kbd>+wheel or <kbd>,</kbd> <kbd>.</kbd> rotate.` : 'Move over the ground to see what a click would place.';
  }
  const held = { shift: false };
  addEventListener('keydown', e => { if (e.key === 'Shift' && !held.shift) { held.shift = true; updatePreview(); } });
  addEventListener('keyup', e => { if (e.key === 'Shift') { held.shift = false; updatePreview(); } });
  ['plDensity', 'plSpacing', 'plSmin', 'plSmax', 'plTilt'].forEach(id => $(id).addEventListener('input', () => { if (id === 'plDensity' || id === 'plSpacing') patternKey = ''; else for (const p of pattern) p.d = { ...p.d }; updatePreview(); }));
  $('radius').addEventListener('input', () => updatePreview());
  $('plList').addEventListener('change', () => updatePreview());
  ed.onToolChange.push(t => { if (t === 'plant') hashStale = true; drawShape(); updatePreview(); });

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
  const brush = {
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
      if (added.length || deleted.length) ed.pushHistory({ ...(added.length ? { added } : {}), ...(deleted.length ? { deleted } : {}), label: s.erase ? `Plant: removed ${deleted.length}` : `Planted ${added.length} (${[...new Set(added.map(id => ed.objects.records.get(id)?.name).filter(Boolean))].slice(0, 3).join(', ')})` });
      ed.msg(s.erase ? `Removed ${deleted.length} object(s).` : `Planted ${added.length} object(s). Ctrl+Z removes them; Save writes them to the world.`);
      makePattern();
      updatePreview();
    },
    key(e) {
      if (e.key.toLowerCase() === 'r' && !e.ctrlKey) { makePattern(); updatePreview(); return true; }
      if (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>') { turn((e.key === ',' || e.key === '<' ? -1 : 1) * ed.turnStep(e)); return true; }
      return false;
    }
  };
  // Line: click to add points, or hold and drag to draw freehand. Grid: drag a box.
  let press = null;
  const shaped = {
    down(e, hit) {
      if (!hit) return;
      ed.el.setPointerCapture(e.pointerId);
      press = { x: e.clientX, y: e.clientY, hit, drag: false };
      if (mode === 'line' && lineShape !== 'points') { figA = { gx: hit.gx, gz: hit.gz }; figB = null; drawShape(); updatePreview(); return; }
      if (mode === 'grid') { gridA = { gx: hit.gx, gz: hit.gz }; gridB = null; shapeTurn = 0; pivot = null; }
    },
    move(e, hit) {
      at = hit; ed.showStatusFor?.(hit);
      if (!hit) return;
      if (press && mode === 'line' && lineShape !== 'points') { figB = { gx: hit.gx, gz: hit.gz }; drawShape(); updatePreview(); return; }
      if (press && !press.drag && Math.hypot(e.clientX - press.x, e.clientY - press.y) > 6) {
        press.drag = true;
        if (pointed()) { const q = local(press.hit); if (!linePts.length || Math.hypot(linePts.at(-1).gx - q.gx, linePts.at(-1).gz - q.gz) > 0.3) linePts.push(q); drawingLine = true; }
      }
      if (press?.drag) {
        if (mode === 'grid') gridB = { gx: hit.gx, gz: hit.gz };
        // Freehand: a point every 2 m, smoothed by the curve.
        else { const q = local(hit); if (Math.hypot(linePts.at(-1).gx - q.gx, linePts.at(-1).gz - q.gz) >= 2) linePts.push(q); }
        drawShape(); updatePreview();
      }
    },
    up(e, hit) {
      const p = press; press = null;
      if (!p) return;
      if (mode === 'line' && lineShape !== 'points') {
        if (!p.drag && !figB) { figA = null; ed.msg(lineShape === 'circle' ? 'Press at the centre and drag out to the size.' : 'Press at a corner and drag to the opposite one.'); }
        drawShape(); updatePreview(); return;
      }
      if (pointed()) {
        const h = local(hit ?? at ?? p.hit);
        if (!p.drag) {
          // Double-click on the last point places the line.
          const last = linePts.at(-1);
          if (last && e.detail >= 2 && Math.hypot(last.gx - h.gx, last.gz - h.gz) < 1.5) { placeShape(); return; }
          linePts.push({ gx: h.gx, gz: h.gz });
        } else if (Math.hypot(linePts.at(-1).gx - h.gx, linePts.at(-1).gz - h.gz) > 0.3) linePts.push({ gx: h.gx, gz: h.gz });
        drawingLine = false;
      } else if (!p.drag) { gridA = gridB = null; }
      drawShape(); updatePreview();
    },
    key(e) {
      const k = e.key.toLowerCase();
      if (k === 'r' && !e.ctrlKey) { draws.length = 0; scatterSeed++; updatePreview(); return true; }
      if (e.key === 'Enter') { placeShape(); return true; }
      if (e.key === 'Escape' && (linePts.length || gridA || figA)) { clearShape(); return true; }
      if (e.key === 'Backspace' && pointed() && linePts.length) { linePts.pop(); drawShape(); updatePreview(); return true; }
      if (turnable() && (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>')) { turnShape((e.key === ',' || e.key === '<' ? -1 : 1) * ed.turnStep(e)); return true; }
      return brush.key(e);
    }
  };
  ed.handlers.plant = {
    down: (e, hit) => (mode === 'brush' ? brush : shaped).down(e, hit),
    move: (e, hit) => (mode === 'brush' ? brush : shaped).move(e, hit),
    up: (e, hit) => (mode === 'brush' ? brush : shaped).up(e, hit),
    key: e => (mode === 'brush' ? brush : shaped).key(e)
  };
  function turn(deg) {
    let r = v('plRot') + deg;
    r = ((r + 180) % 360 + 360) % 360 - 180;
    $('plRot').value = r; syncLabels(); updatePreview();
  }
  $('plNewLayout').onclick = () => { makePattern(); draws.length = 0; scatterSeed++; updatePreview(); };
  ['plRot', 'plRandomYaw', 'plSingle', 'plGrow'].forEach(id => $(id).addEventListener('input', () => updatePreview()));
  try { $('plGrow').checked = localStorage.getItem('plantGrowRoom') !== '0'; } catch { }
  $('plGrow').addEventListener('change', () => { try { localStorage.setItem('plantGrowRoom', $('plGrow').checked ? '1' : '0'); } catch { } });
  // Alt + mouse wheel turns the preview instead of zooming.
  ed.el.addEventListener('wheel', e => {
    if (ed.tool !== 'plant' || !e.altKey) return;
    e.preventDefault(); e.stopImmediatePropagation();
    (turnable() ? turnShape : turn)(Math.sign(e.deltaY) * ed.turnStep(e));
  }, { capture: true, passive: false });
  // For automated tests: what the preview would place.
  window.__plantPreview = () => preview.map(o => ({ ...o }));
  ed.frame ??= [];
  ed.frame.push(step);
}
