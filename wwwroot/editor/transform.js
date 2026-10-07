// Select tool: move, turn and lift the selected objects, and copy them. A move is shown live on the
// instances; when it ends, the originals are deleted and copies of their own data (chest contents,
// health, builder...) are added at the new place, so undo, saving and live apply all just work.

export function createTransform(ed) {
  const { THREE, $ } = ed;
  let session = null, commitTimer = null;
  const D = Math.PI / 180;

  const groundAt = (gx, gz) => ed.sampleHeight(gx, gz);
  const toGrid = r => ({ gx: r.x - ed.originX, gz: r.z - ed.originZ });

  function begin() {
    if (session) return session;
    const ids = [...ed.selection].filter(id => ed.objects.records.get(id) && !ed.objects.records.get(id).deleted);
    if (!ids.length) return null;
    let cx = 0, cz = 0;
    const items = ids.map(id => {
      const r = ed.objects.records.get(id), g = toGrid(r);
      cx += g.gx; cz += g.gz;
      // Original instance matrices, to restore and to transform from.
      const inst = ed.entityOf(id)?.inst.map(({ im, k }) => { const m = new THREE.Matrix4(); im.getMatrixAt(k, m); return { im, k, m }; }) ?? [];
      return { id, r, g, lift: r.y - groundAt(g.gx, g.gz), inst };
    });
    session = { items, cx: cx / ids.length, cz: cz / ids.length, dgx: 0, dgz: 0, dy: 0, turn: 0 };
    return session;
  }
  // Where an item ends up: turned around the selection centre (clockwise from above, like Unity's
  // yaw), moved, and kept at the same height above the ground.
  function target(s, it) {
    const t = s.turn * D, c = Math.cos(t), sn = Math.sin(t), ox = it.g.gx - s.cx, oz = it.g.gz - s.cz;
    const gx = s.cx + ox * c + oz * sn + s.dgx, gz = s.cz - ox * sn + oz * c + s.dgz;
    return { gx, gz, y: groundAt(gx, gz) + it.lift + s.dy };
  }
  const A = new THREE.Matrix4(), T1 = new THREE.Matrix4(), R = new THREE.Matrix4(), T2 = new THREE.Matrix4(), M = new THREE.Matrix4();
  function preview() {
    const s = session; if (!s) return;
    for (const it of s.items) {
      const p = target(s, it);
      // three.js space: x, y, -z; a Unity yaw of +a is a rotation of -a about y.
      T1.makeTranslation(-(it.g.gx - ed.cx), -it.r.y, it.g.gz - ed.cz);
      R.makeRotationY(-s.turn * D);
      T2.makeTranslation(p.gx - ed.cx, p.y, -(p.gz - ed.cz));
      A.multiplyMatrices(T2, R).multiply(T1);
      for (const { im, k, m } of it.inst) { im.setMatrixAt(k, M.multiplyMatrices(A, m)); im.instanceMatrix.needsUpdate = true; }
    }
    ed.drawSelection();
    const moved = Math.hypot(s.dgx, s.dgz);
    ed.msg(`Moving ${s.items.length} object(s): ${moved.toFixed(1)} m${s.turn ? `, turned ${s.turn}°` : ''}${s.dy ? `, ${s.dy > 0 ? '+' : ''}${s.dy.toFixed(2)} m` : ''}`);
  }
  function restore(s) { for (const it of s.items) for (const { im, k, m } of it.inst) { im.setMatrixAt(k, m); im.instanceMatrix.needsUpdate = true; } }
  // A move being written: new turns, lifts and drops wait for it, so they act on the new copies.
  let committing = null;
  const afterCommit = f => (...a) => committing ? committing.then(() => f(...a)) : f(...a);
  function commit() {
    clearTimeout(commitTimer); commitTimer = null;
    if (!session) return committing ?? Promise.resolve();
    const p = commitNow().finally(() => { if (committing === p) committing = null; });
    return committing = p;
  }
  async function commitNow() {
    const s = session; if (!s) return;
    session = null;
    restore(s);
    if (Math.hypot(s.dgx, s.dgz) < 0.01 && !s.turn && Math.abs(s.dy) < 0.001 && !s.dropped) { ed.drawSelection(); return; }
    const copies = s.items.map(it => {
      const p = target(s, it), r = it.r;
      return { name: r.name, x: ed.originX + p.gx, y: p.y, z: ed.originZ + p.gz, rx: r.rx, ry: r.ry + s.turn, rz: r.rz, scale: r.scale,
        sourceId: r.added ? r.sourceId ?? null : r.id, fresh: r.added ? r.fresh ?? true : false, rawOf: r.raw ? r.id : null };
    });
    const ids = s.items.map(it => it.id);
    await ed.setDeleted(ids, true);
    const added = await ed.objects.add(copies);
    ed.pushHistory({ deleted: ids, added, label: `Moved ${added.length} object(s)` });
    ed.selectIds(added);
    ed.msg(`Moved ${added.length} object(s). Ctrl+Z puts them back.`);
  }
  const later = () => { clearTimeout(commitTimer); commitTimer = setTimeout(commit, 700); };
  const turn = afterCommit(deg => { if (!begin()) return; session.turn = ((session.turn + deg) % 360 + 360) % 360; if (session.turn > 180) session.turn -= 360; preview(); later(); });
  const lift = afterCommit(m => { if (!begin()) return; session.dy += m; preview(); later(); });
  // End: drop each selected object onto whatever is under it: the top of another object, or else the
  // ground (its base at ground level, as the game places things).
  const down = new THREE.Vector3(0, -1, 0), dropRay = new THREE.Raycaster(), bb = new THREE.Box3(), pb = new THREE.Box3(), pm = new THREE.Matrix4();
  const drop = afterCommit(() => {
    const s = begin(); if (!s) return;
    const sel = ed.selection;
    const groups = [ed.buildings, ed.newGroup, ...Object.values(ed.objectGroups)].filter(g => g.visible);
    let onObjects = 0;
    for (const it of s.items) {
      const p = target(s, it);
      // The object's current box (the instances already show any move in progress).
      bb.makeEmpty();
      for (const { im, k } of it.inst) { if (!im.geometry.boundingBox) im.geometry.computeBoundingBox(); im.getMatrixAt(k, pm); bb.union(pb.copy(im.geometry.boundingBox).applyMatrix4(pm)); }
      if (bb.isEmpty()) bb.setFromCenterAndSize(new THREE.Vector3(p.gx - ed.cx, p.y, -(p.gz - ed.cz)), new THREE.Vector3(0.2, 0.2, 0.2));
      const cx = (bb.min.x + bb.max.x) / 2, cz = (bb.min.z + bb.max.z) / 2, hx = (bb.max.x - bb.min.x) * 0.35, hz = (bb.max.z - bb.min.z) * 0.35;
      // Highest thing under the middle and four points of the footprint.
      let best = -Infinity;
      for (const [ox, oz] of [[0, 0], [-hx, -hz], [hx, -hz], [-hx, hz], [hx, hz]]) {
        dropRay.set(new THREE.Vector3(cx + ox, bb.max.y + 0.05, cz + oz), down);
        dropRay.far = bb.max.y - p.y + 200;
        const terrain = dropRay.intersectObject(ed.mesh, false)[0];
        for (const h of dropRay.intersectObjects(groups, true)) {
          if (terrain && h.distance > terrain.distance) break;
          const id = h.object.userData.ids?.[h.instanceId];
          // Hidden objects (deleted, or the originals of moved ones) are not there.
          if (id == null || sel.has(id) || ed.objects.records.get(id)?.deleted) continue;
          best = Math.max(best, h.point.y);
          break;
        }
      }
      const ground = groundAt(p.gx, p.gz);
      // On an object: the bottom of the box rests on its top. Otherwise: the base on the ground.
      const y = best > ground + 0.01 ? best - (bb.min.y - p.y) : ground;
      if (best > ground + 0.01) onObjects++;
      it.lift = y - ground - s.dy;
    }
    s.dropped = true;
    preview(); later();
    ed.msg(`Dropped ${s.items.length} object(s): ${onObjects} onto other objects, ${s.items.length - onObjects} onto the ground.`);
  });
  ed.flushTransform = commit;

  // ---- Move arrows (like Blender's): red X (east), green Y (up), blue Z (north) at the selection's
  // centre. Dragging one moves the selection along that axis only; X and Z keep following the ground.
  const AXES = { x: { dir: new THREE.Vector3(1, 0, 0), color: 0xff4d5e }, y: { dir: new THREE.Vector3(0, 1, 0), color: 0x6ee05a }, z: { dir: new THREE.Vector3(0, 0, -1), color: 0x4d8dff } };
  const gizmo = new THREE.Group(); gizmo.visible = false; gizmo.renderOrder = 30; ed.scene.add(gizmo);
  const hitMat = new THREE.MeshBasicMaterial({ visible: false });
  // The arrows start a bit out from the middle, and only they catch the mouse, so the object itself
  // can still be grabbed for a free move.
  const shaftGeo = new THREE.CylinderGeometry(0.025, 0.025, 0.7, 8).translate(0, 0.65, 0), headGeo = new THREE.ConeGeometry(0.08, 0.24, 16).translate(0, 1.1, 0);
  const grabGeo = new THREE.CylinderGeometry(0.09, 0.09, 0.95, 8).translate(0, 0.75, 0);
  for (const [key, a] of Object.entries(AXES)) {
    const arm = new THREE.Group();
    a.mat = new THREE.MeshBasicMaterial({ color: a.color, depthTest: false, transparent: true });
    for (const g of [shaftGeo, headGeo]) { const m = new THREE.Mesh(g, a.mat); m.renderOrder = 30; arm.add(m); }
    const grab = new THREE.Mesh(grabGeo, hitMat); grab.userData.axis = key; arm.add(grab);
    if (key === 'x') arm.rotation.z = -Math.PI / 2;
    if (key === 'z') arm.rotation.x = -Math.PI / 2;
    gizmo.add(arm);
  }
  const centre = new THREE.Vector3();
  // Where the arrows go: the middle of the selection, following a move in progress.
  function placeGizmo() {
    const show = ed.tool === 'select' && ed.selection.size > 0;
    gizmo.visible = show;
    if (!show) return;
    let x = 0, y = 0, z = 0, n = 0;
    if (session) for (const it of session.items) { const p = target(session, it); x += p.gx; y += p.y; z += p.gz; n++; }
    else for (const id of ed.selection) { const r = ed.objects.records.get(id); if (!r || r.deleted) continue; const g = toGrid(r); x += g.gx; y += r.y; z += g.gz; n++; }
    if (!n) { gizmo.visible = false; return; }
    centre.set(x / n - ed.cx, y / n, -(z / n - ed.cz));
    gizmo.position.copy(centre);
    gizmo.scale.setScalar(ed.camera.position.distanceTo(centre) * 0.07);
    for (const [k, a] of Object.entries(AXES)) a.mat.color.setHex(k === (axisDrag?.axis ?? hoverAxis) ? 0xffe14a : a.color);
  }
  ed.frame.push(placeGizmo);
  const ray = new THREE.Raycaster(), mouse = new THREE.Vector2();
  function rayFor(e) {
    const r = ed.el.getBoundingClientRect();
    mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    ray.setFromCamera(mouse, ed.camera);
    return ray;
  }
  function gizmoAxis(e) {
    if (!gizmo.visible) return null;
    gizmo.updateMatrixWorld(true);
    return rayFor(e).intersectObject(gizmo, true).find(h => h.object.userData.axis)?.object.userData.axis ?? null;
  }
  // How far along the axis (through o) the mouse ray passes closest.
  function along(e, o, a) {
    const { origin: p, direction: d } = rayFor(e).ray;
    const w = o.clone().sub(p), b = a.dot(d), den = 1 - b * b;
    if (den < 1e-4) return null;   // looking straight down the axis
    return (b * d.dot(w) - a.dot(w)) / den;
  }
  let axisDrag = null, hoverAxis = null;

  // ---- Lasso: drag on empty ground to draw a zone; everything shown inside it gets selected.
  let lasso = null;
  const lassoLine = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xffc24a, depthTest: false }));
  lassoLine.renderOrder = 23; lassoLine.frustumCulled = false; lassoLine.visible = false; ed.scene.add(lassoLine);
  function drawLasso() {
    const ring = lasso ? [...lasso.pts, lasso.pts[0]] : [], out = [];
    for (let i = 1; i < ring.length; i++) {
      const a = ring[i - 1], b = ring[i], n = Math.max(1, Math.ceil(Math.hypot(b.gx - a.gx, b.gz - a.gz)));
      for (let k = 0; k <= n; k++) { const gx = a.gx + (b.gx - a.gx) * k / n, gz = a.gz + (b.gz - a.gz) * k / n; out.push(new THREE.Vector3(gx - ed.cx, groundAt(gx, gz) + 0.3, -(gz - ed.cz))); }
    }
    lassoLine.geometry.setFromPoints(out);
    lassoLine.visible = out.length > 0;
  }
  function inside(poly, x, z) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if ((a.gz > z) !== (b.gz > z) && x < (b.gx - a.gx) * (z - a.gz) / (b.gz - a.gz) + a.gx) c = !c;
    }
    return c;
  }
  // Only what is drawn can be picked, like a click (see View).
  const shown = id => ed.entityOf(id)?.inst.some(({ im }) => im.visible && im.parent?.visible);
  function finishLasso(add) {
    const poly = lasso.pts; lasso = null; drawLasso();
    if (poly.length < 3) return false;
    const ids = [];
    for (const r of ed.objects.records.values()) {
      if (r.deleted) continue;
      const g = toGrid(r);
      if (inside(poly, g.gx, g.gz) && shown(r.id)) ids.push(r.id);
    }
    ed.selectIds(add ? [...ed.selection, ...ids] : ids);
    ed.msg(ids.length ? `Selected ${ids.length} object(s) inside the zone${add ? ` (${ed.selection.size} in all)` : ''}.` : 'Nothing shown inside the zone.');
    return true;
  }

  // ---- Pointer: drag an arrow or a selected object to move the selection; otherwise the usual selecting.
  let drag = null;
  ed.handlers.select = {
    down(e, hit) {
      const axis = gizmoAxis(e);
      if (axis) {
        clearTimeout(commitTimer);
        const s = begin(); if (!s) return;
        const o = centre.clone(), s0 = along(e, o, AXES[axis].dir);
        if (s0 == null) return;
        axisDrag = { axis, o, s0, dgx: s.dgx, dgz: s.dgz, dy: s.dy };
        ed.el.setPointerCapture(e.pointerId);
        return;
      }
      const ent = e.altKey ? null : ed.pickEntity(e);   // Alt: always draw a zone, even over objects
      if (ent && ed.selection.has(ent.id) && !e.shiftKey && hit) {
        drag = { x: e.clientX, y: e.clientY, from: hit, moving: false };
        ed.el.setPointerCapture(e.pointerId);
        return;
      }
      if (!ent && hit) {
        // Empty ground: a click deselects, a drag draws a zone to select.
        lasso = { x: e.clientX, y: e.clientY, pts: [{ gx: hit.gx, gz: hit.gz }], drawing: false, add: e.shiftKey };
        ed.el.setPointerCapture(e.pointerId);
        commit();
        return;
      }
      commit().then(() => ed.clickSelect(e));
    },
    move(e, hit) {
      if (axisDrag) {
        const t = along(e, axisDrag.o, AXES[axisDrag.axis].dir);
        if (t == null || !session) return;
        let d = t - axisDrag.s0;
        if (e.ctrlKey) d = Math.round(d / 0.5) * 0.5;   // Ctrl: half-metre steps
        session.dgx = axisDrag.dgx; session.dgz = axisDrag.dgz; session.dy = axisDrag.dy;
        if (axisDrag.axis === 'x') session.dgx += d; else if (axisDrag.axis === 'z') session.dgz += d; else session.dy += d;
        preview();
        return;
      }
      if (lasso) {
        if (!lasso.drawing && Math.hypot(e.clientX - lasso.x, e.clientY - lasso.y) > 6) lasso.drawing = true;
        const last = lasso.pts.at(-1);
        if (lasso.drawing && hit && Math.hypot(hit.gx - last.gx, hit.gz - last.gz) >= 0.5) { lasso.pts.push({ gx: hit.gx, gz: hit.gz }); drawLasso(); }
        return;
      }
      if (!drag) {
        const a = gizmoAxis(e);
        if (a !== hoverAxis) { hoverAxis = a; ed.el.style.cursor = a ? 'grab' : ''; }
        if (!a) ed.hoverSelect(e);
        return;
      }
      if (!drag.moving && Math.hypot(e.clientX - drag.x, e.clientY - drag.y) > 5) { drag.moving = true; begin(); }
      if (drag.moving && hit && session) { session.dgx = hit.gx - drag.from.gx; session.dgz = hit.gz - drag.from.gz; preview(); }
    },
    up(e) {
      if (axisDrag) { axisDrag = null; commit(); return; }
      if (lasso) {
        const l = lasso;
        if (!l.drawing || !finishLasso(l.add)) { lasso = null; drawLasso(); if (!l.add) ed.clearSelection(); }
        return;
      }
      const d = drag; drag = null;
      if (!d) return;
      if (d.moving) commit();
      else commit().then(() => ed.clickSelect(e));   // a plain click on a selected object
    },
    key(e) {
      if (e.key === 'Escape' && lasso) { lasso = null; drawLasso(); return true; }
      if (!ed.selection.size) return false;
      if (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>') { turn((e.key === ',' || e.key === '<' ? -1 : 1) * ed.turnStep(e)); return true; }
      if (e.key === 'End') { drop(); return true; }
      if (e.key === 'PageUp' || e.key === 'PageDown') { lift((e.key === 'PageUp' ? 1 : -1) * (e.shiftKey ? 1 : 0.25)); return true; }
      if (e.key === 'Escape' && session) { axisDrag = null; restore(session); session = null; ed.drawSelection(); ed.msg('Move cancelled.'); return true; }
      return false;
    }
  };
  ed.el.addEventListener('wheel', e => {
    if (ed.tool !== 'select' || !e.altKey || !ed.selection.size) return;
    e.preventDefault(); e.stopImmediatePropagation();
    turn(Math.sign(e.deltaY) * ed.turnStep(e));
  }, { capture: true, passive: false });
  ed.onToolChange.push(() => { if (session) commit(); });

  // ---- Copy the selection (Ctrl+C in the Select tool); paste uses the Area tool's paste preview.
  async function copySelection() {
    await commit();
    const recs = [...ed.selection].map(id => ed.objects.records.get(id)).filter(r => r && !r.deleted && ed.objects.state.templates.has(r.prefab));
    if (!recs.length) { ed.msg('Select objects to copy first.', true); return; }
    let cx = 0, cz = 0, x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
    for (const r of recs) { const g = toGrid(r); cx += g.gx; cz += g.gz; x0 = Math.min(x0, g.gx); x1 = Math.max(x1, g.gx); z0 = Math.min(z0, g.gz); z1 = Math.max(z1, g.gz); }
    cx /= recs.length; cz /= recs.length;
    const objects = recs.map(r => { const g = toGrid(r); return { name: r.name, dx: g.gx - cx, dz: g.gz - cz, dy: r.y - groundAt(g.gx, g.gz), follow: true, rx: r.rx, ry: r.ry, rz: r.rz, scale: r.scale, sourceId: r.added ? r.sourceId ?? null : r.id }; });
    const pad = 1;
    ed.setClipboard({ w: 1, h: 1, rel: new Float32Array([NaN]), wt: new Float32Array(1), pnt: new Float32Array(4).fill(-1), objects,
      poly: [{ gx: x0 - cx - pad, gz: z0 - cz - pad }, { gx: x1 - cx + pad, gz: z0 - cz - pad }, { gx: x1 - cx + pad, gz: z1 - cz + pad }, { gx: x0 - cx - pad, gz: z1 - cz + pad }] });
    ed.msg(`Copied ${objects.length} object(s). Ctrl+V to paste (R turns, F mirrors).`);
  }
  (ed.keys ??= []).push(e => {
    if (e.ctrlKey && e.key.toLowerCase() === 'c' && ed.tool === 'select') { copySelection(); return true; }
    return false;
  });
}
