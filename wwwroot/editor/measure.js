// Measuring: a tape between two points (distance, height difference, slope), and View overlays that
// colour the ground by steepness or draw height lines.

export function createMeasure(ed) {
  const { THREE, $, W, H } = ed;
  const panel = document.createElement('div');
  panel.id = 'measurePanel';
  panel.innerHTML = `
    <div class="hint" style="margin-top:0" id="msHint">Click a first point on the ground.</div>
    <dl class="meas" id="msOut"></dl>
    <div class="row"><button id="msClear">Clear <kbd>Esc</kbd></button></div>
    <div class="hint">Slope colours and height lines are in View → Look.</div>`;
  $('locWarn').before(panel);
  ed.panels.push({ el: panel, tools: ['measure'] });
  const style = document.createElement('style');
  style.textContent = `.meas { display: grid; grid-template-columns: auto 1fr; gap: 3px 12px; margin: 6px 0; font-variant-numeric: tabular-nums; }
    .meas dt { color: var(--muted); } .meas dd { margin: 0; text-align: right; }`;
  document.head.appendChild(style);

  // Overlay switches in the View panel's Look section.
  const seeThrough = $('seeThrough').closest('label');
  const ov = document.createElement('div');
  ov.innerHTML = `
    <label class="toggle"><input type="checkbox" id="ovSlope"><span class="t">Slope colours</span><span class="meta">needs Game look</span></label>
    <label class="toggle"><input type="checkbox" id="ovContour"><span class="t">Height lines every</span><select id="ovStep" style="width:64px"><option>1</option><option selected>2</option><option>5</option><option>10</option></select><span class="meta">m</span></label>`;
  seeThrough.after(ov);
  const pref = (k, d) => { try { return localStorage.getItem(k) ?? d; } catch { return d; } };
  const setPref = (k, v) => { try { localStorage.setItem(k, v); } catch { } };
  $('ovSlope').checked = pref('ovSlope', '0') === '1'; $('ovContour').checked = pref('ovContour', '0') === '1'; $('ovStep').value = pref('ovStep', '2');
  function syncOverlay() {
    setPref('ovSlope', $('ovSlope').checked ? '1' : '0'); setPref('ovContour', $('ovContour').checked ? '1' : '0'); setPref('ovStep', $('ovStep').value);
    ed.getLook?.()?.setOverlay({ slope: $('ovSlope').checked, contour: $('ovContour').checked ? +$('ovStep').value : 0 });
  }
  ['ovSlope', 'ovContour', 'ovStep'].forEach(id => $(id).addEventListener('change', syncOverlay));
  ed.onLook = syncOverlay;

  // Tape.
  let a = null, b = null, fixed = false;
  const line = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xffe08a, depthTest: false }));
  line.renderOrder = 18; ed.scene.add(line);
  line.frustumCulled = false;
  const dots = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color: 0xffe08a, size: 9, sizeAttenuation: false, depthTest: false }));
  dots.renderOrder = 19; ed.scene.add(dots);
  dots.frustumCulled = false;
  const v3 = p => new THREE.Vector3(p.gx - ed.cx, p.y + 0.2, -(p.gz - ed.cz));
  function draw() {
    const pts = [a, b].filter(Boolean);
    dots.geometry.setFromPoints(pts.map(v3));
    if (a && b) {
      // Follow the ground between the points, so the line shows the profile.
      const n = Math.max(2, Math.ceil(Math.hypot(b.gx - a.gx, b.gz - a.gz))), p = [];
      for (let i = 0; i <= n; i++) { const gx = a.gx + (b.gx - a.gx) * i / n, gz = a.gz + (b.gz - a.gz) * i / n; p.push(v3({ gx, gz, y: ed.sampleHeight(gx, gz) })); }
      line.geometry.setFromPoints(p);
    } else line.geometry.setFromPoints([]);
    const vis = ed.tool === 'measure' || fixed;
    line.visible = dots.visible = vis;
    if (!a) { $('msHint').textContent = 'Click a first point on the ground.'; $('msOut').innerHTML = ''; return; }
    if (!b) { $('msHint').textContent = 'Click a second point.'; return; }
    const flat = Math.hypot(b.gx - a.gx, b.gz - a.gz), dh = b.y - a.y, d3 = Math.hypot(flat, dh);
    let lo = Infinity, hi = -Infinity;
    const n = Math.max(2, Math.ceil(flat));
    for (let i = 0; i <= n; i++) { const h = ed.sampleHeight(a.gx + (b.gx - a.gx) * i / n, a.gz + (b.gz - a.gz) * i / n); lo = Math.min(lo, h); hi = Math.max(hi, h); }
    $('msHint').textContent = fixed ? 'Click again to start a new measurement.' : 'Click to fix the second point.';
    $('msOut').innerHTML = `
      <dt>Distance</dt><dd>${flat.toFixed(1)} m</dd>
      <dt>Along the slope</dt><dd>${d3.toFixed(1)} m</dd>
      <dt>Height A → B</dt><dd>${a.y.toFixed(1)} → ${b.y.toFixed(1)} m (${dh >= 0 ? '+' : ''}${dh.toFixed(1)})</dd>
      <dt>Slope</dt><dd>${flat > 0.01 ? (dh / flat * 100).toFixed(0) : '–'} % · ${(Math.atan2(Math.abs(dh), flat) * 180 / Math.PI).toFixed(1)}°</dd>
      <dt>Lowest / highest</dt><dd>${lo.toFixed(1)} / ${hi.toFixed(1)} m</dd>
      <dt>Water depth</dt><dd>${Math.min(a.y, b.y) < ed.WATER ? (ed.WATER - Math.min(a.y, b.y)).toFixed(1) + ' m' : 'above sea'}</dd>`;
  }
  const at = hit => ({ gx: hit.gx, gz: hit.gz, y: ed.sampleHeight(hit.gx, hit.gz) });
  ed.handlers.measure = {
    down(e, hit) {
      if (!hit) return;
      if (!a || fixed) { a = at(hit); b = null; fixed = false; }
      else { b = at(hit); fixed = true; }
      draw();
    },
    move(e, hit) { ed.showStatusFor?.(hit); if (a && !fixed && hit) { b = at(hit); draw(); } },
    key(e) { if (e.key === 'Escape') { a = b = null; fixed = false; draw(); return true; } return false; }
  };
  $('msClear').onclick = () => { a = b = null; fixed = false; draw(); };
  ed.onToolChange.push(() => draw());
  draw();
}
