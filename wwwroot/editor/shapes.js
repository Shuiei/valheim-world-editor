// Shape generator (WorldEdit's //generate, Axiom's shape tool): a click puts a shape into the ground:
// a mound, cone, mesa, crater, moat or bowl, or any formula of x, z (metres from the click, east and
// north), d (distance from it), r (radius), h (height) and n(x, z) (smooth noise, -1 to 1).
// Every preset is a formula too, shown in the box, so it can be changed.

export const PRESETS = {
  mound: ['Mound', 'h * smooth(1 - d / r)'],
  cone: ['Cone', 'h * max(0, 1 - d / r)'],
  mesa: ['Mesa', 'h * smooth((r - d) / (0.35 * r))'],
  crater: ['Crater', 'd < r ? -h * max(0, 1 - (d / (0.62 * r)) ^ 2) + 0.35 * h * bell((d - 0.72 * r) / (0.16 * r)) * smooth((r - d) / (0.1 * r)) : 0'],
  moat: ['Moat (ring ditch)', '-h * bell((d - 0.75 * r) / (0.12 * r))'],
  bowl: ['Bowl', '-h * smooth(1 - (d / r) ^ 2)'],
  ridges: ['Ridged hill', 'h * smooth(1 - d / r) * (0.75 + 0.25 * abs(sin(x / 3 + n(x / 9, z / 9) * 2)))'],
};

const FUNCS = {
  sin: Math.sin, cos: Math.cos, tan: Math.tan, abs: Math.abs, sqrt: v => Math.sqrt(Math.max(0, v)), exp: Math.exp, log: v => Math.log(Math.max(1e-9, v)),
  floor: Math.floor, ceil: Math.ceil, round: Math.round, min: Math.min, max: Math.max, pow: Math.pow, atan2: Math.atan2, sign: Math.sign,
  clamp: (v, a, b) => Math.max(a, Math.min(b, v)),
  smooth: t => { t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t); },
  bell: t => Math.exp(-t * t * 2.5),
};

// A small, safe formula language: numbers, the variables, + - * / % ^, comparisons, a ? b : c and the
// functions above. Compiled into a function of an environment; throws an Error with a plain message.
export function compile(src, names) {
  const toks = [];
  const re = /\s*(?:(\d+\.?\d*(?:e[+-]?\d+)?|\.\d+)|([A-Za-z_]\w*)|(<=|>=|==|!=|&&|\|\||[-+*/%^(),<>?:!]))/y;
  let m, pos = 0;
  src = src.trim();
  while (pos < src.length) {
    re.lastIndex = pos;
    if (!(m = re.exec(src)) || m[0].length === 0) throw new Error(`I do not understand “${src.slice(pos).trim().slice(0, 12)}”`);
    toks.push(m[1] != null ? { n: +m[1] } : m[2] != null ? { id: m[2] } : { op: m[3] });
    pos = re.lastIndex;
  }
  let i = 0;
  const peek = () => toks[i], eat = op => { if (toks[i]?.op === op) { i++; return true; } return false; };
  const need = op => { if (!eat(op)) throw new Error(`“${op}” expected${toks[i] ? ` before “${toks[i].op ?? toks[i].id ?? toks[i].n}”` : ' at the end'}`); };
  function primary() {
    const t = toks[i++];
    if (!t) throw new Error('the formula ends too early');
    if (t.n != null) return () => t.n;
    if (t.op === '(') { const e = ternary(); need(')'); return e; }
    if (t.op === '-') { const e = power(); return env => -e(env); }
    if (t.op === '!') { const e = power(); return env => e(env) ? 0 : 1; }
    if (t.id != null) {
      if (eat('(')) {
        const args = [];
        if (!eat(')')) { do args.push(ternary()); while (eat(',')); need(')'); }
        if (t.id === 'n') { if (args.length !== 2) throw new Error('n(x, z) takes two numbers'); return env => env.n(args[0](env), args[1](env)); }
        const f = FUNCS[t.id];
        if (!f) throw new Error(`there is no function “${t.id}”`);
        return env => f(...args.map(a => a(env)));
      }
      if (t.id === 'pi') return () => Math.PI;
      if (!names.includes(t.id)) throw new Error(`there is no “${t.id}” (you can use ${names.join(', ')}, pi)`);
      return env => env[t.id];
    }
    throw new Error(`“${t.op}” is out of place`);
  }
  function power() { const a = primary(); if (eat('^')) { const b = power(); return env => Math.pow(a(env), b(env)); } return a; }
  function product() { let a = power(); for (;;) { const op = peek()?.op; if (op !== '*' && op !== '/' && op !== '%') return a; i++; const b = power(), l = a; a = op === '*' ? env => l(env) * b(env) : op === '/' ? env => l(env) / b(env) : env => l(env) % b(env); } }
  function sum() { let a = product(); for (;;) { const op = peek()?.op; if (op !== '+' && op !== '-') return a; i++; const b = product(), l = a; a = op === '+' ? env => l(env) + b(env) : env => l(env) - b(env); } }
  function compare() {
    let a = sum();
    for (;;) {
      const op = peek()?.op; if (!['<', '>', '<=', '>=', '==', '!='].includes(op)) return a;
      i++; const b = sum(), l = a;
      a = { '<': env => +(l(env) < b(env)), '>': env => +(l(env) > b(env)), '<=': env => +(l(env) <= b(env)), '>=': env => +(l(env) >= b(env)), '==': env => +(l(env) === b(env)), '!=': env => +(l(env) !== b(env)) }[op];
    }
  }
  function logic() { let a = compare(); for (;;) { const op = peek()?.op; if (op !== '&&' && op !== '||') return a; i++; const b = compare(), l = a; a = op === '&&' ? env => +(l(env) && b(env)) : env => +(l(env) || b(env)); } }
  function ternary() { const c = logic(); if (eat('?')) { const a = ternary(); need(':'); const b = ternary(); return env => c(env) ? a(env) : b(env); } return c; }
  if (!toks.length) throw new Error('the formula is empty');
  const f = ternary();
  if (i < toks.length) throw new Error(`“${toks[i].op ?? toks[i].id ?? toks[i].n}” is out of place`);
  return f;
}

