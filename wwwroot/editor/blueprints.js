// Blueprints (WorldEdit's schematics, Axiom's blueprint browser): the clipboard saved under a name as
// a file in the app's data folder, listed with a picture, and pasted again in any area or world.
import { encodeClip, decodeClip } from './area.js';
import { objectKind } from './objects.js';
import { isWindow, pickFile } from '../app.js';

const KIND_COLOR = { buildings: '#c98a4b', ruins: '#a08cc8', trees: '#2f6b2a', rocks: '#9a9a9a', ore: '#e0803a', bushes: '#7cbf5a', pickables: '#e8d24a', other: '#e6e9ee' };

// A small picture of a copy seen from above (north up): the ground shaded by height, objects as dots.
export function drawThumb(clip, pieceNames = new Set(), size = 160) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d');
  g.fillStyle = '#1e232b'; g.fillRect(0, 0, size, size);
  // Extent: the outline, the ground grid and every object.
  let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
  const grow = (x, z) => { x0 = Math.min(x0, x); x1 = Math.max(x1, x); z0 = Math.min(z0, z); z1 = Math.max(z1, z); };
  for (const p of clip.poly ?? []) grow(p.gx, p.gz);
  for (const o of clip.objects) grow(o.dx, o.dz);
  const hasGround = clip.rel.some(v => !Number.isNaN(v));
  if (hasGround) { grow(-(clip.w - 1) / 2, -(clip.h - 1) / 2); grow((clip.w - 1) / 2, (clip.h - 1) / 2); }
  if (!isFinite(x0)) return c.toDataURL('image/png');
  const span = Math.max(4, x1 - x0, z1 - z0) * 1.1, mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
  const sx = x => (x - mx) / span * size + size / 2, sy = z => size / 2 - (z - mz) / span * size;
  if (hasGround) {
    let lo = Infinity, hi = -Infinity;
    for (const v of clip.rel) if (!Number.isNaN(v)) { lo = Math.min(lo, v); hi = Math.max(hi, v); }
    const cell = Math.max(1, size / span);
    for (let iz = 0; iz < clip.h; iz++) for (let ix = 0; ix < clip.w; ix++) {
      const v = clip.rel[iz * clip.w + ix];
      if (Number.isNaN(v)) continue;
      const t = hi > lo ? (v - lo) / (hi - lo) : 0.5, painted = clip.pnt[(iz * clip.w + ix) * 4] >= 0;
      const col = painted ? [150 + 40 * t, 120 + 30 * t, 80 + 20 * t] : [70 + 110 * t, 110 + 70 * t, 55 + 60 * t];
      g.fillStyle = `rgb(${col.map(Math.round).join(',')})`;
      g.fillRect(sx(ix - (clip.w - 1) / 2) - cell / 2, sy(iz - (clip.h - 1) / 2) - cell / 2, cell + 0.5, cell + 0.5);
    }
  }
  if (clip.poly?.length) {
    g.strokeStyle = 'rgba(95,212,255,.8)'; g.lineWidth = 1.5; g.beginPath();
    clip.poly.forEach((p, i) => i ? g.lineTo(sx(p.gx), sy(p.gz)) : g.moveTo(sx(p.gx), sy(p.gz)));
    g.closePath(); g.stroke();
  }
  const r = Math.max(1.5, Math.min(4, size / span * 0.6));
  for (const o of clip.objects) {
    g.fillStyle = KIND_COLOR[objectKind(o.name, pieceNames)] ?? KIND_COLOR.other;
    g.beginPath(); g.arc(sx(o.dx), sy(o.dz), r, 0, Math.PI * 2); g.fill();
  }
  return c.toDataURL('image/png');
}

