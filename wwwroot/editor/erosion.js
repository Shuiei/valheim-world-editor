// Erosion (VoxelSniper's erode brush, Axiom's erosion): ground that weathers like real ground.
// Thermal: where the ground is steeper than its resting angle, material slides down to its lower
// neighbours (screes, softened cliffs). Water: drops of rain run downhill, dig where they speed up and
// leave what they carry where they slow down (gullies, fans, smooth valleys). Both work on a local
// copy of the heights, then write the result back through setHeight (so the ±8 m limit applies).

const N8 = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [1, -1], [-1, 1], [-1, -1]];

// h: heights of a w x d window (row-major), wt: how much each point may change (0..1).
// talus: steepest slope (rise per metre) that stays put. amount: share of the excess moved per pass.
export function thermal(h, wt, w, d, talus, amount, passes) {
  const delta = new Float32Array(h.length);
  for (let p = 0; p < passes; p++) {
    delta.fill(0);
    for (let z = 1; z < d - 1; z++) for (let x = 1; x < w - 1; x++) {
      const i = z * w + x, k = wt[i];
      if (k <= 0) continue;
      let total = 0, maxD = 0;
      const ds = [];
      for (const [dx, dz] of N8) {
        const j = (z + dz) * w + x + dx, dist = dx && dz ? Math.SQRT2 : 1, diff = h[i] - h[j] - talus * dist;
        if (diff > 0 && wt[j] > 0) { ds.push([j, diff]); total += diff; maxD = Math.max(maxD, diff); }
      }
      if (!total) continue;
      // Move half of the largest excess (so the slope settles instead of swinging), shared out.
      const move = maxD * 0.5 * amount * k;
      delta[i] -= move;
      for (const [j, diff] of ds) delta[j] += move * diff / total;
    }
    for (let i = 0; i < h.length; i++) h[i] += delta[i];
  }
}

// Rain drops (after Hans Theobald Beyer's "Implementation of a method for hydraulic erosion").
export function hydraulic(h, wt, w, d, drops, strength, rand = Math.random) {
  const inertia = 0.05, capacityK = 4, deposit = 0.3, erode = 0.3 * strength, evaporate = 0.02, gravity = 4, minSlope = 0.01, life = 40;
  const at = (x, z) => h[z * w + x];
  function gradient(px, pz) {
    const x = Math.floor(px), z = Math.floor(pz), u = px - x, v = pz - z;
    const a = at(x, z), b = at(x + 1, z), c = at(x, z + 1), e = at(x + 1, z + 1);
    return { gx: (b - a) * (1 - v) + (e - c) * v, gz: (c - a) * (1 - u) + (e - b) * u, h: a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + e * u * v };
  }
  function change(px, pz, amount) {
    const x = Math.floor(px), z = Math.floor(pz), u = px - x, v = pz - z;
    for (const [dx, dz, f] of [[0, 0, (1 - u) * (1 - v)], [1, 0, u * (1 - v)], [0, 1, (1 - u) * v], [1, 1, u * v]]) {
      const i = (z + dz) * w + x + dx;
      h[i] += amount * f * wt[i];
    }
  }
  for (let n = 0; n < drops; n++) {
    let px = 1 + rand() * (w - 3), pz = 1 + rand() * (d - 3);
    if (wt[Math.floor(pz) * w + Math.floor(px)] <= 0) continue;
    let dirX = 0, dirZ = 0, speed = 1, water = 1, sediment = 0;
    for (let step = 0; step < life; step++) {
      const g = gradient(px, pz);
      dirX = dirX * inertia - g.gx * (1 - inertia); dirZ = dirZ * inertia - g.gz * (1 - inertia);
      const len = Math.hypot(dirX, dirZ);
      if (len < 1e-6) break;
      dirX /= len; dirZ /= len;
      const nx = px + dirX, nz = pz + dirZ;
      if (nx < 1 || nz < 1 || nx >= w - 2 || nz >= d - 2) break;
      const dh = gradient(nx, nz).h - g.h;
      const capacity = Math.max(-dh, minSlope) * speed * water * capacityK;
      if (sediment > capacity || dh > 0) {
        // Uphill or full: drop sediment (enough to fill a pit going uphill).
        const amount = dh > 0 ? Math.min(dh, sediment) : (sediment - capacity) * deposit;
        sediment -= amount; change(px, pz, amount);
      } else {
        const amount = Math.min((capacity - sediment) * erode, -dh);
        sediment += amount; change(px, pz, -amount);
      }
      speed = Math.sqrt(Math.max(0, speed * speed + dh * gravity));
      water *= 1 - evaporate;
      px = nx; pz = nz;
    }
  }
}

