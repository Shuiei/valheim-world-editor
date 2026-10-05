// Masks (like WorldEdit's masks or WorldPainter's filters): limit brushes, paths, area actions and
// planting to some biomes, a height range, a slope range or painted / unpainted ground.

const BIOMES = [
  [1, 'Meadows'], [8, 'Black Forest'], [2, 'Swamp'], [4, 'Mountain'], [16, 'Plains'],
  [512, 'Mistlands'], [32, 'Ashlands'], [64, 'Deep North'], [256, 'Ocean']
];

export function createMasks(ed, tools) {
  const { $ } = ed;
  const box = document.createElement('div');
  box.className = 'sub';
  box.id = 'maskBox';
  box.innerHTML = `
    <h3 class="maskHead"><label class="toggle" style="padding:0"><input type="checkbox" id="mOn"><span class="t">Mask</span></label></h3>
    <div id="mBody" hidden>
      <div class="hint" style="margin-top:0">Only change ground that matches everything below.</div>
      <div class="chips" id="mBiomes">${BIOMES.map(([v, n]) => `<button data-b="${v}">${n}</button>`).join('')}</div>
      <label class="field">Height <span class="pair"><input id="mHmin" type="number" step="0.5" placeholder="min"><input id="mHmax" type="number" step="0.5" placeholder="max"></span><span>m</span></label>
      <label class="field">Slope <span class="pair"><input id="mSmin" type="number" step="1" min="0" max="90" placeholder="0"><input id="mSmax" type="number" step="1" min="0" max="90" placeholder="90"></span><span>°</span></label>
      <label class="field">Paint <select id="mPaint">
        <option value="any">Any ground</option>
        <option value="unpainted">Only unpainted</option>
        <option value="painted">Only painted</option>
        <option value="dirt">Only dirt</option>
        <option value="cultivated">Only cultivated</option>
        <option value="paved">Only paved</option>
      </select></label>
      <div class="hint warn" id="mWarn" hidden></div>
      <div class="hint">No biome selected = all biomes. Leave a box empty for no limit. Alt + Shift + click the ground fills the height range around it (±2 m).</div>
    </div>`;
  $('locWarn').before(box);
  const style = document.createElement('style');
  style.textContent = `
    .chips { display: flex; flex-wrap: wrap; gap: 4px; margin: 6px 0; }
    .chips button { padding: 3px 7px; font-size: 11.5px; border-radius: 999px; }
    .chips button.on { background: var(--accent); color: var(--accent-ink); border-color: var(--accent); }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; }
    .field:has(.pair) { grid-template-columns: 60px 1fr 16px; }
    .maskHead { display: flex; } .maskHead .toggle { flex: 1; }
    #mWarn { color: var(--warn); }`;
  document.head.appendChild(style);
  ed.panels.push({ el: box, tools });

  const biomes = new Set();
  box.querySelectorAll('[data-b]').forEach(b => b.onclick = () => {
    const v = +b.dataset.b;
    biomes.has(v) ? biomes.delete(v) : biomes.add(v);
    b.classList.toggle('on', biomes.has(v));
    sync();
  });
  const num = id => { const v = $(id).value.trim(); return v === '' ? null : +v; };
  let cfg = null;
  function sync() {
    $('mBody').hidden = !$('mOn').checked;
    box.classList.toggle('active', $('mOn').checked);
    cfg = !$('mOn').checked ? null : {
      biomes: biomes.size ? new Set(biomes) : null,
      hmin: num('mHmin'), hmax: num('mHmax'), smin: num('mSmin'), smax: num('mSmax'), paint: $('mPaint').value
    };
    ed.maskActive = !!cfg;
    // Settings that let (almost) nothing through.
    const w = [];
    if (cfg?.smax != null && cfg.smax <= 0) w.push(`Slope max ${cfg.smax}° only lets perfectly flat ground through: empty the box for no limit.`);
    if (cfg?.smin != null && cfg.smax != null && cfg.smin > cfg.smax) w.push('Slope min is above max: nothing matches.');
    if (cfg?.hmin != null && cfg.hmax != null && cfg.hmin > cfg.hmax) w.push('Height min is above max: nothing matches.');
    $('mWarn').hidden = !w.length; $('mWarn').textContent = w.join(' ');
  }
  ['mOn', 'mHmin', 'mHmax', 'mSmin', 'mSmax', 'mPaint'].forEach(id => $(id).addEventListener('input', sync));
  sync();

  const { W, H } = ed;
  function slopeDeg(g) {
    const gx = g % W, gz = (g - gx) / W;
    const hx = ed.height(gz * W + Math.min(W - 1, gx + 1)) - ed.height(gz * W + Math.max(0, gx - 1));
    const hz = ed.height(Math.min(H - 1, gz + 1) * W + gx) - ed.height(Math.max(0, gz - 1) * W + gx);
    return Math.atan(Math.hypot(hx, hz) / 2) * 180 / Math.PI;
  }
  function paintOk(g) {
    const p = cfg.paint;
    if (p === 'any') return true;
    const painted = !!ed.pmod[g], r = ed.paint[g * 4], gr = ed.paint[g * 4 + 1], b = ed.paint[g * 4 + 2];
    if (p === 'unpainted') return !painted || r + gr + b < 0.3;
    if (!painted) return false;
    if (p === 'painted') return r + gr + b >= 0.3;
    if (p === 'dirt') return r >= 0.5;
    if (p === 'cultivated') return gr >= 0.5;
    return b >= 0.5;
  }
  // During a brush stroke the mask judges the ground as it was when the stroke started, so raising
  // past the height limit or changing the slope does not stop the stroke half-way.
  let frozen = null;
  ed.maskFreeze = on => { frozen = on && cfg ? new Map() : null; };
  // 1 where the ground passes the mask, 0 elsewhere.
  ed.mask = g => {
    if (!cfg) return 1;
    if (frozen) { let v = frozen.get(g); if (v === undefined) frozen.set(g, v = test(g)); return v; }
    return test(g);
  };
  function test(g) {
    if (cfg.biomes && !cfg.biomes.has(ed.vbiome[g])) return 0;
    if (cfg.hmin != null || cfg.hmax != null) {
      const h = ed.height(g);
      if ((cfg.hmin != null && h < cfg.hmin) || (cfg.hmax != null && h > cfg.hmax)) return 0;
    }
    if (cfg.smin != null || cfg.smax != null) {
      const s = slopeDeg(g);
      if ((cfg.smin != null && s < cfg.smin) || (cfg.smax != null && s > cfg.smax)) return 0;
    }
    return paintOk(g) ? 1 : 0;
  }
  // Alt + Shift + click: height range around the clicked ground.
  ed.pickMaskHeight = h => { $('mOn').checked = true; $('mHmin').value = (h - 2).toFixed(1); $('mHmax').value = (h + 2).toFixed(1); sync(); };
  return { sync };
}
