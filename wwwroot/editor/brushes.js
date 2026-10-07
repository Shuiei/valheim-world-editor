// Brush shapes and falloff (WorldPainter's brushes): the shape of the sculpt and paint brushes (circle,
// square, ring, ragged noise, or a picture: see stamps) and how their effect fades towards the edge.
// Every sculpt and paint tool asks ed.brush for the weight of a point.

// Falloff: t is 1 at the middle of the brush and 0 at its edge.
const FALLOFF = {
  smooth: t => t * t * (3 - 2 * t),
  linear: t => t,
  dome: t => Math.sqrt(Math.max(0, 1 - (1 - t) * (1 - t))),
  flat: t => t >= 0.2 ? 1 : (t / 0.2) * (t / 0.2) * (3 - 2 * t / 0.2),
  peak: t => t * t * t,
};

export function createBrushes(ed) {
  const { $ } = ed;
  const box = document.createElement('div');
  box.id = 'brushShape';
  box.innerHTML = `
    <label class="field">Shape <select id="bShape">
      <option value="circle">Circle</option><option value="square">Square</option><option value="ring">Ring</option><option value="noise">Ragged (noise)</option>
    </select></label>
    <label class="field">Falloff <select id="bFalloff">
      <option value="smooth">Smooth</option><option value="linear">Linear</option><option value="dome">Dome</option><option value="flat">Flat top</option><option value="peak">Peak</option>
    </select></label>
    <label class="field" id="bTurnRow">Turn <input id="bTurn" type="range" min="-180" max="180" step="1" value="0"><span id="bTurnV"></span></label>`;
  document.querySelector('[data-for-tools="brush"]').appendChild(box);
  const pref = (k, d) => { try { return localStorage.getItem(k) ?? d; } catch { return d; } };
  // The shape chosen last; a stamp is only known once its module adds it (addShape picks it then).
  const wanted = pref('brushShape', 'circle');
  $('bShape').value = wanted; $('bFalloff').value = pref('brushFalloff', 'smooth');
  if (!$('bShape').value) $('bShape').value = 'circle';
  if (!$('bFalloff').value) $('bFalloff').value = 'smooth';
  // Shapes added by other modules (stamps): name -> { label, weight(u, v) for u, v in -1..1, turnable }.
  const extra = {};
  const turnable = () => $('bShape').value === 'square' || !!extra[$('bShape').value]?.turnable;
  function sync() {
    try { localStorage.setItem('brushShape', $('bShape').value); localStorage.setItem('brushFalloff', $('bFalloff').value); } catch { }
    $('bTurnRow').hidden = !turnable() || ed.tool === 'plant';
    $('bTurnV').textContent = `${$('bTurn').value}°`;
    // A picture has its own falloff.
    $('bFalloff').closest('label').hidden = !!extra[$('bShape').value] || ed.tool === 'plant';
    $('bShape').closest('label').hidden = ed.tool === 'plant';
    ed.updateRing?.();
  }
  ['bShape', 'bFalloff', 'bTurn'].forEach(id => $(id).addEventListener('input', sync));
  ed.onToolChange.push(sync);

  // The point (dx, dz) from the brush middle, in the brush's own turned frame and scaled to -1..1.
  function local(dx, dz, r) {
    const a = -(+$('bTurn').value) * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
    return turnable() ? [(dx * c - dz * s) / r, (dx * s + dz * c) / r] : [dx / r, dz / r];
  }
  // Weight of a point of the brush (0..1), before the mask. gx, gz: grid point, for the noise.
  function weight(dx, dz, r, gx = 0, gz = 0) {
    const shape = $('bShape').value, [u, v] = local(dx, dz, r);
    if (extra[shape]) return extra[shape].weight(u, v);
    let d;
    switch (shape) {
      case 'square': d = Math.max(Math.abs(u), Math.abs(v)); break;
      case 'ring': d = Math.abs(Math.hypot(u, v) - 0.7) / 0.3; break;
      case 'noise': d = Math.hypot(u, v) * (1 + 0.35 * ed.fbm((ed.originX + gx) / Math.max(2, r * 0.45), (ed.originZ + gz) / Math.max(2, r * 0.45))); break;
      default: d = Math.hypot(u, v);
    }
    if (d >= 1) return 0;
    return FALLOFF[$('bFalloff').value](1 - d);
  }
  // How far from the middle a point can be affected (a turned square reaches its corners).
  const reach = r => (turnable() || $('bShape').value === 'noise') ? r * 1.42 : r;
  // Outline drawn on the ground: n points around the brush, as offsets from its middle.
  function outline(r, n) {
    const shape = $('bShape').value, a = +$('bTurn').value * Math.PI / 180, out = [];
    for (let i = 0; i < n; i++) {
      const t = i / n * Math.PI * 2;
      let x = Math.cos(t), z = Math.sin(t);
      if (shape === 'square' || (extra[shape] && turnable())) { const m = Math.max(Math.abs(x), Math.abs(z)); x /= m; z /= m; }
      if (turnable()) [x, z] = [x * Math.cos(a) - z * Math.sin(a), x * Math.sin(a) + z * Math.cos(a)];
      out.push([x * r, z * r]);
    }
    return out;
  }
  // The Ring shape works on a band from 40% of the radius to the edge: its inner edge, or null.
  const innerOutline = (r, n) => $('bShape').value === 'ring' ? Array.from({ length: n }, (_, i) => { const t = i / n * Math.PI * 2; return [Math.cos(t) * r * 0.4, Math.sin(t) * r * 0.4]; }) : null;
  function turn(deg) {
    let t = +$('bTurn').value + deg; t = ((t + 180) % 360 + 360) % 360 - 180;
    $('bTurn').value = t; sync();
  }
  // , . turn square brushes and pictures (Shift: 15°).
  (ed.keys ??= []).push(e => {
    if (!ed.isBrushTool?.(ed.tool) || !turnable()) return false;
    if (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>') { turn((e.key === ',' || e.key === '<' ? -1 : 1) * ed.turnStep(e)); return true; }
    return false;
  });
  function addShape(name, label, def) {
    extra[name] = def;
    if (![...$('bShape').options].some(o => o.value === name)) $('bShape').add(new Option(label, name));
    if (name === wanted) $('bShape').value = name;
    sync();
  }
  function removeShape(name) {
    delete extra[name];
    [...$('bShape').options].find(o => o.value === name)?.remove();
    if (!$('bShape').value) $('bShape').value = 'circle';
    sync();
  }
  ed.brush = { weight, reach, outline, innerOutline, addShape, removeShape, sync, get shape() { return $('bShape').value; }, set shape(v) { $('bShape').value = v; sync(); } };
  sync();
  return ed.brush;
}
