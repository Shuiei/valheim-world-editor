// Area tool (WorldEdit-style selections): select a box or polygon of ground, then change the ground
// inside it, remove / select / replace the objects in it, copy and paste it, or hand its zones back
// to the world generator. Also the Paste tool and "Replace with" for the Select tool.
import { KINDS, KIND_LABEL, objectKind } from './objects.js';
import { isWindow, pickFolder } from '../app.js';

const PAINTS = { dirt: [1, 0, 0, 1], cultivated: [0, 1, 0, 1], paved: [0, 0, 1, 1], natural: [0, 0, 0, 1] };
const smooth01 = t => t * t * (3 - 2 * t);

// The clipboard as JSON (browser storage, blueprint files): heights in cm (-32768 = no ground), edge
// weights and paint in 1/255, objects and outline as they are.
export function encodeClip(c) {
  const q = Array.from(c.rel, v => Number.isNaN(v) ? -32768 : Math.max(-32767, Math.min(32767, Math.round(v * 100))));
  return { w: c.w, h: c.h, rel: q, wt: Array.from(c.wt, v => Math.round(v * 255)), pnt: Array.from(c.pnt, v => Math.round(v * 255)), objects: c.objects, poly: c.poly, ...(c.name ? { name: c.name } : {}) };
}
export function decodeClip(o) {
  return { w: o.w, h: o.h, rel: Float32Array.from(o.rel, v => v === -32768 ? NaN : v / 100), wt: Float32Array.from(o.wt, v => v / 255), pnt: Float32Array.from(o.pnt, v => v < 0 ? -1 : v / 255), objects: o.objects, poly: o.poly, ...(o.name ? { name: o.name } : {}) };
}

