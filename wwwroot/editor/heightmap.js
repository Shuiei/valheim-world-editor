// Heightmaps (WorldPainter's heightmap import and export): the ground of the area as a 16-bit
// grayscale picture to change in an image editor or a terrain tool, and a picture back into the
// ground, fitted to the selection (or the whole area), with the lowest and highest heights chosen.
import { isWindow, pickFile } from '../app.js';

export function createHeightmap(ed) {
  const { $, W, H } = ed;
  const box = document.createElement('div');
  box.className = 'sub';
  box.innerHTML = `<h3>Heightmap</h3>
    <div class="row"><button id="hmExport" title="The ground of the whole area as a 16-bit grayscale picture">Export the area</button><button id="hmImport" title="A grayscale picture into the ground">Import…</button></div>
    <input id="hmFile" type="file" accept="image/png" hidden>
    <div id="hmBox" hidden>
      <div class="hint" id="hmInfo"></div>
      <label class="field">Lowest <input id="hmMin" type="number" step="0.5"><span>m</span></label>
      <label class="field">Highest <input id="hmMax" type="number" step="0.5"><span>m</span></label>
      <div class="row"><button id="hmApply" class="primary">Put it into the ground</button><button id="hmCancel">Cancel</button></div>
    </div>
    <div class="hint">Black is the lowest height, white the highest; north is at the top of the picture, one pixel per metre when exported. An import fits the picture to the selection (or the whole area) and works within the game's ±8 m.</div>`;
  $('areaPanel').appendChild(box);

  const area = () => `x0=${ed.X0}&z0=${ed.Z0}&x1=${ed.X1}&z1=${ed.Z1}`;
  $('hmExport').onclick = async () => {
    if (!isWindow) { const a = document.createElement('a'); a.href = `/api/heightmap.png?${area()}`; a.download = ''; a.click(); }
    const r = await fetch(`/api/heightmap/export?${area()}`, { method: 'POST' });
    if (!r.ok) { ed.msg(`Could not export: ${await r.text()}`, true); return; }
    const res = await r.json();
    ed.msg(`Heightmap written to ${res.path} (${res.width} × ${res.height}, ${res.min.toFixed(1)} to ${res.max.toFixed(1)} m${isWindow ? '' : '; also downloaded'}).`);
  };

  // Where the picture goes: the selection's box, or the whole area.
  function target() {
    const a = ed.area.weights();
    if (a) return { a, x0: a.x0, z0: a.z0, w: a.x1 - a.x0 + 1, h: a.z1 - a.z0 + 1 };
    const cells = [];
    for (let gz = 0; gz < H; gz++) for (let gx = 0; gx < W; gx++) { const g = gz * W + gx, m = ed.mask(g); if (m > 0) cells.push([g, m]); }
    return { a: { x0: 0, x1: W - 1, z0: 0, z1: H - 1, cells }, x0: 0, z0: 0, w: W, h: H };
  }
  let picture = null;   // { values, w, h, x0, z0, a }
  async function load(req) {
    const t = target();
    const url = `/api/heightmap/decode?w=${t.w}&h=${t.h}${req.path ? `&path=${encodeURIComponent(req.path)}` : ''}`;
    const r = await fetch(url, req.path ? { method: 'POST' } : { method: 'POST', headers: { 'Content-Type': 'application/octet-stream' }, body: req.bytes });
    if (!r.ok) { ed.msg(await r.text(), true); return; }
    const res = await r.json();
    picture = { ...t, values: res.values };
    // Heights written in the picture by the editor, else the range of the ground it covers now.
    let lo = Infinity, hi = -Infinity;
    for (const [g] of t.a.cells) { const v = ed.height(g); lo = Math.min(lo, v); hi = Math.max(hi, v); }
    // Heights from the picture are kept to the millimetre, so an exported area comes back unchanged.
    $('hmMin').value = res.min != null ? +res.min.toFixed(3) : lo.toFixed(1); $('hmMax').value = res.max != null ? +res.max.toFixed(3) : hi.toFixed(1);
    $('hmInfo').textContent = `Picture ${res.width} × ${res.height} fitted to ${t.w} × ${t.h} m (${ed.area.polygon() ? 'the selection' : 'the whole area'})${res.min != null ? ', with the heights it was exported with' : ''}.`;
    $('hmBox').hidden = false;
  }
  $('hmImport').onclick = async () => {
    if (isWindow) { const path = await pickFile('Import a heightmap (grayscale PNG)'); if (path) load({ path }); return; }
    $('hmFile').click();
  };
  $('hmFile').onchange = async () => {
    const f = $('hmFile').files[0]; $('hmFile').value = '';
    if (f) load({ bytes: await f.arrayBuffer() });
  };
  $('hmCancel').onclick = () => { picture = null; $('hmBox').hidden = true; };
  $('hmApply').onclick = () => {
    if (!picture) return;
    const lo = +$('hmMin').value, hi = +$('hmMax').value, p = picture;
    const state = ed.snapshotState(), touched = new Set();
    let clamped = 0;
    for (const [g, w] of p.a.cells) {
      const gx = g % W, gz = (g - gx) / W;
      if (ed.locked(gx, gz)) continue;
      const ix = gx - p.x0, iz = gz - p.z0;
      if (ix < 0 || iz < 0 || ix >= p.w || iz >= p.h) continue;
      const want = lo + p.values[iz * p.w + ix] * (hi - lo), h = ed.height(g);
      ed.setHeight(g, h + (want - h) * w);
      if (w > 0.999 && Math.abs(ed.height(g) - want) > 0.05) clamped++;
      touched.add(g);
    }
    ed.area.commit(state, touched, p.a, { label: 'Heightmap import' });
    picture = null; $('hmBox').hidden = true;
    ed.msg(clamped ? `Imported, but ${clamped} point(s) could not reach their height: the game keeps the ground within ±8 m of the original (red points).` : `Imported the heightmap into ${touched.size} point(s). Ctrl+Z undoes it.`, clamped > 0);
  };
}
