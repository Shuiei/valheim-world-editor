// Object inspector (MCEdit's NBT editor, Amulet's entity editor): everything the selected object holds
// in the save, by name, and editing it: sign text, portal tag, chest contents, ward permissions,
// crop timers... An edit replaces the object by a copy with the new data, one undo step.

// Readable names for the data most players look for. The key is the game's own name.
const LABEL = {
  text: 'Text (sign)', tag: 'Tag (portal)', creator: 'Builder (player id)', creatorName: 'Builder name', author: 'Author',
  health: 'Health', plantTime: 'Planted at (game time)', items: 'Contents', permitted: 'Permitted players', enabled: 'Enabled (1 = on)',
  item: 'Item', variant: 'Variant', quality: 'Quality', ownerName: 'Owner name', owner: 'Owner (player id)', fuel: 'Fuel', StartTime: 'Started at',
  level: 'Level (stars)', tamed: 'Tamed (1 = yes)', support: 'Support', scale: 'Scale', picked: 'Picked (1 = yes)', picked_time: 'Picked at',
  spawntime: 'Spawned at', lastTime: 'Last updated (game time)', accTime: 'Time accumulated', queued: 'Queued items', state: 'State',
};
const SECTIONS = { strings: 'text', ints: 'whole number', floats: 'number', longs: 'large number', vec3: '3 numbers (x y z)', quats: '4 numbers (x y z w)', bytes: 'data' };
const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

