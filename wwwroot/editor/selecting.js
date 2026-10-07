// More ways to select (Axiom's magic select): a double click on a building piece selects the whole
// building, every piece connected to it through pieces that touch.

export function createSelecting(ed) {
  const { $ } = ed;
  // Building pieces: player-built ones, and pieces of the game's build catalogue placed without a
  // builder (ruins, or pieces placed by tools).
  const isPiece = r => r && !r.deleted && (r.kind === 'buildings' || ed.objects.state.pieceNames.has(r.name));
  const row = document.createElement('div');
  row.className = 'row';
  row.innerHTML = '<button id="selBuilding" title="Add every piece connected to the selected ones (double-click a piece does it too)">Whole building</button>';
  $('selectPanel').querySelector('.row').after(row);

  // The world box of each drawn building piece, slightly grown so pieces that touch overlap.
  function pieceBoxes() {
    const out = [];
    for (const r of ed.objects.records.values()) {
      if (!isPiece(r)) continue;
      const ent = ed.entityOf(r.id);
      if (!ent?.inst.length) continue;
      const b = ed.boxOf(ent);
      if (b.isEmpty()) continue;
      out.push({ id: r.id, box: b.expandByScalar(0.15) });
    }
    return out;
  }
  // Every piece connected to the start pieces, walking from piece to touching piece.
  function connected(startIds) {
    const boxes = pieceBoxes(), byId = new Map(boxes.map(b => [b.id, b]));
    const cell = 4, grid = new Map(), key = (x, y, z) => `${x},${y},${z}`;
    const cellsOf = b => {
      const out = [];
      for (let x = Math.floor(b.min.x / cell); x <= Math.floor(b.max.x / cell); x++)
        for (let y = Math.floor(b.min.y / cell); y <= Math.floor(b.max.y / cell); y++)
          for (let z = Math.floor(b.min.z / cell); z <= Math.floor(b.max.z / cell); z++) out.push(key(x, y, z));
      return out;
    };
    for (const b of boxes) for (const k of cellsOf(b.box)) (grid.get(k) ?? grid.set(k, []).get(k)).push(b);
    const seen = new Set(startIds.filter(id => byId.has(id))), queue = [...seen];
    while (queue.length) {
      const b = byId.get(queue.pop());
      for (const k of cellsOf(b.box)) for (const o of grid.get(k) ?? []) {
        if (seen.has(o.id) || !o.box.intersectsBox(b.box)) continue;
        seen.add(o.id); queue.push(o.id);
      }
    }
    return [...seen];
  }
  function selectBuilding(ids) {
    const start = ids.filter(id => isPiece(ed.objects.records.get(id)));
    if (!start.length) { ed.msg('Select a building piece first (or double-click one).', true); return; }
    const all = connected(start);
    ed.selectIds([...new Set([...ed.selection, ...all])]);
    ed.msg(`Selected the whole building: ${all.length} connected piece(s).`);
  }
  $('selBuilding').onclick = () => selectBuilding([...ed.selection]);
  const handler = ed.handlers.select;
  handler.dblclick = e => {
    const ent = ed.pickEntity(e);
    if (ent && isPiece(ed.objects.records.get(ent.id))) selectBuilding([ent.id]);
  };
  ed.onSelection ??= [];
  ed.onSelection.push(() => { $('selBuilding').disabled = ![...ed.selection].some(id => isPiece(ed.objects.records.get(id))); });
  ed.selecting = { connected, selectBuilding };
}
