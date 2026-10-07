// More ways to select (Axiom's magic select, WorldEdit's selection tools): a double click on a building
// piece selects the whole building (every piece connected to it through pieces that touch); select
// every object of the same kinds, invert the selection, and keep selections by name.

export function createSelecting(ed) {
  const { $ } = ed;
  // Building pieces: player-built ones, and pieces of the game's build catalogue placed without a
  // builder (ruins, or pieces placed by tools).
  const isPiece = r => r && !r.deleted && (r.kind === 'buildings' || ed.objects.state.pieceNames.has(r.name));
  const row = document.createElement('div');
  row.className = 'row';
  row.innerHTML = '<button id="selBuilding" title="Add every piece connected to the selected ones (double-click a piece does it too)">Whole building</button><button id="selSame">Same kind</button><button id="selInvert">Invert</button>';
  $('selectPanel').querySelector('.row').after(row);
  const saved = document.createElement('div');
  saved.innerHTML = `<label class="field">Saved <select id="selSaved"><option value="">Saved selections…</option></select><span><button id="selSaveBtn" class="mini" title="Keep the selection under a name">keep</button></span></label>
    <div class="row" id="selSavedRow" hidden><button id="selLoad">Select it</button><button id="selAddSaved">Add it</button><button id="selForget">Forget</button></div>`;
  row.after(saved);

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
  // Only what is drawn can be selected, like a click (see View).
  const shown = id => ed.entityOf(id)?.inst.some(({ im }) => im.visible && im.parent?.visible);
  const alive = () => [...ed.objects.records.values()].filter(r => !r.deleted && shown(r.id));
  $('selSame').onclick = () => {
    const names = new Set([...ed.selection].map(id => ed.objects.records.get(id)?.name).filter(Boolean));
    if (!names.size) { ed.msg('Select an object of each kind you want first.', true); return; }
    const ids = alive().filter(r => names.has(r.name)).map(r => r.id);
    ed.selectIds(ids);
    ed.msg(`Selected every shown ${[...names].slice(0, 4).join(', ')}${names.size > 4 ? '…' : ''} in the area: ${ids.length} object(s).`);
  };
  $('selInvert').onclick = () => {
    const ids = alive().filter(r => !ed.selection.has(r.id)).map(r => r.id);
    ed.selectIds(ids);
    ed.msg(`Selected the ${ids.length} shown object(s) that were not selected.`);
  };

  // Saved selections, per world, in this browser. Ids change when the world is saved, so objects are
  // found again by kind and position.
  const KEY = `savedSelections:${ed.world.name}`;
  const read = () => { try { return JSON.parse(localStorage.getItem(KEY) ?? '[]'); } catch { return []; } };
  const write = list => { try { localStorage.setItem(KEY, JSON.stringify(list)); } catch { ed.msg('This browser could not keep the selection.', true); } };
  function fillSaved() {
    const list = read(), v = $('selSaved').value;
    $('selSaved').innerHTML = `<option value="">${list.length ? 'Saved selections…' : 'No saved selections'}</option>` + list.map((s, i) => `<option value="${i}">${s.name.replace(/</g, '&lt;')} (${s.items.length})</option>`).join('');
    if (v && +v < list.length) $('selSaved').value = v;
    $('selSavedRow').hidden = $('selSaved').value === '';
  }
  $('selSaved').addEventListener('change', fillSaved);
  $('selSaveBtn').onclick = () => {
    const recs = [...ed.selection].map(id => ed.objects.records.get(id)).filter(r => r && !r.deleted);
    if (!recs.length) { ed.msg('Select something first.', true); return; }
    const name = prompt('Name of the selection:', `Selection ${read().length + 1}`)?.trim();
    if (!name) return;
    const list = read().filter(s => s.name !== name);
    list.push({ name, items: recs.map(r => ({ name: r.name, x: +r.x.toFixed(3), y: +r.y.toFixed(3), z: +r.z.toFixed(3) })) });
    write(list); fillSaved();
    $('selSaved').value = String(list.length - 1); fillSaved();
    ed.msg(`Kept the selection “${name}” (${recs.length} object(s)). It can be picked again here, also after saving the world.`);
  };
  function findSaved(s) {
    const recs = [...ed.objects.records.values()].filter(r => !r.deleted), ids = [];
    let missing = 0;
    for (const it of s.items) {
      const r = recs.find(r => r.name === it.name && Math.abs(r.x - it.x) < 0.05 && Math.abs(r.z - it.z) < 0.05 && Math.abs(r.y - it.y) < 0.2);
      if (r) ids.push(r.id); else missing++;
    }
    return { ids, missing };
  }
  function useSaved(add) {
    const s = read()[+$('selSaved').value];
    if (!s) return;
    const { ids, missing } = findSaved(s);
    ed.setTool('select');
    ed.selectIds(add ? [...ed.selection, ...ids] : ids);
    ed.msg(`Selected ${ids.length} object(s) of “${s.name}”${missing ? `; ${missing} are not in this area or no longer where they were` : ''}.`, missing > 0);
  }
  $('selLoad').onclick = () => useSaved(false);
  $('selAddSaved').onclick = () => useSaved(true);
  $('selForget').onclick = () => {
    const list = read(), i = +$('selSaved').value;
    if (!list[i] || !confirm(`Forget the selection “${list[i].name}”?`)) return;
    list.splice(i, 1); write(list); $('selSaved').value = ''; fillSaved();
  };
  fillSaved();
  ed.selecting = { connected, selectBuilding, findSaved };
}
