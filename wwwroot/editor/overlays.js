// Overlays (View panel): the ground wards protect, the range crafting stations let you build in,
// and the ground locations flatten, drawn as rings on the ground. In live mode, Go to jumps to a
// player (moving the area when they are elsewhere).

export function createOverlays(ed) {
  const { THREE, $ } = ed;
  const groups = {};
  for (const k of ['wards', 'stations', 'flatten']) { groups[k] = new THREE.Group(); groups[k].visible = false; ed.scene.add(groups[k]); }
  ed.overlayGroups = groups;
  const COLORS = { wards: 0xff7043, stations: 0x4fc3ff, level: 0xffd54f, smooth: 0xffd54f };
  let data = null;
  const toScene = (gx, gz, lift = 0.4) => new THREE.Vector3(gx - ed.cx, ed.sampleHeight(Math.max(0, Math.min(ed.W - 1, gx)), Math.max(0, Math.min(ed.H - 1, gz))) + lift, -(gz - ed.cz));
  // A ring (or square) on the ground: points every metre or so, following its shape.
  function ring(x, z, r, color, opacity = 0.9, square = false) {
    const gx = x - ed.originX, gz = z - ed.originZ, pts = [];
    if (square) for (const [ax, az, bx, bz] of [[-r, -r, r, -r], [r, -r, r, r], [r, r, -r, r], [-r, r, -r, -r]]) {
      const n = Math.max(1, Math.ceil(2 * r));
      for (let k = 0; k <= n; k++) pts.push(toScene(gx + ax + (bx - ax) * k / n, gz + az + (bz - az) * k / n));
    } else {
      const n = Math.max(24, Math.ceil(2 * Math.PI * r));
      for (let k = 0; k <= n; k++) { const a = k / n * Math.PI * 2; pts.push(toScene(gx + Math.cos(a) * r, gz + Math.sin(a) * r)); }
    }
    const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthTest: false }));
    line.renderOrder = 8; line.frustumCulled = false;
    return line;
  }
  const clear = g => { for (const c of [...g.children]) { g.remove(c); c.geometry.dispose(); c.material.dispose(); } };
  function draw() {
    if (!data) return;
    for (const g of Object.values(groups)) clear(g);
    const counts = { wards: 0, stations: 0, flatten: 0 };
    for (const r of ed.objects.alive()) {
      const range = data.ranges[r.name];
      if (!range) continue;
      if (range[0] > 0) { groups.wards.add(ring(r.x, r.z, range[0], COLORS.wards)); counts.wards++; }
      if (range[1] > 0) { groups.stations.add(ring(r.x, r.z, range[1], COLORS.stations)); counts.stations++; }
    }
    for (const f of data.flatten) {
      // Level: flat ground; smooth: where it blends into the land around (fainter).
      if (f.level > 0) groups.flatten.add(ring(f.x, f.z, f.level, COLORS.level, 0.95, f.square));
      if (f.smooth > 0) groups.flatten.add(ring(f.x, f.z, f.smooth, COLORS.smooth, 0.35, f.square));
      counts.flatten++;
    }
    for (const [k, n] of Object.entries(counts)) { const el = document.querySelector(`[data-count="${k}"]`); if (el) el.textContent = `(${n})`; }
  }
  fetch(`/api/overlays?x0=${ed.X0}&z0=${ed.Z0}&x1=${ed.X1}&z1=${ed.Z1}`).then(r => r.json()).then(d => { data = d; draw(); }).catch(() => {});
  let timer = 0;
  const later = () => { clearTimeout(timer); timer = setTimeout(draw, 300); };
  (ed.onObjects ??= []).push(later);
  (ed.onObjectsChanged ??= []).push(later);
  // The rings follow the ground: drawn again when it changed (after a stroke or an undo).
  (ed.onTerrainChanged ??= []).push(later);

  // ---- Go to a player (live mode).
  (ed.onPlayers ??= []).push(list => {
    $('playersBox').hidden = !list.length;
    const keep = $('goPlayer').value;
    $('goPlayer').innerHTML = list.map(p => `<option value="${p.name}">${p.name}</option>`).join('');
    if (list.some(p => p.name === keep)) $('goPlayer').value = keep;
  });
  $('goPlayerBtn').onclick = () => {
    const p = (ed.players ?? []).find(x => x.name === $('goPlayer').value);
    if (p) ed.gotoWorld(p.x, p.z);
  };
}