export function createShapes(ed) {
  const { THREE, $, W, H } = ed;
  const panel = document.createElement('div');
  panel.id = 'shapePanel';
  panel.innerHTML = `
    <label class="field">Shape <select id="shPreset">${Object.entries(PRESETS).map(([k, [n]]) => `<option value="${k}">${n}</option>`).join('')}<option value="custom">Formula…</option></select></label>
    <label class="field">Radius <input id="shRadius" type="range" min="2" max="80" step="1" value="16"><span id="shRadiusV"></span></label>
    <label class="field">Height <input id="shHeight" type="number" step="0.5" value="5"><span>m</span></label>
    <label style="display:block;color:var(--muted);margin:7px 0 3px">Formula<textarea id="shExpr" rows="3" spellcheck="false" style="margin-top:3px"></textarea></label>
    <div class="hint" id="shErr"></div>
    <div class="hint">Click the ground to put the shape there: the formula gives how many metres to add to the ground at each point (negative digs). x and z are metres east and north of the click, d the distance from it, r the radius, h the height, n(x, z) smooth noise from -1 to 1. Functions: smooth, bell, sin, cos, abs, sqrt, min, max, pow, clamp, exp, floor; a ? b : c. Only points within the radius change; the Mask and the ±8 m limit apply.</div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['shape'] });
  const style = document.createElement('style');
  style.textContent = `#shExpr { width: 100%; background: var(--panel2); color: var(--text); border: 1px solid var(--line); border-radius: 6px; padding: 4px 6px; font: 12px/1.4 ui-monospace, monospace; resize: vertical; } #shErr { color: var(--warn); }`;
  document.head.appendChild(style);
  const pref = (k, d) => { try { return localStorage.getItem(k) ?? d; } catch { return d; } };
  $('shPreset').value = pref('shapePreset', 'mound');
  if (!$('shPreset').value) $('shPreset').value = 'mound';
  $('shExpr').value = $('shPreset').value === 'custom' ? pref('shapeFormula', PRESETS.mound[1]) : PRESETS[$('shPreset').value][1];
  let fn = null;
  function sync() {
    $('shRadiusV').textContent = `${$('shRadius').value} m`;
    try { fn = compile($('shExpr').value, ['x', 'z', 'd', 'r', 'h']); $('shErr').textContent = ''; } catch (err) { fn = null; $('shErr').textContent = `Formula: ${err.message}.`; }
    try { localStorage.setItem('shapePreset', $('shPreset').value); if ($('shPreset').value === 'custom') localStorage.setItem('shapeFormula', $('shExpr').value); } catch { }
    drawOutline();
  }
  $('shPreset').addEventListener('input', () => { if ($('shPreset').value !== 'custom') $('shExpr').value = PRESETS[$('shPreset').value][1]; sync(); });
  $('shExpr').addEventListener('input', () => { const p = $('shPreset').value; if (p !== 'custom' && $('shExpr').value !== PRESETS[p][1]) $('shPreset').value = 'custom'; sync(); });
  ['shRadius', 'shHeight'].forEach(id => $(id).addEventListener('input', sync));

  const outline = new THREE.LineLoop(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xffd27a, depthTest: false }));
  outline.renderOrder = 10; outline.frustumCulled = false; ed.scene.add(outline);
  let at = null;
  function drawOutline() {
    outline.visible = ed.tool === 'shape' && !!at;
    if (!outline.visible) return;
    const r = +$('shRadius').value, pts = [];
    for (let k = 0; k < 96; k++) { const a = k / 96 * Math.PI * 2, gx = at.gx + Math.cos(a) * r, gz = at.gz + Math.sin(a) * r; pts.push(new THREE.Vector3(gx - ed.cx, ed.sampleHeight(gx, gz) + 0.3, -(gz - ed.cz))); }
    outline.geometry.setFromPoints(pts);
  }
  // The noise of the Naturalize tool, in world coordinates, so shapes match their surroundings.
  const env = { n: (x, z) => ed.noise2 ? ed.noise2(x, z) : ed.fbm(x, z) / 1.6 };
  function apply(hit) {
    if (!fn) { ed.msg('Fix the formula first.', true); return; }
    const r = +$('shRadius').value, hgt = +$('shHeight').value;
    const state = ed.snapshotState(), touched = new Set();
    const x0 = Math.max(1, Math.floor(hit.gx - r)), x1 = Math.min(W - 2, Math.ceil(hit.gx + r)), z0 = Math.max(1, Math.floor(hit.gz - r)), z1 = Math.min(H - 2, Math.ceil(hit.gz + r));
    let clamped = false, bad = 0;
    for (let gz = z0; gz <= z1; gz++) for (let gx = x0; gx <= x1; gx++) {
      const x = gx - hit.gx, z = gz - hit.gz, d = Math.hypot(x, z);
      if (d > r || ed.locked(gx, gz)) continue;
      const g = gz * W + gx, w = ed.mask(g);
      if (w <= 0) continue;
      Object.assign(env, { x, z, d, r, h: hgt });
      let v;
      try { v = fn(env); } catch { v = NaN; }
      if (!Number.isFinite(v)) { bad++; continue; }
      if (!v) continue;
      const h = ed.height(g), want = h + v * w;
      ed.setHeight(g, want);
      if (Math.abs(ed.height(g) - want) > 1e-3) clamped = true;
      touched.add(g);
    }
    ed.refresh(x0 - 1, z0 - 1, x1 + 1, z1 + 1);
    const zones = ed.zonesOf(touched);
    if (zones.length) { ed.upload(zones); ed.pushHistory({ state, zones, label: `Shape: ${$('shPreset').selectedOptions[0].text.replace('…', '')}` }); }
    ed.msg(!touched.size ? 'The formula gives 0 everywhere within the radius: nothing changed.' + (bad ? ` (${bad} point(s) gave no number.)` : '')
      : clamped ? 'Placed, but part of the shape reached the game limit of ±8 m from the original ground (red points).' : `Placed the shape on ${touched.size} point(s). Ctrl+Z undoes it.`, clamped || !touched.size);
  }
  ed.handlers.shape = {
    down(e, hit) { if (hit) apply(hit); },
    move(e, hit) { at = hit; ed.showStatusFor?.(hit); drawOutline(); },
  };
  ed.onToolChange.push(() => drawOutline());
  sync();
}