export function createArea(ed) {
  const { THREE, $, W, H, N } = ed;
  const toGrid = (x, z) => ({ gx: x - ed.originX, gz: z - ed.originZ });

  // ---- Panel.
  const panel = document.createElement('div');
  panel.id = 'areaPanel';
  panel.innerHTML = `
    <div class="seg"><button data-shape="box" class="on">Box</button><button data-shape="poly">Polygon</button></div>
    <div class="hint" id="aInfo"></div>
    <label class="field">Soft edge <input id="aSoft" type="range" min="0" max="20" step="0.5" value="3"><span id="aSoftV"></span></label>
    <div class="sub"><h3>Ground</h3>
      <div class="actGrid">
        <button data-act="flatten" title="Level everything to the height below">Flatten</button>
        <button data-act="raise" title="Lift by the amount below">Raise</button>
        <button data-act="lower" title="Dig down by the amount below">Lower</button>
        <button data-act="smooth" title="Even out bumps">Smooth</button>
        <button data-act="natural" title="Natural-looking bumps (settings in the Naturalize tool)">Naturalize</button>
        <button data-act="restore" title="Back to the generated ground, paint removed">Restore</button>
        <button data-act="erode" title="Weather the ground inside: slopes settle and rain cuts gullies (rest angle from the Erode tool)">Erode</button>
      </div>
      <label class="field">Height <input id="aHeight" type="number" step="0.1" value="35"><span><button id="aAvg" class="mini" title="Average height inside the selection">avg</button></span></label>
      <label class="field">Amount <input id="aAmount" type="number" step="0.1" value="2"><span>m</span></label>
      <label class="field">Paint <select id="aPaint"><option value="dirt">Dirt</option><option value="cultivated">Cultivated</option><option value="paved">Paved</option><option value="natural">Clear paint</option></select><span><button id="aPaintBtn" class="mini">apply</button></span></label>
    </div>
    <div class="sub"><h3>Objects inside</h3>
      <div class="chips" id="aKinds"></div>
      <div class="row"><button id="aRemove">Remove</button><button id="aSelectObj">Select</button></div>
      <label class="field">Replace <select id="aFrom"></select></label>
      <label class="field">with <select id="aTo" class="typeList"></select></label>
      <div class="row"><button id="aReplace">Replace</button></div>
    </div>
    <div class="sub"><h3>Copy &amp; paste</h3>
      <div class="row"><button id="aCopy">Copy <kbd>Ctrl+C</kbd></button><button id="aPasteBtn">Paste <kbd>Ctrl+V</kbd></button></div>
      <div class="row"><button id="aSaveBp">Save blueprint…</button><button id="aLibrary">Blueprints…</button></div>
      <div class="hint" id="aClip"></div>
    </div>
    <div class="sub"><h3>Restore from a backup</h3>
      <label class="field">Backup <select id="aBackup"><option value="">Choose a backup…</option></select></label>
      <label class="check"><input type="checkbox" id="aBkGround" checked> Ground (height and paint)</label>
      <label class="check"><input type="checkbox" id="aBkObjects" checked> Objects of the kinds ticked above</label>
      <div class="row"><button id="aBkRestore">Restore the selection</button></div>
      <div class="hint" id="aBkInfo">Puts the selection back as it was in a backup: the editor's backups and the game's own are listed. Tick Buildings above to bring buildings back too.</div>
    </div>
    <div class="sub"><h3>Reset zones</h3>
      <label class="check"><input type="checkbox" id="aKeepB" checked> Keep my buildings</label>
      <label class="check"><input type="checkbox" id="aResetGround" checked> Reset ground edits too</label>
      <div class="row"><button id="aReset">Reset zones…</button><button id="aUnreset">Cancel reset</button></div>
      <div class="hint">On save, the zones under the selection lose their trees, rocks, ruins and dungeon entrances, and the game generates them again the next time someone goes there.</div>
    </div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['area'] });

  const pastePanel = document.createElement('div');
  pastePanel.id = 'pastePanel';
  pastePanel.innerHTML = `
    <div class="hint" id="psInfo" style="margin-top:0"></div>
    <label class="check"><input type="checkbox" id="psGround" checked> Ground shape and paint</label>
    <label class="check"><input type="checkbox" id="psObjects" checked> Objects</label>
    <label class="field">Height <input id="psOffset" type="number" step="0.5" value="0"><span>m</span></label>
    <div class="sub"><h3>Repeat (stack)</h3>
      <label class="field">Copies <input id="psCount" type="number" min="1" max="50" step="1" value="1"><span></span></label>
      <label class="field">Along <select id="psDir"><option value="x">its width</option><option value="z">its depth</option><option value="y">upwards</option></select></label>
      <label class="field">Gap <input id="psGap" type="number" step="0.25" value="0"><span>m</span></label>
    </div>
    <div class="row"><button id="psRot" title="Quarter turn; , . or Alt+wheel turn by 1° (Shift: 15°)">Turn 90° <kbd>R</kbd></button><button id="psFlip">Mirror <kbd>F</kbd></button><button id="psDone">Done <kbd>Esc</kbd></button></div>
    <div class="hint">Click to place; the copied ground keeps its shape relative to the point you click. Height moves the paste up or down.</div>`;
  $('locWarn').before(pastePanel);
  ed.panels.push({ el: pastePanel, tools: ['paste'] });

  const style = document.createElement('style');
  style.textContent = `
    .seg { display: flex; gap: 0; margin-bottom: 6px; } .seg button { flex: 1; border-radius: 0; } .seg button:first-child { border-radius: 7px 0 0 7px; } .seg button:last-child { border-radius: 0 7px 7px 0; }
    .seg button.on { background: var(--accent); color: var(--accent-ink); border-color: var(--accent); }
    .actGrid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 4px; margin: 4px 0 6px; }
    .actGrid button { padding: 5px 2px; font-size: 11.5px; }
    .chips .n { opacity: .65; margin-left: 3px; }
    button.mini { padding: 3px 6px; font-size: 11px; width: 100%; }`;
  document.head.appendChild(style);

  // ---- Selection shape.
  let shape = 'box', pts = [], dragging = false, closed = false;
  const outline = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0x5fd4ff, depthTest: false }));
  outline.renderOrder = 15; outline.frustumCulled = false; ed.scene.add(outline);
  panel.querySelectorAll('[data-shape]').forEach(b => b.onclick = () => {
    shape = b.dataset.shape; panel.querySelectorAll('[data-shape]').forEach(x => x.classList.toggle('on', x === b));
    clear();
  });
  const soft = () => +$('aSoft').value;
  $('aSoft').oninput = () => { $('aSoftV').textContent = `${soft()} m`; };
  $('aSoftV').textContent = `${soft()} m`;

  function polygon() {
    if (shape === 'box') {
      if (pts.length < 2) return null;
      const [a, b] = pts;
      return [{ gx: a.gx, gz: a.gz }, { gx: b.gx, gz: a.gz }, { gx: b.gx, gz: b.gz }, { gx: a.gx, gz: b.gz }];
    }
    return pts.length >= 3 ? pts : null;
  }
  function drawOutline(poly, open) {
    const p = [];
    const ring = open ? poly : [...poly, poly[0]];
    for (let i = 1; i < ring.length; i++) {
      const a = ring[i - 1], b = ring[i], n = Math.max(1, Math.ceil(Math.hypot(b.gx - a.gx, b.gz - a.gz)));
      for (let k = 0; k <= n; k++) {
        const gx = a.gx + (b.gx - a.gx) * k / n, gz = a.gz + (b.gz - a.gz) * k / n;
        p.push(new THREE.Vector3(gx - ed.cx, ed.sampleHeight(gx, gz) + 0.3, -(gz - ed.cz)));
      }
    }
    outline.geometry.setFromPoints(p);
  }
  function redraw() {
    const poly = polygon();
    if (poly) drawOutline(poly, false);
    else if (pts.length) drawOutline(pts, true);
    else outline.geometry.setFromPoints([]);
    outline.visible = ed.tool === 'area' || ed.tool === 'paste' ? true : !!poly;
    updateInfo();
  }
  function clear() { pts = []; closed = false; dragging = false; redraw(); }

  function inside(poly, x, z) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if ((a.gz > z) !== (b.gz > z) && x < (b.gx - a.gx) * (z - a.gz) / (b.gz - a.gz) + a.gx) c = !c;
    }
    return c;
  }
  function edgeDist(poly, x, z) {
    let best = Infinity;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[j], b = poly[i], dx = b.gx - a.gx, dz = b.gz - a.gz, l2 = dx * dx + dz * dz || 1e-9;
      const t = Math.max(0, Math.min(1, ((x - a.gx) * dx + (z - a.gz) * dz) / l2));
      best = Math.min(best, Math.hypot(x - (a.gx + dx * t), z - (a.gz + dz * t)));
    }
    return best;
  }
  // Weights of the grid points inside the selection (soft edge inwards), times the mask.
  function areaWeights(withMask = true) {
    const poly = polygon();
    if (!poly) return null;
    let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
    for (const p of poly) { x0 = Math.min(x0, p.gx); x1 = Math.max(x1, p.gx); z0 = Math.min(z0, p.gz); z1 = Math.max(z1, p.gz); }
    x0 = Math.max(0, Math.floor(x0)); x1 = Math.min(W - 1, Math.ceil(x1)); z0 = Math.max(0, Math.floor(z0)); z1 = Math.min(H - 1, Math.ceil(z1));
    const s = soft(), cells = [];
    for (let gz = z0; gz <= z1; gz++) for (let gx = x0; gx <= x1; gx++) {
      if (!inside(poly, gx, gz)) continue;
      const g = gz * W + gx;
      let w = s > 0 ? smooth01(Math.min(1, edgeDist(poly, gx, gz) / s)) : 1;
      if (withMask) w *= ed.mask(g);
      if (w > 0) cells.push([g, w]);
    }
    return { poly, x0, x1, z0, z1, cells };
  }
  function objectsInside(poly) {
    const out = [];
    for (const r of ed.objects.alive()) {
      const { gx, gz } = toGrid(r.x, r.z);
      if (inside(poly, gx, gz)) out.push(r);
    }
    return out;
  }

  // ---- Info, object kinds and replace lists.
  const kindOn = new Set(['trees', 'rocks', 'bushes', 'pickables']);
  function updateInfo() {
    const poly = polygon();
    if (!poly) {
      $('aInfo').textContent = shape === 'box' ? 'Drag on the ground to select a box.' : 'Click points around the area; double-click or Enter closes it. Backspace removes a point, Esc clears.';
      $('aKinds').innerHTML = ''; $('aFrom').innerHTML = '';
      return;
    }
    let area = 0;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) area += (poly[j].gx + poly[i].gx) * (poly[j].gz - poly[i].gz);
    // Kinds hidden in View (spoilers) are not counted or listed.
    const shown = k => (k === 'buildings' ? ed.buildings : ed.objectGroups[k]).visible;
    const inObjs = objectsInside(poly).filter(r => r.added || shown(r.kind)), byKind = {}, byName = {};
    for (const r of inObjs) { byKind[r.kind] = (byKind[r.kind] ?? 0) + 1; byName[r.name] = (byName[r.name] ?? 0) + 1; }
    $('aInfo').textContent = `${Math.abs(area / 2).toFixed(0)} m² selected · ${inObjs.length} shown object(s) inside. Click outside to start a new selection.`;
    $('aKinds').innerHTML = KINDS.map(k => `<button data-k="${k}" class="${kindOn.has(k) ? 'on' : ''}">${KIND_LABEL[k]}<span class="n">${shown(k) ? byKind[k] ?? 0 : 'hidden'}</span></button>`).join('');
    $('aKinds').querySelectorAll('[data-k]').forEach(b => b.onclick = () => { kindOn.has(b.dataset.k) ? kindOn.delete(b.dataset.k) : kindOn.add(b.dataset.k); b.classList.toggle('on'); });
    $('aFrom').innerHTML = Object.entries(byName).sort((a, b) => b[1] - a[1]).map(([n, c]) => `<option value="${n}">${n} (${c})</option>`).join('');
  }
  function fillTypeLists() {
    const types = ed.objects.creatableTypes(), groups = {};
    for (const t of types) (groups[t.kind] ??= []).push(t);
    const html = KINDS.filter(k => groups[k]).map(k => `<optgroup label="${KIND_LABEL[k]}">${groups[k].map(t => `<option value="${t.name}">${t.name}</option>`).join('')}</optgroup>`).join('');
    document.querySelectorAll('select.typeList').forEach(s => { const v = s.value; s.innerHTML = html; if (v) s.value = v; });
  }
  (ed.onObjects ??= []).push(() => { fillTypeLists(); updateInfo(); });

  // ---- Ground actions.
  function groundAction(act) {
    const a = areaWeights();
    if (!a) { ed.msg('Select an area first.', true); return; }
    if (act === 'erode') {
      const state = ed.snapshotState(), touched = new Set();
      ed.erosion.areaErode(a, touched);
      commitTerrain(state, touched, { x0: a.x0 - 1, x1: a.x1 + 1, z0: a.z0 - 1, z1: a.z1 + 1 }, { label: 'Area: Erode' });
      ed.msg(`Eroded ${touched.size} point(s). Ctrl+Z undoes it.`);
      return;
    }
    const state = ed.snapshotState(), touched = new Set();
    const before = new Float32Array(N); for (let g = 0; g < N; g++) before[g] = ed.height(g);
    const T = +$('aHeight').value, A = +$('aAmount').value, col = PAINTS[$('aPaint').value];
    const blur = (g, r) => {
      const gx = g % W, gz = (g - gx) / W; let s = 0, n = 0;
      for (let dz = -r; dz <= r; dz++) for (let dx = -r; dx <= r; dx++) {
        const x = gx + dx, z = gz + dz; if (x < 0 || z < 0 || x >= W || z >= H) continue; s += before[z * W + x]; n++;
      }
      return s / n;
    };
    const rNat = Math.max(2, Math.min(8, Math.round(ed.nSize() / 3)));
    for (const [g, w] of a.cells) {
      const gx = g % W, gz = (g - gx) / W;
      if (ed.locked(gx, gz)) continue;
      const h = ed.height(g);
      touched.add(g);
      switch (act) {
        case 'flatten': ed.setHeight(g, h + (T - h) * w); break;
        case 'raise': ed.setHeight(g, h + A * w); break;
        case 'lower': ed.setHeight(g, h - A * w); break;
        case 'smooth': ed.setHeight(g, h + (blur(g, 3) - h) * w); break;
        case 'natural': ed.setHeight(g, h + (blur(g, rNat) + ed.nAmp() * ed.worldNoise(gx, gz) - h) * w); break;
        case 'restore':
          if (ed.mod[g]) { ed.level[g] *= 1 - w; ed.smooth[g] *= 1 - w; if (w > 0.98 || Math.abs(ed.level[g]) + Math.abs(ed.smooth[g]) < 0.01) { ed.level[g] = 0; ed.smooth[g] = 0; ed.mod[g] = 0; } }
          if (ed.pmod[g] && w > 0.5) ed.pmod[g] = 0;
          break;
        case 'paint':
          if (!ed.pmod[g]) { ed.paint.set([0, 0, 0, 1], g * 4); ed.pmod[g] = 1; }
          for (let c = 0; c < 4; c++) ed.paint[g * 4 + c] += (col[c] - ed.paint[g * 4 + c]) * w;
          break;
      }
    }
    const actName = act === 'paint' ? `Paint ${$('aPaint').selectedOptions[0].text.toLowerCase()}` : act[0].toUpperCase() + act.slice(1);
    commitTerrain(state, touched, a, { label: `Area: ${actName}` });
    ed.msg(`${actName} applied to ${touched.size} point(s).`);
  }
  function commitTerrain(state, touched, a, extra = {}) {
    ed.refresh(a.x0 - 1, a.z0 - 1, a.x1 + 1, a.z1 + 1);
    const zones = ed.zonesOf(touched);
    if (zones.length) { ed.upload(zones); ed.pushHistory({ state, zones, ...extra }); }
    else if (Object.keys(extra).length) ed.pushHistory(extra);
    redraw();
  }
  panel.querySelectorAll('[data-act]').forEach(b => b.onclick = () => groundAction(b.dataset.act));
  $('aPaintBtn').onclick = () => groundAction('paint');
  $('aAvg').onclick = () => {
    const a = areaWeights(false); if (!a) return;
    let s = 0; for (const [g] of a.cells) s += ed.height(g);
    $('aHeight').value = (s / a.cells.length).toFixed(1);
  };

  // ---- Object actions.
  function pickedObjects() {
    const poly = polygon(); if (!poly) { ed.msg('Select an area first.', true); return null; }
    const shown = k => (k === 'buildings' ? ed.buildings : ed.objectGroups[k]).visible;
    return objectsInside(poly).filter(r => kindOn.has(r.kind) && (r.added || shown(r.kind)) && ed.mask(Math.round(toGrid(r.x, r.z).gz) * W + Math.round(toGrid(r.x, r.z).gx)));
  }
  $('aRemove').onclick = () => {
    const list = pickedObjects(); if (!list) return;
    if (!list.length) { ed.msg('No objects of the chosen kinds inside the selection.'); return; }
    const ids = list.map(r => r.id);
    ed.setDeleted(ids, true); ed.pushHistory({ deleted: ids, label: `Area: removed ${ids.length} object(s)` });
    ed.msg(`Removed ${ids.length} object(s). Ctrl+Z brings them back.`); updateInfo();
  };
  $('aSelectObj').onclick = () => {
    const list = pickedObjects(); if (!list) return;
    ed.selectIds(list.map(r => r.id)); ed.setTool('select');
  };
  async function replace(ids, toName) {
    const recs = ids.map(id => ed.objects.records.get(id)).filter(r => r && !r.deleted);
    if (!recs.length || !toName) return;
    const added = await ed.objects.add(recs.map(r => ({ name: toName, x: r.x, y: r.y, z: r.z, rx: r.rx, ry: r.ry, rz: r.rz, scale: 0 })));
    const deleted = recs.map(r => r.id);
    await ed.setDeleted(deleted, true);
    ed.pushHistory({ deleted, added, label: `Replaced ${deleted.length} with ${toName}` });
    ed.msg(`Replaced ${deleted.length} object(s) with ${toName}.`); updateInfo();
  }
  $('aReplace').onclick = () => {
    const poly = polygon(); if (!poly) { ed.msg('Select an area first.', true); return; }
    const from = $('aFrom').value;
    const shown = k => (k === 'buildings' ? ed.buildings : ed.objectGroups[k]).visible;
    replace(objectsInside(poly).filter(r => r.name === from && shown(r.kind)).map(r => r.id), $('aTo').value);
  };
  // Select tool: "Replace with".
  const selRow = document.createElement('div');
  selRow.innerHTML = `<label class="field">Replace <select id="selTo" class="typeList"></select><span><button id="selReplace" class="mini">go</button></span></label>`;
  $('selectPanel').querySelector('.row').after(selRow);
  $('selReplace').onclick = () => replace([...ed.selection], $('selTo').value).then(() => ed.clearSelection());

  // ---- Copy and paste.
  let clip = null, rot = 0, flip = false;   // rot: degrees (counter-clockwise seen from above)
  try { const c = localStorage.getItem('editorClipboard'); if (c) clip = decodeClip(JSON.parse(c)); } catch { }
  function updateClipInfo() {
    $('aClip').textContent = clip ? `Clipboard${clip.name ? ` (${clip.name})` : ''}: ${clip.w} × ${clip.h} m, ${clip.objects.length} object(s).` : 'Copies the ground (shape and paint) and the shown objects inside the selection.';
  }
  updateClipInfo();
  function copy() {
    const a = areaWeights(false);
    if (!a) { ed.msg('Select an area first.', true); return; }
    let ref = 0; for (const [g] of a.cells) ref += ed.height(g); ref /= a.cells.length;
    const w = a.x1 - a.x0 + 1, h = a.z1 - a.z0 + 1, rel = new Float32Array(w * h).fill(NaN), wt = new Float32Array(w * h), pnt = new Float32Array(w * h * 4).fill(-1);
    for (const [g, k] of a.cells) {
      const gx = g % W, gz = (g - gx) / W, i = (gz - a.z0) * w + (gx - a.x0);
      rel[i] = ed.height(g) - ref; wt[i] = k;
      if (ed.pmod[g]) for (let c = 0; c < 4; c++) pnt[i * 4 + c] = ed.paint[g * 4 + c];
    }
    const cxs = (a.x0 + a.x1) / 2, czs = (a.z0 + a.z1) / 2;
    const shown = new Set(KINDS.filter(k => (k === 'buildings' ? ed.buildings : ed.objectGroups[k]).visible));
    const objects = objectsInside(a.poly).filter(r => shown.has(r.kind) && ed.objects.state.templates.has(r.prefab)).map(r => {
      const { gx, gz } = toGrid(r.x, r.z);
      return { name: r.name, dx: gx - cxs, dz: gz - czs, dy: r.y - ref, rx: r.rx, ry: r.ry, rz: r.rz, scale: r.scale, sourceId: r.added ? r.sourceId ?? null : r.id };
    });
    clip = { w, h, rel, wt, pnt, objects, poly: a.poly.map(p => ({ gx: p.gx - cxs, gz: p.gz - czs })) };
    try { localStorage.setItem('editorClipboard', JSON.stringify(encodeClip(clip))); } catch { }
    updateClipInfo();
    ed.msg(`Copied ${w} × ${h} m and ${objects.length} object(s). Ctrl+V to paste, here or in another area.`);
  }
  $('aCopy').onclick = copy;
  $('aPasteBtn').onclick = () => startPaste();
  // The Select tool's copy puts its objects here too.
  ed.setClipboard = c => { clip = c; try { localStorage.setItem('editorClipboard', JSON.stringify(encodeClip(clip))); } catch { } updateClipInfo(); };
  ed.getClipboard = () => clip;
  // The selection, for other modules (heightmaps): its outline and the weights of its points.
  ed.area = { polygon: () => polygon(), weights: (withMask = true) => areaWeights(withMask), commit: (state, touched, a, extra) => commitTerrain(state, touched, a, extra) };
  ed.startPaste = () => startPaste();
  function startPaste() {
    if (!clip) { ed.msg('Copy an area first (Area tool, Ctrl+C).', true); return; }
    ed.setTool('paste');
  }
  // Source offset (from the clip centre) -> destination offset, for the turn and mirror.
  function xf(dx, dz) {
    if (flip) dx = -dx;
    const t = rot * Math.PI / 180, c = Math.cos(t), s = Math.sin(t);
    return [dx * c - dz * s, dx * s + dz * c];
  }
  function inv(dx, dz) {
    const t = -rot * Math.PI / 180, c = Math.cos(t), s = Math.sin(t);
    [dx, dz] = [dx * c - dz * s, dx * s + dz * c];
    if (flip) dx = -dx;
    return [dx, dz];
  }
  const turnPaste = deg => { rot = ((rot + deg) % 360 + 360) % 360; updateGhost(); };
  const ghost = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color: 0xffc24a, size: 5, sizeAttenuation: false, depthTest: false }));
  ghost.renderOrder = 16; ghost.frustumCulled = false; ed.scene.add(ghost);
  let pasteAt = null;
  // Repeat: the offset (from the clip centre, before turning) and height step between two copies.
  // Each copy is the size of the copy plus the gap along the chosen direction, like WorldEdit's //stack.
  function repeatStep() {
    let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity, y0 = Infinity, y1 = -Infinity;
    for (const p of clip.poly) { x0 = Math.min(x0, p.gx); x1 = Math.max(x1, p.gx); z0 = Math.min(z0, p.gz); z1 = Math.max(z1, p.gz); }
    for (const o of clip.objects) { y0 = Math.min(y0, o.dy); y1 = Math.max(y1, o.dy); }
    const gap = +$('psGap').value || 0, dir = $('psDir').value;
    if (dir === 'y') return { dx: 0, dz: 0, dy: (isFinite(y0) ? Math.max(1, y1 - y0 + 1) : 2) + gap };
    return dir === 'x' ? { dx: x1 - x0 + gap, dz: 0, dy: 0 } : { dx: 0, dz: z1 - z0 + gap, dy: 0 };
  }
  const copies = () => Math.max(1, Math.min(50, Math.round(+$('psCount').value || 1)));
  // Where copy k goes: its anchor point on the ground and the height added to it.
  function copyAt(at, k) {
    const st = repeatStep(), [x, z] = xf(st.dx * k, st.dz * k);
    return { gx: at.gx + x, gz: at.gz + z, up: st.dy * k };
  }
  const extraOutlines = new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0x5fd4ff, transparent: true, opacity: 0.6, depthTest: false }));
  extraOutlines.renderOrder = 15; extraOutlines.frustumCulled = false; ed.scene.add(extraOutlines);
  function updateGhost() {
    const show = ed.tool === 'paste' && clip && pasteAt;
    ghost.visible = extraOutlines.visible = !!show;
    if (!show) { if (ed.tool === 'paste') outline.geometry.setFromPoints([]); return; }
    drawOutline(clip.poly.map(p => { const [x, z] = xf(p.gx, p.gz); return { gx: pasteAt.gx + x, gz: pasteAt.gz + z }; }), false);
    const n = copies(), segs = [], dots = [], baseH = ed.sampleHeight(pasteAt.gx, pasteAt.gz) + +$('psOffset').value;
    for (let k = 0; k < n; k++) {
      const c = copyAt(pasteAt, k), up = $('psDir').value === 'y';
      const anchorH = (up ? baseH : ed.sampleHeight(c.gx, c.gz) + +$('psOffset').value) + c.up;
      for (const o of clip.objects) {
        const [x, z] = xf(o.dx, o.dz);
        const y = o.follow ? ed.sampleHeight(c.gx + x, c.gz + z) + o.dy + c.up : anchorH + o.dy;
        dots.push(new THREE.Vector3(c.gx + x - ed.cx, y + 0.5, -(c.gz + z - ed.cz)));
      }
      if (k === 0) continue;
      const ring = clip.poly.map(p => { const [x, z] = xf(p.gx, p.gz); return [c.gx + x, c.gz + z]; });
      ring.forEach(([ax, az], i) => {
        const [bx, bz] = ring[(i + 1) % ring.length];
        segs.push(new THREE.Vector3(ax - ed.cx, ed.sampleHeight(ax, az) + 0.3 + c.up, -(az - ed.cz)), new THREE.Vector3(bx - ed.cx, ed.sampleHeight(bx, bz) + 0.3 + c.up, -(bz - ed.cz)));
      });
    }
    ghost.geometry.setFromPoints(dots);
    extraOutlines.geometry.setFromPoints(segs);
    $('psInfo').textContent = `${clip.name ? clip.name + ': ' : ''}${clip.w} × ${clip.h} m, ${clip.objects.length} object(s)${n > 1 ? `, ${n} copies` : ''} · turned ${+rot.toFixed(1)}°${flip ? ', mirrored' : ''}.`;
  }
  // One paste (with its repeats) is one undo step.
  async function paste(at) {
    const n = copies(), up = $('psDir').value === 'y';
    const state = ed.snapshotState(), touched = new Set(), list = [];
    const half = Math.ceil(Math.hypot(clip.w, clip.h) / 2) + 1;
    let bx0 = W, bx1 = 0, bz0 = H, bz1 = 0;
    const baseH = ed.sampleHeight(at.gx, at.gz) + +$('psOffset').value;
    for (let k = 0; k < n; k++) {
      const c = copyAt(at, k);
      const anchorH = (up ? baseH : ed.sampleHeight(c.gx, c.gz) + +$('psOffset').value) + c.up;
      const x0 = Math.max(1, Math.floor(c.gx - half)), x1 = Math.min(W - 2, Math.ceil(c.gx + half)), z0 = Math.max(1, Math.floor(c.gz - half)), z1 = Math.min(H - 2, Math.ceil(c.gz + half));
      // Stacked upwards, only the first copy shapes the ground.
      if ($('psGround').checked && !(up && k > 0)) {
        bx0 = Math.min(bx0, x0); bx1 = Math.max(bx1, x1); bz0 = Math.min(bz0, z0); bz1 = Math.max(bz1, z1);
        for (let gz = z0; gz <= z1; gz++) for (let gx = x0; gx <= x1; gx++) {
          const [sx, sz] = inv(gx - c.gx, gz - c.gz);
          const ix = Math.round(sx + (clip.w - 1) / 2), iz = Math.round(sz + (clip.h - 1) / 2);
          if (ix < 0 || iz < 0 || ix >= clip.w || iz >= clip.h) continue;
          const i = iz * clip.w + ix, rel = clip.rel[i];
          if (Number.isNaN(rel)) continue;
          const g = gz * W + gx, w = clip.wt[i] * ed.mask(g);
          if (w <= 0 || ed.locked(gx, gz)) continue;
          const h = ed.height(g);
          ed.setHeight(g, h + (anchorH + rel - h) * w);
          if (clip.pnt[i * 4] >= 0) {
            if (!ed.pmod[g]) { ed.paint.set([0, 0, 0, 1], g * 4); ed.pmod[g] = 1; }
            for (let ch = 0; ch < 4; ch++) ed.paint[g * 4 + ch] += (clip.pnt[i * 4 + ch] - ed.paint[g * 4 + ch]) * w;
          }
          touched.add(g);
        }
      }
      if ($('psObjects').checked) for (const o of clip.objects) {
        const [x, z] = xf(o.dx, o.dz);
        // Objects copied with the Select tool keep their height above the ground where they land.
        const y = o.follow ? ed.sampleHeight(c.gx + x, c.gz + z) + o.dy + c.up : anchorH + o.dy;
        list.push({ name: o.name, x: ed.originX + c.gx + x, y, z: ed.originZ + c.gz + z, rx: flip ? -o.rx : o.rx, ry: (flip ? -o.ry : o.ry) - rot, rz: flip ? -o.rz : o.rz, scale: o.scale, sourceId: o.sourceId ?? null, fresh: true });
      }
    }
    const added = list.length ? await ed.objects.add(list) : [];
    commitTerrain(state, touched, { x0: Math.min(bx0, bx1), x1: bx1, z0: Math.min(bz0, bz1), z1: bz1 }, { label: n > 1 ? `Paste ×${n}` : 'Paste', ...(added.length ? { added } : {}) });
    ed.msg(`Pasted${n > 1 ? ` ${n} copies` : ''}${touched.size ? ' with the ground' : ''}${added.length ? `, ${added.length} object(s)` : ''}. Click again to paste more, Esc when done.`);
  }
  $('psRot').onclick = () => turnPaste(90);
  // Alt + mouse wheel turns the paste in small steps.
  ed.el.addEventListener('wheel', e => {
    if (ed.tool !== 'paste' || !e.altKey) return;
    e.preventDefault(); e.stopImmediatePropagation();
    turnPaste(-Math.sign(e.deltaY) * ed.turnStep(e));
  }, { capture: true, passive: false });
  $('psFlip').onclick = () => { flip = !flip; updateGhost(); };
  $('psDone').onclick = () => ed.setTool('area');
  for (const id of ['psOffset', 'psCount', 'psDir', 'psGap']) $(id).addEventListener('input', updateGhost);

  // ---- Restore from a backup (WorldEdit's //restore): the ground and objects inside the selection as
  // they were in a backup of this world. Objects unchanged since then are left alone.
  let backupOpen = null;
  async function fillBackups() {
    const list = await (await fetch('/api/backups')).json().catch(() => []);
    const keep = $('aBackup').value;
    $('aBackup').innerHTML = `<option value="">Choose a backup…</option>${list.map(b => `<option value="${b.path.replace(/"/g, '&quot;')}">${b.kind === 'game' ? 'Game' : 'Editor'} · ${new Date(b.date).toLocaleString()}</option>`).join('')}<option value="__other">Another folder…</option>`;
    if (keep && [...$('aBackup').options].some(o => o.value === keep)) $('aBackup').value = keep;
  }
  $('aBackup').addEventListener('change', async () => {
    if ($('aBackup').value !== '__other') return;
    const path = isWindow ? await pickFolder('Choose a backup (or copy) of this world: the folder with _main.<n>.chunks') : prompt('Folder of a backup or copy of this world (with its _main.<n>.chunks file):');
    if (!path) { $('aBackup').value = ''; return; }
    const o = document.createElement('option'); o.value = path; o.textContent = path;
    $('aBackup').insertBefore(o, $('aBackup').lastElementChild); $('aBackup').value = path;
  });
  ed.onToolChange.push(t => { if (t === 'area') fillBackups(); });
  async function openBackup(path) {
    if (backupOpen?.path === path) return backupOpen;
    $('aBkInfo').textContent = 'Reading the backup…';
    const r = await fetch('/api/backup/open', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ path }) });
    if (!r.ok) { const why = await r.text(); $('aBkInfo').textContent = why; ed.msg(why, true); return null; }
    backupOpen = await r.json();
    $('aBkInfo').textContent = `Backup: save #${backupOpen.saveNumber} of ${backupOpen.name}, ${backupOpen.objects.toLocaleString()} objects (${new Date(backupOpen.date).toLocaleString()}).`;
    return backupOpen;
  }
  $('aBkRestore').onclick = async () => {
    const poly = polygon();
    if (!poly) { ed.msg('Select an area first.', true); return; }
    const path = $('aBackup').value;
    if (!path || path === '__other') { ed.msg('Choose a backup first.', true); return; }
    if (!await openBackup(path)) return;
    const box = `x0=${ed.X0}&z0=${ed.Z0}&x1=${ed.X1}&z1=${ed.Z1}`;
    const state = ed.snapshotState(), touched = new Set();
    let a = { x0: 0, x1: W - 1, z0: 0, z1: H - 1 };
    if ($('aBkGround').checked) {
      const { zones } = await (await fetch(`/api/backup/region?${box}`)).json();
      const bmod = new Uint8Array(N), blevel = new Float32Array(N), bsmooth = new Float32Array(N), bpmod = new Uint8Array(N), bpaint = new Float32Array(N * 4);
      for (const z of zones) {
        const ox = (z.x - ed.X0) * 64, oz = (z.z - ed.Z0) * 64;
        for (let k = 0; k < 65; k++) for (let l = 0; l < 65; l++) {
          const i = k * 65 + l, g = (oz + k) * W + ox + l;
          bmod[g] = z.modified[i] ? 1 : 0; blevel[g] = z.level[i]; bsmooth[g] = z.smooth[i]; bpmod[g] = z.paintModified[i] ? 1 : 0;
          for (let c = 0; c < 4; c++) bpaint[g * 4 + c] = z.paint[i * 4 + c];
        }
      }
      const aw = areaWeights(); a = aw;
      for (const [g, w] of aw.cells) {
        const gx = g % W, gz = (g - gx) / W;
        if (ed.locked(gx, gz)) continue;
        if (w > 0.999) {
          // Inside the soft edge: exactly as in the backup.
          ed.mod[g] = bmod[g]; ed.level[g] = blevel[g]; ed.smooth[g] = bsmooth[g]; ed.pmod[g] = bpmod[g];
          for (let c = 0; c < 4; c++) ed.paint[g * 4 + c] = bpaint[g * 4 + c];
        } else {
          const hb = bmod[g] ? Math.max(ed.base[g] - 8, Math.min(ed.base[g] + 8, ed.base[g] + blevel[g] + bsmooth[g])) : ed.base[g], h = ed.height(g);
          ed.setHeight(g, h + (hb - h) * w);
          if (bpmod[g] || ed.pmod[g]) {
            if (!ed.pmod[g]) { ed.paint.set([0, 0, 0, 1], g * 4); ed.pmod[g] = 1; }
            const target = bpmod[g] ? [0, 1, 2, 3].map(c => bpaint[g * 4 + c]) : [0, 0, 0, 1];
            for (let c = 0; c < 4; c++) ed.paint[g * 4 + c] += (target[c] - ed.paint[g * 4 + c]) * w;
          }
        }
        touched.add(g);
      }
    }
    let deleted = [], added = [];
    if ($('aBkObjects').checked) {
      const back = await (await fetch(`/api/backup/objects?${box}`)).json();
      const pieceNames = ed.objects.state.pieceNames, nameOf = o => o.name ?? ed.objects.state.names[o.prefab] ?? String(o.prefab);
      const kindOf = o => o.piece || pieceNames.has(nameOf(o)) ? 'buildings' : objectKind(nameOf(o), pieceNames);
      const inPoly = o => inside(poly, o.x - ed.originX, o.z - ed.originZ);
      const wanted = back.filter(o => inPoly(o) && kindOn.has(kindOf(o)));
      const current = objectsInside(poly).filter(r => kindOn.has(r.kind));
      // Unchanged since the backup (same kind, place and facing): kept as they are.
      const same = (r, o) => r.prefab === o.prefab && Math.abs(r.x - o.x) < 0.01 && Math.abs(r.y - o.y) < 0.01 && Math.abs(r.z - o.z) < 0.01 && Math.abs(((r.ry - o.ry) % 360 + 540) % 360 - 180) < 0.6;
      const keep = new Set(), bring = [];
      for (const o of wanted) { const r = current.find(c => !keep.has(c.id) && same(c, o)); if (r) keep.add(r.id); else bring.push(o); }
      deleted = current.filter(r => !keep.has(r.id)).map(r => r.id);
      const pairs = bring.map(o => ({ backupId: o.id, newId: ed.objects.reserveId(), o }));
      if (deleted.length) await ed.setDeleted(deleted, true);
      if (pairs.length) {
        const res = await (await fetch('/api/backup/restore', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pairs: pairs.map(p => ({ backupId: p.backupId, newId: p.newId })) }) })).json();
        ed.showPendingFrom(res.pending);
        for (const p of pairs) await ed.objects.adopt({ id: p.newId, prefab: p.o.prefab, name: nameOf(p.o), x: p.o.x, y: p.o.y, z: p.o.z, rx: p.o.rx, ry: p.o.ry, rz: p.o.rz, scale: 0 });
        added = pairs.map(p => p.newId);
        ed.changed?.();
      }
    }
    if (!touched.size && !deleted.length && !added.length) { ed.msg('Nothing to restore: the selection is as it was in the backup.'); return; }
    commitTerrain(state, touched, a, { label: 'Restored from backup', ...(deleted.length ? { deleted } : {}), ...(added.length ? { added } : {}) });
    ed.msg(`Restored from the backup: ${touched.size ? 'the ground, ' : ''}${added.length} object(s) brought back, ${deleted.length} removed. Ctrl+Z undoes it.`);
    updateInfo();
  };

  // ---- Reset zones.
  const resetGroup = new THREE.Group(); ed.scene.add(resetGroup);
  const resetZones = new Map();   // "x,z" -> { keepBuildings, ground }
  for (const z of ed.region.zones) if (z.reset) resetZones.set(`${z.x},${z.z}`, z.reset);
  function drawResets() {
    resetGroup.clear();
    for (const key of resetZones.keys()) {
      const [zx, zz] = key.split(',').map(Number), gx0 = (zx - ed.X0) * 64, gz0 = (zz - ed.Z0) * 64;
      const p = [];
      for (const [a, b] of [[0, 0], [64, 0], [64, 64], [0, 64], [0, 0]]) p.push(new THREE.Vector3(gx0 + a - ed.cx, ed.sampleHeight(gx0 + a, gz0 + b) + 0.6, -(gz0 + b - ed.cz)));
      const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(p), new THREE.LineBasicMaterial({ color: 0xff5040, depthTest: false }));
      line.renderOrder = 14; resetGroup.add(line);
    }
  }
  drawResets();
  function zonesUnder() {
    const a = areaWeights(false); if (!a) return [];
    const set = new Set();
    for (const [g] of a.cells) {
      const gx = g % W, gz = (g - gx) / W;
      const zx = Math.min(ed.size - 1, Math.floor(gx / 64)), zz = Math.min(ed.size - 1, Math.floor(gz / 64));
      set.add(`${ed.X0 + zx},${ed.Z0 + zz}`);
    }
    return [...set].map(s => s.split(',').map(Number));
  }
  async function postResets(zones, keepBuildings, ground, undo) {
    const res = await (await fetch('/api/reset-zones', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ zones, keepBuildings, ground, undo }) })).json();
    for (const [x, z] of zones) undo ? resetZones.delete(`${x},${z}`) : resetZones.set(`${x},${z}`, { keepBuildings, ground });
    drawResets(); ed.showPendingFrom(res);
  }
  ed.setResets = (r, forward) => (ed.track ?? (p => p))(postResets(r.zones, r.keepBuildings, r.ground, !forward));
  $('aReset').onclick = async () => {
    const zones = zonesUnder();
    if (!zones.length) { ed.msg('Select an area first.', true); return; }
    const keep = $('aKeepB').checked, ground = $('aResetGround').checked;
    if (!confirm(`Reset ${zones.length} zone(s) (${zones.map(z => z.join(', ')).join(' · ')}) when you save?\n\n` +
      `• Every tree, rock, ruin and dungeon entrance in them is removed${keep ? ' (your buildings stay)' : ', your buildings too'}.\n` +
      `${ground ? '• Their ground edits are undone.\n' : ''}• Valheim generates the zones again the next time a player goes there.`)) return;
    const extra = { resets: { zones, keepBuildings: keep, ground }, label: `Reset ${zones.length} zone(s)` };
    let state = null; const touched = new Set();
    if (ground) {
      state = ed.snapshotState();
      for (const [zx, zz] of zones) {
        const gx0 = (zx - ed.X0) * 64, gz0 = (zz - ed.Z0) * 64;
        for (let k = 0; k <= 64; k++) for (let l = 0; l <= 64; l++) {
          const gx = gx0 + l, gz = gz0 + k; if (gx >= W || gz >= H) continue;
          const g = gz * W + gx;
          if (ed.mod[g] || ed.pmod[g]) { ed.mod[g] = 0; ed.level[g] = 0; ed.smooth[g] = 0; ed.pmod[g] = 0; touched.add(g); }
        }
      }
    }
    await postResets(zones, keep, ground, false);
    if (state && touched.size) commitTerrain(state, touched, { x0: 0, x1: W - 1, z0: 0, z1: H - 1 }, extra);
    else ed.pushHistory(extra);
    ed.msg(`${zones.length} zone(s) marked for reset (red outline). Save to apply; Ctrl+Z cancels.`);
  };
  $('aUnreset').onclick = async () => {
    const zones = zonesUnder().filter(([x, z]) => resetZones.has(`${x},${z}`));
    if (!zones.length) { ed.msg('No zone marked for reset under the selection.'); return; }
    const r = resetZones.get(`${zones[0][0]},${zones[0][1]}`);
    await postResets(zones, r.keepBuildings, r.ground, true);
    ed.pushHistory({ resets: { zones, keepBuildings: r.keepBuildings, ground: r.ground }, resetUndo: true, label: `Cancelled reset of ${zones.length} zone(s)` });
    ed.msg(`Reset cancelled for ${zones.length} zone(s).`);
  };

  // ---- Pointer and keys.
  ed.handlers.area = {
    down(e, hit) {
      if (!hit) return;
      if (shape === 'box') {
        ed.el.setPointerCapture(e.pointerId);
        pts = [{ gx: hit.gx, gz: hit.gz }, { gx: hit.gx, gz: hit.gz }]; dragging = true; redraw();
      } else {
        if (closed) { pts = []; closed = false; }
        pts.push({ gx: hit.gx, gz: hit.gz }); redraw();
      }
    },
    move(e, hit) {
      ed.showStatusFor?.(hit);
      if (dragging && hit) { pts[1] = { gx: hit.gx, gz: hit.gz }; redraw(); }
    },
    up() {
      if (dragging) { dragging = false; if (Math.hypot(pts[1].gx - pts[0].gx, pts[1].gz - pts[0].gz) < 1) clear(); else redraw(); }
    },
    dblclick() { if (shape === 'poly' && pts.length >= 3) { closed = true; redraw(); } },
    key(e) {
      if (e.key === 'Escape') { clear(); return true; }
      if (shape === 'poly' && e.key === 'Enter' && pts.length >= 3) { closed = true; redraw(); return true; }
      if (shape === 'poly' && e.key === 'Backspace') { pts.pop(); redraw(); return true; }
      return false;
    }
  };
  ed.handlers.paste = {
    down(e, hit) { if (hit && clip) paste(hit); },
    move(e, hit) { ed.showStatusFor?.(hit); if (hit) { pasteAt = hit; updateGhost(); } },
    key(e) {
      const k = e.key.toLowerCase();
      if (k === 'r') { turnPaste(90); return true; }
      // , . turn in small steps (. clockwise seen from above, like the other tools).
      if (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>') { turnPaste((e.key === ',' || e.key === '<' ? 1 : -1) * ed.turnStep(e)); return true; }
      if (k === 'f') { flip = !flip; updateGhost(); return true; }
      if (e.key === 'Escape') { ed.setTool('area'); return true; }
      return false;
    }
  };
  (ed.keys ??= []).push(e => {
    if (!e.ctrlKey) return false;
    const k = e.key.toLowerCase();
    if (k === 'c' && ed.tool === 'area') { copy(); return true; }
    if (k === 'v') { startPaste(); return true; }
    return false;
  });
  ed.onToolChange ??= [];
  ed.onToolChange.push(t => { outline.visible = t === 'area' || t === 'paste' || !!polygon(); if (t !== 'paste') ghost.visible = extraOutlines.visible = false; if (t === 'area') redraw(); });
  redraw();
  return { clear };
}