export function createBlueprints(ed) {
  const { $ } = ed;
  const panel = document.createElement('aside');
  panel.id = 'bpPanel'; panel.className = 'card side'; panel.hidden = true;
  panel.innerHTML = `
    <h3>Blueprints <button class="ghost" id="bpClose" title="Close">✕</button></h3>
    <div class="row" style="margin:0 0 6px"><input id="bpSearch" type="search" placeholder="Search blueprints" style="flex:2"><button id="bpImport" title="Import a PlanBuild .blueprint or a .vbuild file">Import file…</button></div>
    <input id="bpFile" type="file" accept=".blueprint,.vbuild" hidden>
    <div id="bpList"></div>
    <div class="hint" id="bpFolder"></div>`;
  document.body.appendChild(panel);
  const style = document.createElement('style');
  style.textContent = `
    #bpPanel { position: fixed; right: 10px; top: 70px; width: 320px; padding: 12px 14px; max-height: calc(100vh - 120px); overflow-y: auto; z-index: 6; }
    #bpPanel h3 { margin: 0 0 8px; font-size: 14px; display: flex; justify-content: space-between; align-items: center; }
    .bp { display: grid; grid-template-columns: 64px 1fr; gap: 4px 10px; padding: 6px; border: 1px solid transparent; border-radius: 8px; }
    .bp:hover { border-color: var(--line); background: rgba(255,255,255,.03); }
    .bp img { width: 64px; height: 64px; border-radius: 6px; border: 1px solid var(--line); grid-row: span 2; image-rendering: pixelated; }
    .bp .nm { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bp .mt { color: var(--muted); font-size: 11px; }
    .bp .acts { grid-column: 2; display: flex; gap: 4px; flex-wrap: wrap; }
    .bp .acts button { padding: 3px 7px; font-size: 11px; }`;
  document.head.appendChild(style);

  let list = [];
  async function refresh() {
    try {
      const res = await (await fetch('/api/blueprints')).json();
      list = res.list;
      $('bpFolder').textContent = `Kept as files in ${res.folder}. Each one can be pasted into any world.`;
    } catch (err) { list = []; ed.msg(`Could not read the blueprints: ${err.message}`, true); }
    render();
  }
  const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  function render() {
    const q = $('bpSearch').value.trim().toLowerCase();
    const shown = list.filter(b => !q || b.name.toLowerCase().includes(q));
    $('bpList').innerHTML = shown.length ? shown.map(b => `<div class="bp" data-id="${esc(b.id)}">
      <img alt="" src="${b.thumb ? esc(b.thumb) : 'data:,'}">
      <div><div class="nm" title="${esc(b.name)}">${esc(b.name)}</div>
        <div class="mt">${b.w} × ${b.h} m · ${b.objects} object(s)${b.ground ? ' · ground' : ''}${b.world ? ` · from ${esc(b.world)}` : ''}${b.source ? ` · ${esc(b.source)}` : ''}</div></div>
      <div class="acts"><button data-act="paste" class="primary" title="Put this blueprint on the clipboard and start pasting it">Paste</button><button data-act="blueprint" title="Write it as a PlanBuild .blueprint file">.blueprint</button><button data-act="vbuild" title="Write it as a .vbuild file">.vbuild</button><button data-act="delete" title="Delete this blueprint file">Delete</button></div></div>`).join('')
      : `<div class="hint">${list.length ? 'Nothing matches.' : 'No blueprints yet. Copy something (Area or Select tool, Ctrl+C), then “Save blueprint…”.'}</div>`;
    $('bpList').querySelectorAll('[data-act]').forEach(btn => btn.onclick = () => {
      const id = btn.closest('.bp').dataset.id;
      if (btn.dataset.act === 'paste') pasteBlueprint(id);
      else if (btn.dataset.act === 'delete') removeBlueprint(id);
      else exportBlueprint(id, btn.dataset.act);
    });
  }
  $('bpSearch').addEventListener('input', render);

  // Loads a blueprint into the clipboard. Object ids only mean something in the world they came
  // from: elsewhere new objects are copied from that world's own objects of the same kind.
  async function pasteBlueprint(id) {
    const r = await fetch(`/api/blueprints/${encodeURIComponent(id)}`);
    if (!r.ok) { ed.msg('That blueprint could not be read.', true); refresh(); return; }
    const doc = await r.json();
    const clip = decodeClip(doc.clip);
    clip.name = doc.name;
    if (doc.world !== ed.world.name) clip.objects = clip.objects.map(o => ({ ...o, sourceId: null }));
    const known = ed.objects.state.byName;
    const missing = [...new Set(clip.objects.filter(o => !ed.objects.state.templates.has(known.get(o.name) ?? ed.objects.stableHash(o.name))).map(o => o.name))];
    ed.setClipboard(clip);
    toggle(false);
    ed.startPaste();
    ed.msg(`“${doc.name}” is on the clipboard: click to place it.${missing.length ? ` ${missing.length} kind(s) are unknown here and are left out: ${missing.slice(0, 5).join(', ')}${missing.length > 5 ? '…' : ''}.` : ''}`, missing.length > 0);
  }
  async function removeBlueprint(id) {
    const b = list.find(x => x.id === id);
    if (!confirm(`Delete the blueprint “${b?.name ?? id}”? Its file is removed.`)) return;
    await fetch(`/api/blueprints/${encodeURIComponent(id)}`, { method: 'DELETE' });
    refresh();
  }
  // Saves the clipboard as a blueprint (asks for the name; a blueprint with that name is replaced).
  async function saveBlueprint(source = null) {
    const clip = ed.getClipboard();
    if (!clip) { ed.msg('Copy something first (Area or Select tool, Ctrl+C), then save it as a blueprint.', true); return null; }
    const d = new Date(), pad = n => String(n).padStart(2, '0');
    const name = prompt('Name of the blueprint:', clip.name ?? `Blueprint ${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}.${pad(d.getMinutes())}`)?.trim();
    if (!name) return null;
    const { exists } = await (await fetch(`/api/blueprints/exists?name=${encodeURIComponent(name)}`)).json();
    if (exists && !confirm(`A blueprint called “${name}” already exists. Replace it?`)) return null;
    const thumb = drawThumb(clip, ed.objects.state.pieceNames);
    const r = await fetch('/api/blueprints', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, thumb, clip: encodeClip({ ...clip, name }), source }) });
    if (!r.ok) { ed.msg(`Could not save the blueprint: ${await r.text()}`, true); return null; }
    ed.setClipboard({ ...clip, name });
    ed.msg(`Saved the blueprint “${name}” (${clip.objects.length} object(s)). Find it under Blueprints… in any world.`);
    if (!panel.hidden) refresh();
    return name;
  }
  // Other mods' blueprint files: read by the editor, then kept as a blueprint like any copy.
  async function importFile(req) {
    const r = await fetch('/api/blueprints/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req) });
    if (!r.ok) { ed.msg(`Could not import that file: ${await r.text()}`, true); return null; }
    const res = await r.json();
    const clip = decodeClip(res.clip);
    let name = res.name || 'Imported';
    for (let n = 2; (await (await fetch(`/api/blueprints/exists?name=${encodeURIComponent(name)}`)).json()).exists; n++) name = `${res.name} (${n})`;
    const thumb = drawThumb(clip, ed.objects.state.pieceNames);
    const source = (req.fileName ?? req.path ?? '').toLowerCase().endsWith('.vbuild') ? 'vbuild' : 'PlanBuild';
    const save = await fetch('/api/blueprints', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, thumb, clip: encodeClip({ ...clip, name }), source }) });
    if (!save.ok) { ed.msg(`Could not keep the imported blueprint: ${await save.text()}`, true); return null; }
    await refresh();
    const left = res.unknown.length ? ` ${res.unknown.length} kind(s) the game does not know were left out (mods?): ${res.unknown.slice(0, 5).join(', ')}${res.unknown.length > 5 ? '…' : ''}.` : '';
    ed.msg(`Imported “${name}”: ${clip.objects.length} piece(s)${res.terrain ? `, ground from ${res.terrain} terrain mark(s)` : ''}.${left}${res.skipped ? ` ${res.skipped} unreadable line(s) skipped.` : ''}`, !!res.unknown.length);
    return name;
  }
  $('bpImport').onclick = async () => {
    if (isWindow) { const path = await pickFile('Import a PlanBuild .blueprint or .vbuild file'); if (path) importFile({ path }); return; }
    $('bpFile').click();
  };
  $('bpFile').onchange = async () => {
    const f = $('bpFile').files[0]; $('bpFile').value = '';
    if (f) importFile({ fileName: f.name, text: await f.text() });
  };
  // Writes a blueprint for PlanBuild (or as .vbuild) into the export folder; in a browser it is also downloaded.
  async function exportBlueprint(id, format) {
    const r = await fetch(`/api/blueprints/${encodeURIComponent(id)}/export`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ format }) });
    if (!r.ok) { ed.msg('Could not export that blueprint.', true); return; }
    const res = await r.json();
    if (!isWindow) {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(new Blob([res.text], { type: 'text/plain' })); a.download = res.fileName; a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    }
    ed.msg(`Written to ${res.path}.${format === 'blueprint' ? ' For PlanBuild, copy it into BepInEx/config/PlanBuild/blueprints.' : ''} Only the objects are written, not the ground.`);
  }
  function toggle(open = panel.hidden) {
    panel.hidden = !open;
    if (open) {
      for (const id of ['viewPanel', 'historyPanel', 'help']) { const el = $(id); if (el) el.hidden = true; }
      for (const id of ['viewToggle', 'historyToggle', 'helpToggle']) $(id)?.classList.remove('on');
      refresh();
    }
  }
  $('bpClose').onclick = () => toggle(false);
  $('aSaveBp').onclick = () => saveBlueprint();
  $('aLibrary').onclick = () => toggle();
  ed.blueprints = { toggle, refresh, save: saveBlueprint, paste: pasteBlueprint, importFile, exportBlueprint };
  return ed.blueprints;
}