export function createErosion(ed) {
  const { $, W, H } = ed;
  const panel = document.createElement('div');
  panel.id = 'erodePanel';
  panel.innerHTML = `
    <div class="seg" id="erModes"><button data-e="thermal" class="on" title="Steep ground slides down to its resting angle">Thermal</button><button data-e="water" title="Rain runs downhill, cutting gullies and filling hollows">Water</button></div>
    <label class="field" id="erTalusRow">Rest angle <input id="erTalus" type="range" min="10" max="60" step="1" value="33"><span id="erTalusV"></span></label>
    <div class="hint">Hold and drag over the ground; Strength sets how fast it weathers. The Mask applies.</div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['erode'] });
  let mode = 'thermal';
  const sync = () => { $('erTalusV').textContent = `${$('erTalus').value}°`; $('erTalusRow').hidden = mode !== 'thermal'; $('erModes').querySelectorAll('[data-e]').forEach(b => b.classList.toggle('on', b.dataset.e === mode)); };
  $('erModes').querySelectorAll('[data-e]').forEach(b => b.onclick = () => { mode = b.dataset.e; sync(); });
  $('erTalus').addEventListener('input', sync);
  sync();

  // Copies the heights of a window into a local array, runs fn on it, and writes back what changed.
  function run(x0, z0, x1, z1, weightOf, fn, touched) {
    x0 = Math.max(0, x0); z0 = Math.max(0, z0); x1 = Math.min(W - 1, x1); z1 = Math.min(H - 1, z1);
    const w = x1 - x0 + 1, d = z1 - z0 + 1, h = new Float32Array(w * d), wt = new Float32Array(w * d);
    for (let z = 0; z < d; z++) for (let x = 0; x < w; x++) {
      const gx = x0 + x, gz = z0 + z, g = gz * W + gx;
      h[z * w + x] = ed.height(g);
      wt[z * w + x] = ed.locked(gx, gz) ? 0 : weightOf(gx, gz, g);
    }
    const before = h.slice();
    fn(h, wt, w, d);
    for (let i = 0; i < h.length; i++) {
      if (Math.abs(h[i] - before[i]) < 1e-5) continue;
      const g = (z0 + Math.floor(i / w)) * W + x0 + i % w;
      ed.setHeight(g, h[i]);
      touched.add(g);
    }
    return { x0, z0, x1, z1 };
  }
  const talus = () => Math.tan(+$('erTalus').value * Math.PI / 180);
  // One frame of the brush.
  function brushStep(hover, r, rate, touched) {
    const reach = ed.brush.reach(r) + 2;
    const box = run(Math.floor(hover.gx - reach), Math.floor(hover.gz - reach), Math.ceil(hover.gx + reach), Math.ceil(hover.gz + reach),
      (gx, gz, g) => ed.brush.weight(gx - hover.gx, gz - hover.gz, r, gx, gz) * ed.mask(g),
      (h, wt, w, d) => mode === 'thermal' ? thermal(h, wt, w, d, talus(), Math.min(1, rate * 12), 2) : hydraulic(h, wt, w, d, Math.ceil(r * r * rate * 6), 1),
      touched);
    ed.refresh(box.x0 - 1, box.z0 - 1, box.x1 + 1, box.z1 + 1);
  }
  // The Area tool's Erode: both kinds over the whole selection, in one go.
  function areaErode(a, touched) {
    const weights = new Map(a.cells);
    return run(a.x0 - 1, a.z0 - 1, a.x1 + 1, a.z1 + 1, (gx, gz, g) => weights.get(g) ?? 0, (h, wt, w, d) => {
      thermal(h, wt, w, d, talus(), 0.8, 12);
      hydraulic(h, wt, w, d, Math.ceil(w * d * 0.6), 1);
      thermal(h, wt, w, d, talus(), 0.5, 4);
    }, touched);
  }
  ed.erosion = { brushStep, areaErode, get mode() { return mode; } };
}
