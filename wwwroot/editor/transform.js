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
  async function commit() {
    clearTimeout(commitTimer); commitTimer = null;
    const s = session; if (!s) return;
    session = null;
    restore(s);
    if (Math.hypot(s.dgx, s.dgz) < 0.01 && !s.turn && Math.abs(s.dy) < 0.001) { ed.drawSelection(); return; }
    const copies = s.items.map(it => {
      const p = target(s, it), r = it.r;
      return { name: r.name, x: ed.originX + p.gx, y: p.y, z: ed.originZ + p.gz, rx: r.rx, ry: r.ry + s.turn, rz: r.rz, scale: r.scale,
        sourceId: r.added ? r.sourceId ?? null : r.id, fresh: r.added ? r.fresh ?? true : false };
    });
    const ids = s.items.map(it => it.id);
    await ed.setDeleted(ids, true);
    const added = await ed.objects.add(copies);
    ed.pushHistory({ deleted: ids, added, label: `Moved ${added.length} object(s)` });
    ed.selectIds(added);
    ed.msg(`Moved ${added.length} object(s). Ctrl+Z puts them back.`);
  }
  const later = () => { clearTimeout(commitTimer); commitTimer = setTimeout(commit, 700); };
  function turn(deg) { if (!begin()) return; session.turn = ((session.turn + deg) % 360 + 360) % 360; if (session.turn > 180) session.turn -= 360; preview(); later(); }
  function lift(m) { if (!begin()) return; session.dy += m; preview(); later(); }
  ed.flushTransform = commit;

  // ---- Pointer: drag a selected object to move the selection; otherwise the usual selecting.
  let drag = null;
  ed.handlers.select = {
    down(e, hit) {
      const ent = ed.pickEntity(e);
      if (ent && ed.selection.has(ent.id) && !e.shiftKey && hit) {
        drag = { x: e.clientX, y: e.clientY, from: hit, moving: false };
        ed.el.setPointerCapture(e.pointerId);
        return;
      }
      commit().then(() => ed.clickSelect(e));
    },
    move(e, hit) {
      if (!drag) { ed.hoverSelect(e); return; }
      if (!drag.moving && Math.hypot(e.clientX - drag.x, e.clientY - drag.y) > 5) { drag.moving = true; begin(); }
      if (drag.moving && hit && session) { session.dgx = hit.gx - drag.from.gx; session.dgz = hit.gz - drag.from.gz; preview(); }
    },
    up(e) {
      const d = drag; drag = null;
      if (!d) return;
      if (d.moving) commit();
      else commit().then(() => ed.clickSelect(e));   // a plain click on a selected object
    },
    key(e) {
      if (!ed.selection.size) return false;
      if (e.key === ',' || e.key === '<' || e.key === '.' || e.key === '>') { turn((e.key === ',' || e.key === '<' ? -1 : 1) * (e.shiftKey ? 5 : 15)); return true; }
      if (e.key === 'PageUp' || e.key === 'PageDown') { lift((e.key === 'PageUp' ? 1 : -1) * (e.shiftKey ? 1 : 0.25)); return true; }
      if (e.key === 'Escape' && session) { restore(session); session = null; ed.drawSelection(); ed.msg('Move cancelled.'); return true; }
      return false;
    }
  };
  ed.el.addEventListener('wheel', e => {
    if (ed.tool !== 'select' || !e.altKey || !ed.selection.size) return;
    e.preventDefault(); e.stopImmediatePropagation();
    turn(Math.sign(e.deltaY) * (e.shiftKey ? 5 : 15));
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
