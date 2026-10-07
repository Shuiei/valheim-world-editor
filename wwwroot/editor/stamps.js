// Stamps (WorldPainter's custom brushes, Axiom's stamp tool): a picture as the brush shape. White
// works fully, black not at all. A few are built in (mountain, mesa, crater rim...); any grayscale
// picture can be loaded. With "Stamp once", a click of Raise or Lower puts the whole picture into the
// ground at once, to a chosen height.

const SIZE = 128;

// Built-in stamps, made from a formula (u, v from -1 to 1, north up) into a picture.
function hash(x, y) { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); }
function vnoise(x, y) {
  const X = Math.floor(x), Y = Math.floor(y), fx = x - X, fy = y - Y, sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
  const a = hash(X, Y), b = hash(X + 1, Y), c = hash(X, Y + 1), d = hash(X + 1, Y + 1);
  return (a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy;
}
const fractal = (x, y) => { let s = 0, a = 0.5; for (let o = 0; o < 4; o++) { s += vnoise(x, y) * a; x *= 2.03; y *= 2.03; a *= 0.5; } return s / 0.9375; };
const BUILT_IN = {
  mountain: ['Mountain', (u, v) => { const d = Math.hypot(u, v); return d >= 1 ? 0 : Math.pow(1 - d, 1.6) * (0.75 + 0.5 * fractal(u * 4 + 7, v * 4 + 3)); }],
  mesa: ['Mesa (flat top)', (u, v) => { const d = Math.hypot(u, v) * (1 + 0.12 * (fractal(u * 3, v * 3) - 0.5)); return d >= 1 ? 0 : d < 0.62 ? 1 : 1 - Math.pow((d - 0.62) / 0.38, 0.7); }],
  crater: ['Crater rim', (u, v) => { const d = Math.hypot(u, v); return d >= 1 ? 0 : Math.max(0, 1 - Math.abs(d - 0.72) / 0.28) ** 1.5; }],
  dunes: ['Dunes', (u, v) => { const d = Math.hypot(u, v); if (d >= 1) return 0; const edge = Math.min(1, (1 - d) * 3); return edge * (0.5 + 0.5 * Math.sin(u * 9 + fractal(u * 2, v * 2) * 3)) ** 2; }],
  rocky: ['Rocky ground', (u, v) => { const d = Math.hypot(u, v); if (d >= 1) return 0; return Math.min(1, (1 - d) * 2.5) * Math.max(0, fractal(u * 6 + 11, v * 6 - 5) * 1.6 - 0.45); }],
};
function render(f) {
  const data = new Float32Array(SIZE * SIZE);
  for (let y = 0; y < SIZE; y++) for (let x = 0; x < SIZE; x++) data[y * SIZE + x] = Math.max(0, Math.min(1, f(x / (SIZE - 1) * 2 - 1, 1 - y / (SIZE - 1) * 2)));
  return data;
}
// A picture (any size) to SIZE x SIZE weights: brightness times opacity.
function fromImage(img) {
  const c = document.createElement('canvas'); c.width = c.height = SIZE;
  const g = c.getContext('2d'); g.drawImage(img, 0, 0, SIZE, SIZE);
  const px = g.getImageData(0, 0, SIZE, SIZE).data, data = new Float32Array(SIZE * SIZE);
  for (let i = 0; i < SIZE * SIZE; i++) data[i] = (0.299 * px[i * 4] + 0.587 * px[i * 4 + 1] + 0.114 * px[i * 4 + 2]) / 255 * (px[i * 4 + 3] / 255);
  return { data, url: c.toDataURL('image/png') };
}
// Weight at (u, v) in -1..1 (north up), between the four nearest pixels.
function sample(data, u, v) {
  if (u <= -1 || u >= 1 || v <= -1 || v >= 1) return 0;
  const x = (u + 1) / 2 * (SIZE - 1), y = (1 - (v + 1) / 2) * (SIZE - 1), x0 = Math.floor(x), y0 = Math.floor(y), tx = x - x0, ty = y - y0;
  const at = (a, b) => data[Math.min(SIZE - 1, b) * SIZE + Math.min(SIZE - 1, a)];
  return (at(x0, y0) * (1 - tx) + at(x0 + 1, y0) * tx) * (1 - ty) + (at(x0, y0 + 1) * (1 - tx) + at(x0 + 1, y0 + 1) * tx) * ty;
}

export function createStamps(ed) {
  const { $ } = ed;
  const box = document.createElement('div');
  box.id = 'stampBox';
  box.innerHTML = `
    <div class="row" style="margin-top:2px"><button id="stLoad" title="Use a grayscale picture as the brush shape">Load stamp…</button><button id="stRemove" title="Forget the loaded picture chosen as Shape">Forget stamp</button></div>
    <input id="stFile" type="file" accept="image/*" hidden>
    <div id="stOnceBox"><label class="check"><input type="checkbox" id="stOnce"> Stamp once: a click puts the whole stamp in</label>
      <label class="field" id="stHeightRow">Height <input id="stHeight" type="number" step="0.5" value="4"><span>m</span></label></div>`;
  $('brushShape').appendChild(box);
  const loaded = new Map();   // shape name -> { label, url }
  const STORE = 'brushStamps';
  function add(name, label, data, turnable = true) { ed.brush.addShape(name, label, { weight: (u, v) => sample(data, u, v), turnable, data }); }
  for (const [key, [label, f]] of Object.entries(BUILT_IN)) add(`stamp:${key}`, `Stamp: ${label}`, render(f));
  // Pictures loaded before, kept in this browser.
  function store() { try { localStorage.setItem(STORE, JSON.stringify([...loaded].map(([name, v]) => ({ name, label: v.label, url: v.url })))); } catch { ed.msg('This browser could not keep the stamp for next time.', true); } }
  async function addPicture(name, label, url) {
    const img = new Image(); img.src = url;
    await img.decode();
    const { data, url: small } = fromImage(img);
    loaded.set(name, { label, url: small });
    add(name, `Stamp: ${label}`, data);
  }
  let saved = [];
  try { saved = JSON.parse(localStorage.getItem(STORE) ?? '[]'); } catch { }
  for (const s of saved) addPicture(s.name, s.label, s.url).catch(() => { });
  $('stLoad').onclick = () => $('stFile').click();
  $('stFile').onchange = async () => {
    const f = $('stFile').files[0]; $('stFile').value = '';
    if (!f) return;
    const url = await new Promise(r => { const fr = new FileReader(); fr.onload = () => r(fr.result); fr.readAsDataURL(f); });
    const name = `pic:${Date.now()}`, label = f.name.replace(/\.[^.]+$/, '');
    try { await addPicture(name, label, url); } catch { ed.msg('That picture could not be read.', true); return; }
    store();
    ed.brush.shape = name;
    ed.msg(`Loaded the stamp “${label}”: white parts work fully, black parts not at all.`);
  };
  $('stRemove').onclick = () => {
    const name = ed.brush.shape;
    if (!loaded.has(name)) { ed.msg('Choose a loaded picture as Shape first (built-in stamps stay).', true); return; }
    loaded.delete(name); ed.brush.removeShape(name); store();
  };
  const isStamp = () => ed.brush.shape.startsWith('stamp:') || ed.brush.shape.startsWith('pic:');
  function sync() {
    const once = isStamp() && (ed.tool === 'raise' || ed.tool === 'lower');
    $('stOnceBox').hidden = !once;
    $('stHeightRow').hidden = !$('stOnce').checked;
    $('stRemove').hidden = !loaded.has(ed.brush.shape);
    box.hidden = ed.tool === 'plant';
  }
  $('bShape').addEventListener('input', sync); $('stOnce').addEventListener('input', sync);
  ed.onToolChange.push(sync);
  sync();

  // Stamp once: a click of Raise (or Lower) moves the ground under the stamp by Height times the picture.
  // One undo step; the ±8 m limit and the Mask apply.
  ed.stampOnce = hit => {
    if (!$('stOnce').checked || !isStamp() || !(ed.tool === 'raise' || ed.tool === 'lower')) return false;
    const r = ed.radius, reach = ed.brush.reach(r), amount = (+$('stHeight').value || 0) * (ed.tool === 'lower' ? -1 : 1);
    const state = ed.snapshotState(), touched = new Set();
    const x0 = Math.max(1, Math.floor(hit.gx - reach)), x1 = Math.min(ed.W - 2, Math.ceil(hit.gx + reach));
    const z0 = Math.max(1, Math.floor(hit.gz - reach)), z1 = Math.min(ed.H - 2, Math.ceil(hit.gz + reach));
    let clamped = false;
    for (let gz = z0; gz <= z1; gz++) for (let gx = x0; gx <= x1; gx++) {
      if (ed.locked(gx, gz)) continue;
      const g = gz * ed.W + gx, w = ed.brush.weight(gx - hit.gx, gz - hit.gz, r, gx, gz) * ed.mask(g);
      if (w <= 0) continue;
      const h = ed.height(g), want = h + amount * w;
      ed.setHeight(g, want);
      if (Math.abs(ed.height(g) - want) > 1e-3) clamped = true;
      touched.add(g);
    }
    ed.refresh(x0 - 1, z0 - 1, x1 + 1, z1 + 1);
    const zones = ed.zonesOf(touched);
    if (zones.length) { ed.upload(zones); ed.pushHistory({ state, zones, label: `Stamp: ${$('bShape').selectedOptions[0].text.replace(/^Stamp: /, '')}` }); }
    ed.msg(clamped ? 'Stamped, but part of it reached the game limit of ±8 m from the original ground (red points).' : `Stamped ${Math.abs(amount)} m ${amount >= 0 ? 'up' : 'down'}. Ctrl+Z undoes it.`, clamped);
    return true;
  };
}