export function createInspector(ed) {
  const { $ } = ed;
  const panel = document.createElement('aside');
  panel.id = 'inspPanel'; panel.className = 'card side'; panel.hidden = true;
  panel.innerHTML = `
    <h3><span id="inTitle">Object</span> <button class="ghost" id="inClose" title="Close (Esc)">✕</button></h3>
    <div class="hint" id="inWhere" style="margin-top:0"></div>
    <div id="inInv"></div>
    <div class="sub"><h3>Data</h3><div id="inFields"></div>
      <div class="inAdd"><select id="inAddSec">${Object.entries(SECTIONS).filter(([k]) => k !== 'bytes').map(([k, v]) => `<option value="${k}">${v}</option>`).join('')}</select>
        <input id="inAddKey" placeholder="name (text, tag…)"><input id="inAddVal" placeholder="value"><button id="inAddBtn" title="Add this value to the object">Add</button></div>
    </div>
    <div class="row"><button id="inApply" class="primary" disabled>Apply changes</button><button id="inRevert" disabled>Revert</button></div>
    <div class="hint">Applying replaces the object by a copy with the new data (Ctrl+Z puts the old one back), like a move does; Save or Apply live writes it. Change values only if you know what they do: the game may reset or ignore wrong ones.</div>
    <datalist id="inItemNames"></datalist>`;
  document.body.appendChild(panel);
  const style = document.createElement('style');
  style.textContent = `
    #inspPanel { position: fixed; right: 10px; top: 70px; width: 360px; padding: 12px 14px; max-height: calc(100vh - 136px); overflow-y: auto; overflow-x: hidden; z-index: 6; }
    #inspPanel input, #inspPanel select { min-width: 0; }
    #inspPanel h3 { margin: 0 0 6px; display: flex; justify-content: space-between; align-items: center; }
    #inspPanel > h3 { font-size: 14px; }
    .inRow { display: grid; grid-template-columns: minmax(0, 1fr) 150px 22px; gap: 6px; align-items: center; padding: 3px 0; border-bottom: 1px solid rgba(255,255,255,.04); }
    .inRow .k { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .inRow .k small { color: var(--muted); margin-left: 4px; }
    .inRow input { width: 100%; background: var(--panel2); color: var(--text); border: 1px solid var(--line); border-radius: 5px; padding: 3px 5px; font: inherit; font-size: 12px; }
    .inRow.removed .k, .inRow.removed input { text-decoration: line-through; opacity: .5; }
    .inRow.changed input { border-color: var(--accent); }
    .inRow button, .inItem button { padding: 2px 5px; font-size: 11px; }
    .inRow .note { grid-column: 1 / -1; color: var(--muted); font-size: 11px; margin-top: -2px; }
    .inAdd { display: grid; grid-template-columns: 92px minmax(0, 1fr) minmax(0, 1fr) auto; gap: 4px; margin-top: 8px; }
    .inAdd input, .inAdd select { padding: 3px 4px; font-size: 12px; }
    .inItem { display: grid; grid-template-columns: minmax(0, 1fr) 50px 38px 46px 34px 34px 22px; gap: 4px; align-items: center; margin: 2px 0; }
    .inItem input { width: 100%; background: var(--panel2); color: var(--text); border: 1px solid var(--line); border-radius: 5px; padding: 3px 4px; font: inherit; font-size: 12px; }
    .inItem.head { color: var(--muted); font-size: 10.5px; text-transform: uppercase; letter-spacing: .04em; }
    .inItem.bad input[data-f="x"], .inItem.bad input[data-f="y"] { border-color: var(--warn); }`;
  document.head.appendChild(style);
  fetch('/api/items').then(r => r.json()).then(names => { $('inItemNames').innerHTML = names.map(n => `<option value="${esc(n)}">`).join(''); }).catch(() => { });

  let detail = null, fields = [], items = null, added = [];
  const ITEMS_KEY = ed.objects.stableHash('items');

  async function open(id) {
    const r = await fetch(`/api/object/${id}`);
    if (!r.ok) { ed.msg('That object cannot be read.', true); return; }
    detail = await r.json();
    reset();
    for (const el of ['viewPanel', 'historyPanel', 'help', 'bpPanel']) { const e = $(el); if (e) e.hidden = true; }
    for (const el of ['viewToggle', 'historyToggle', 'helpToggle']) $(el)?.classList.remove('on');
    panel.hidden = false;
  }
  function reset() {
    fields = detail.fields.map(f => ({ ...f, now: f.value, removed: false }));
    items = detail.inventory?.items ? detail.inventory.items.map(i => ({ ...i })) : detail.inventory ? [] : null;
    added = [];
    render();
  }
  const label = f => f.name ? (LABEL[f.name] ? `${LABEL[f.name]}` : f.name) : `#${f.key}`;
  function render() {
    $('inTitle').textContent = detail.name ?? `Prefab ${detail.prefab}`;
    $('inWhere').textContent = `Object ${detail.id} · x ${detail.x.toFixed(2)}, y ${detail.y.toFixed(2)}, z ${detail.z.toFixed(2)} · turned ${detail.ry.toFixed(1)}°${detail.connection ? ' · has a connection (kept)' : ''}`;
    $('inFields').innerHTML = fields.length || added.length ? [...fields.map((f, i) => {
      const isItems = f.section === 'bytes' && f.key === ITEMS_KEY && items && !detail.inventory?.error;
      const ro = f.section === 'bytes';
      return `<div class="inRow${f.removed ? ' removed' : ''}${f.now !== f.value ? ' changed' : ''}" data-i="${i}">
        <div class="k" title="${esc(f.name ?? '')} · ${SECTIONS[f.section]} · key ${f.key}">${esc(label(f))}${f.name && LABEL[f.name] ? `<small>${esc(f.name)}</small>` : ''}</div>
        <input value="${esc(isItems ? 'see Contents above' : ro ? f.note : f.now)}" ${ro ? 'readonly' : ''} title="${SECTIONS[f.section]}">
        <button data-rm title="${f.removed ? 'Keep this value' : 'Remove this value'}">${f.removed ? '↺' : '✕'}</button>
        ${f.note && !ro ? `<div class="note">${esc(f.note)}</div>` : ''}</div>`;
    }), ...added.map((a, i) => `<div class="inRow changed" data-a="${i}"><div class="k">${esc(LABEL[a.key] ?? a.key)} <small>new</small></div><input value="${esc(a.value)}"><button data-rma title="Do not add">✕</button></div>`)].join('')
      : '<div class="hint">This object holds no data: the game uses its defaults.</div>';
    $('inFields').querySelectorAll('.inRow[data-i]').forEach(row => {
      const f = fields[+row.dataset.i], input = row.querySelector('input');
      input.oninput = () => { f.now = input.value; row.classList.toggle('changed', f.now !== f.value); dirty(); };
      row.querySelector('[data-rm]').onclick = () => { f.removed = !f.removed; render(); };
    });
    $('inFields').querySelectorAll('.inRow[data-a]').forEach(row => {
      const a = added[+row.dataset.a];
      row.querySelector('input').oninput = e => { a.value = e.target.value; dirty(); };
      row.querySelector('[data-rma]').onclick = () => { added.splice(+row.dataset.a, 1); render(); };
    });
    renderItems();
    dirty();
  }
  // Container size from the game; unknown for carts and ships (their container is on a part).
  const grid = () => ({ w: detail.inventory?.width || 0, h: detail.inventory?.height || 0 });
  function renderItems() {
    const box = $('inInv');
    if (!detail.inventory) { box.innerHTML = ''; return; }
    if (detail.inventory.error) { box.innerHTML = `<div class="sub"><h3>Contents</h3><div class="hint warn">The contents cannot be read: ${esc(detail.inventory.error)}</div></div>`; return; }
    const { w, h } = grid();
    box.innerHTML = `<div class="sub"><h3>Contents <span class="hint" style="margin:0;text-transform:none;letter-spacing:0">${w ? `${w} × ${h} slots` : 'size unknown'} · ${items.length} item(s)</span></h3>
      <div class="inItem head"><span>Item</span><span>Stack</span><span>Qual.</span><span>Dur. %</span><span>X</span><span>Y</span><span></span></div>
      ${items.map((it, i) => `<div class="inItem${w && (it.x >= w || it.y >= h) ? ' bad' : ''}" data-i="${i}">
        <input data-f="name" list="inItemNames" value="${esc(it.name)}" title="The item's prefab name"><input data-f="stack" type="number" min="1" value="${it.stack}">
        <input data-f="quality" type="number" min="1" max="10" value="${it.quality}"><input data-f="durability" type="number" min="0" step="1" value="${Math.round(it.durability)}">
        <input data-f="x" type="number" min="0" value="${it.x}"><input data-f="y" type="number" min="0" value="${it.y}"><button data-rm title="Take this item out">✕</button></div>`).join('')}
      <div class="row"><button id="inAddItem">Add item</button><button id="inSortItems" title="Put every item in the first free slots, row by row">Tidy slots</button></div></div>`;
    box.querySelectorAll('.inItem[data-i]').forEach(row => {
      const it = items[+row.dataset.i];
      row.querySelectorAll('input').forEach(input => input.oninput = () => {
        const f = input.dataset.f;
        it[f] = f === 'name' ? input.value.trim() : +input.value;
        if (f === 'name') delete it.prefab;
        if (f === 'x' || f === 'y') row.classList.toggle('bad', !!w && (it.x >= w || it.y >= h));
        dirty();
      });
      row.querySelector('[data-rm]').onclick = () => { items.splice(+row.dataset.i, 1); renderItems(); dirty(); };
    });
    $('inAddItem').onclick = () => {
      const slot = freeSlot();
      if (!slot) { ed.msg('No free slot left in this container.', true); return; }
      items.push({ name: 'Wood', stack: 1, quality: 1, durability: 100, x: slot.x, y: slot.y, variant: 0, worldLevel: 0, crafterId: '0', crafterName: '', equipped: false, pickedUp: false, cheated: false, customData: {} });
      renderItems(); dirty();
      $('inInv').querySelector('.inItem[data-i]:last-of-type input[data-f="name"]')?.select();
    };
    $('inSortItems').onclick = () => {
      const { w: gw } = grid(), width = gw || 4;
      items.forEach((it, i) => { it.x = i % width; it.y = Math.floor(i / width); });
      renderItems(); dirty();
    };
  }
  // First free slot, row by row. Without a known size: 4 wide, 2 rows (the smallest containers).
  function freeSlot() {
    const { w, h } = grid(), gw = w || 4, gh = h || Math.max(2, ...items.map(i => i.y + 1));
    for (let y = 0; y < gh; y++) for (let x = 0; x < gw; x++) if (!items.some(i => i.x === x && i.y === y)) return { x, y };
    return null;
  }
  const original = () => JSON.stringify(detail.inventory?.items ?? []);
  function changes() {
    const set = [];
    for (const f of fields) {
      if (f.removed) set.push({ section: f.section, key: String(f.key), value: null });
      else if (f.now !== f.value && f.section !== 'bytes') set.push({ section: f.section, key: String(f.key), value: String(f.now) });
    }
    for (const a of added) if (a.key.trim()) set.push({ section: a.section, key: a.key.trim(), value: String(a.value) });
    const inv = items && !detail.inventory?.error && JSON.stringify(items) !== original() ? items : null;
    return { set, inv };
  }
  function dirty() {
    const c = changes(), any = c.set.length > 0 || !!c.inv;
    $('inApply').disabled = $('inRevert').disabled = !any;
  }
  $('inAddBtn').onclick = () => {
    const key = $('inAddKey').value.trim();
    if (!key) { ed.msg('Type the name of the value to add (for example text for a sign).', true); return; }
    added.push({ section: $('inAddSec').value, key, value: $('inAddVal').value });
    $('inAddKey').value = ''; $('inAddVal').value = '';
    render();
  };
  $('inRevert').onclick = reset;
  $('inClose').onclick = () => { panel.hidden = true; };
  $('inApply').onclick = async () => {
    const { set, inv } = changes();
    if (inv) {
      const { w, h } = grid();
      if (w && inv.some(i => i.x >= w || i.y >= h) && !confirm(`Some items are outside the ${w} × ${h} slots of this container: the game would not show them. Apply anyway?`)) return;
    }
    await ed.flushTransform?.();
    const oldId = detail.id, newId = ed.objects.reserveId();
    const r = await fetch('/api/objects/edit', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: oldId, newId, set, inventory: inv }) });
    if (!r.ok) { ed.msg(`Could not change the object: ${await r.text()}`, true); return; }
    const res = await r.json();
    await ed.objects.adopt({ ...res.detail, name: res.detail.name ?? detail.name, scale: 0 });
    await ed.setDeleted([oldId], true);
    ed.showPendingFrom(res.pending);
    ed.changed?.();
    ed.pushHistory({ deleted: [oldId], added: [newId], label: `Edited ${detail.name ?? 'object'}` });
    ed.selectIds([newId]);
    detail = res.detail; detail.name ??= res.detail.name;
    reset();
    ed.msg(`Changed ${detail.name ?? 'the object'}. Ctrl+Z puts the old one back; Save or Apply live writes it.`);
  };
  ed.inspect = open;
  ed.inspector = { open, close: () => { panel.hidden = true; }, get open_() { return !panel.hidden; } };
  // Select tool: I (or the button) opens the inspector on the selected object.
  const selRow = document.createElement('div');
  selRow.className = 'row';
  selRow.innerHTML = '<button id="selInspect" disabled>Inspect data <kbd>I</kbd></button>';
  $('selectPanel').querySelector('.row').after(selRow);
  const one = () => ed.selection.size === 1 ? [...ed.selection][0] : null;
  $('selInspect').onclick = () => { const id = one(); if (id != null) open(id); };
  ed.onSelection ??= [];
  ed.onSelection.push(() => {
    $('selInspect').disabled = one() == null;
    if (!panel.hidden && one() != null && one() !== detail?.id) open(one());
  });
  (ed.keys ??= []).push(e => {
    if (e.key.toLowerCase() === 'i' && !e.ctrlKey && ed.tool === 'select') { const id = one(); if (id != null) { open(id); return true; } }
    if (e.key === 'Escape' && !panel.hidden) { panel.hidden = true; return true; }
    return false;
  });
}
