// Point handles for drawn lines (Path tool, Place tool lines): dots on the line's points that can be dragged
// to fine-tune it, a drag on the line between two points adds one there, Ctrl + click removes one.
// The tool keeps its points ({ gx, gz } on the editor's grid); this draws the dots and tells which
// point or which stretch of the line is under the mouse.

export function createHandles(ed, color = 0xffffff) {
  const { THREE } = ed;
  const dots = new THREE.Points(new THREE.BufferGeometry(), new THREE.PointsMaterial({ color, size: 9, sizeAttenuation: false, depthTest: false }));
  dots.renderOrder = 13; dots.frustumCulled = false; dots.visible = false; ed.scene.add(dots);
  const v = new THREE.Vector3();
  // Screen position (CSS pixels) of a grid point on the ground.
  function screen(p) {
    v.set(p.gx - ed.cx, ed.sampleHeight(p.gx, p.gz) + 0.6, -(p.gz - ed.cz)).project(ed.camera);
    const r = ed.el.getBoundingClientRect();
    return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height, v.z];
  }
  return {
    draw(pts, show = true) {
      dots.visible = show && pts.length > 0;
      dots.geometry.setFromPoints(pts.map(p => new THREE.Vector3(p.gx - ed.cx, ed.sampleHeight(p.gx, p.gz) + 0.6, -(p.gz - ed.cz))));
    },
    hide() { dots.visible = false; },
    // Index of the point within 10 px of the mouse, or -1.
    point(e, pts) {
      let best = -1, bd = 10;
      pts.forEach((p, i) => { const [x, y, z] = screen(p); const d = Math.hypot(x - e.clientX, y - e.clientY); if (z < 1 && d < bd) { bd = d; best = i; } });
      return best;
    },
    // The stretch of the line under the mouse (within 8 px): the index of the point it starts from.
    // dense: the drawn line as points, each with seg = the index of the point its stretch starts from.
    segment(e, dense) {
      let best = -1, bd = 8;
      for (let i = 1; i < dense.length; i++) {
        // The line follows the ground: checked every metre or so, not as one straight stretch.
        const p = dense[i - 1], q = dense[i], n = Math.max(1, Math.ceil(Math.hypot(q.gx - p.gx, q.gz - p.gz)));
        let prev = screen(p);
        for (let k = 1; k <= n; k++) {
          const cur = screen({ gx: p.gx + (q.gx - p.gx) * k / n, gz: p.gz + (q.gz - p.gz) * k / n });
          const [ax, ay, az] = prev, [bx, by, bz] = cur;
          prev = cur;
          if (az >= 1 || bz >= 1) continue;
          const dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy || 1e-9;
          const t = Math.max(0, Math.min(1, ((e.clientX - ax) * dx + (e.clientY - ay) * dy) / l2));
          const d = Math.hypot(e.clientX - (ax + dx * t), e.clientY - (ay + dy * t));
          if (d < bd) { bd = d; best = p.seg ?? 0; }
        }
      }
      return best;
    },
  };
}
