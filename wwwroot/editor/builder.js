// Who builds: the player id the game finds as "creator" on the pieces the editor places, so it treats
// them as player built (full materials back when taken down, a base for fires, a target for raids,
// wards and private chests that answer to that player). View panel: Built by. Select tool: Make
// player built, for pieces placed without a builder (by older versions of the editor).

export function createBuilder(ed) {
  const { $ } = ed;
  const key = `builder:${ed.world.name}`;
  let players = [];
  const label = p => `${p.name ?? `Player ${p.id}`}${p.local ? ' (this computer)' : ''}${p.pieces ? ` · ${p.pieces} piece${p.pieces === 1 ? '' : 's'}` : ''}`;
  function fill(current) {
    $('builder').innerHTML = players.map(p => `<option value="${p.id}">${label(p)}</option>`).join('')
      + '<option value="other">Other player id…</option><option value="0">Nobody (not player built)</option>';
    $('builder').value = current === '0' || players.some(p => p.id === current) ? current : players[0]?.id ?? '0';
  }
  async function choose(id) {
    const r = await fetch('/api/builder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id }) });
    if (!r.ok) { ed.msg(await r.text(), true); return false; }
    try { localStorage.setItem(key, id); } catch { }
    return true;
  }
  async function load() {
    const res = await fetch('/api/builders').then(r => r.json());
    players = res.players;
    let current = res.builder;
    let saved = null; try { saved = localStorage.getItem(key); } catch { }
    if (saved && saved !== current && /^-?\d+$/.test(saved)) {
      if (saved !== '0' && !players.some(p => p.id === saved)) players.push({ id: saved, name: null, pieces: 0 });
      if (await choose(saved)) current = saved;
    }
    fill(current);
  }
  let last = '';
  $('builder').onfocus = () => { last = $('builder').value; };
  $('builder').onchange = async () => {
    let id = $('builder').value;
    if (id === 'other') {
      id = (prompt('Player id to write as the builder (the number Valheim keeps for a character):', '') ?? '').trim();
      if (!/^-?\d+$/.test(id) || /^-?0+$/.test(id)) { $('builder').value = last; if (id) ed.msg('A player id is a whole number other than 0.', true); return; }
      if (!players.some(p => p.id === id)) players.push({ id, name: null, pieces: 0 });
    }
    if (await choose(id)) { fill(id); last = id; ed.msg(id === '0' ? 'New pieces get no builder: the game takes them for parts of a ruin.' : `New pieces are built by ${label(players.find(p => p.id === id))}.`); }
    else $('builder').value = last;
  };
  load();

  // ---- Select tool: Make player built.
  const row = document.createElement('div');
  row.className = 'row';
  row.innerHTML = '<button id="selClaim" disabled>Make player built</button>';
  $('selectPanel').querySelector('.row').after(row);
  (ed.onSelection ??= []).push(() => { $('selClaim').disabled = !ed.selection.size; });
  $('selClaim').onclick = async () => {
    await ed.flushTransform?.();
    const ids = [...ed.selection].filter(id => ed.objects.records.get(id));
    const objects = ids.map(id => ({ id, newId: ed.objects.reserveId() }));
    const r = await fetch('/api/objects/claim', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ objects }) });
    if (!r.ok) { ed.msg(await r.text(), true); return; }
    const res = await r.json();
    if (!res.changed.length) { ed.msg('Nothing to change: the selected pieces already have a builder, or are not things players build.'); return; }
    const old = res.changed.map(c => c.id), added = [];
    for (const c of res.changed) {
      await ed.objects.adopt({ ...c.detail, name: c.detail.name ?? ed.objects.records.get(c.id)?.name, scale: 0 });
      added.push(c.detail.id);
    }
    await ed.setDeleted(old, true);
    ed.showPendingFrom(res.pending);
    ed.changed?.();
    ed.pushHistory({ deleted: old, added, label: `Made ${added.length} piece(s) player built` });
    ed.selectIds([...ed.selection].filter(id => !old.includes(id)).concat(added));
    ed.msg(`${added.length} piece(s) are now player built (${ids.length - added.length} left as they were). Save or Apply live writes it.`);
  };
}
