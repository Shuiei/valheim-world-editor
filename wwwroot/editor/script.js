// Script console (like MCEdit's filters): a few lines of JavaScript run against the world with a
// small API (vwe), for jobs the tools do not cover. Coordinates are the game's, in metres (x east,
// z north, y up). Everything one run changes is one step in History. Scripts can be kept in this
// browser; a few examples come with it.

const EXAMPLES = {
  'Count the kinds in the selection': `// Objects inside the Area selection (or the whole area), by kind.
const counts = {};
for (const o of vwe.objects({ inSelection: true })) counts[o.name] = (counts[o.name] ?? 0) + 1;
for (const [name, n] of Object.entries(counts).sort((a, b) => b[1] - a[1])) vwe.log(name, n);`,
  'Remove young beeches in the selection': `// Beech_small1 and Beech_small2 inside the selection go; grown beeches stay.
const young = vwe.objects({ name: /^Beech_small/, inSelection: true });
vwe.remove(young);
vwe.log(\`Removed \${young.length} young beech(es).\`);`,
  'Rocks on steep slopes': `// A rock every few metres where the ground is steeper than 30°, inside the selection.
const step = 4, list = [];
for (const p of vwe.points({ inSelection: true, every: step })) {
  const dx = vwe.ground(p.x + 1, p.z) - vwe.ground(p.x - 1, p.z), dz = vwe.ground(p.x, p.z + 1) - vwe.ground(p.x, p.z - 1);
  const slope = Math.atan(Math.hypot(dx, dz) / 2) * 180 / Math.PI;
  if (slope > 30 && Math.random() < 0.5) list.push({ name: Math.random() < 0.7 ? 'Rock_3' : 'Rock_4', x: p.x + Math.random() * 2 - 1, z: p.z + Math.random() * 2 - 1, ry: Math.random() * 360 });
}
vwe.add(list);
vwe.log(\`Placed \${list.length} rock(s).\`);`,
  'Terraces': `// The ground inside the selection in steps of 2 m (rounded down), like rice terraces.
const step = 2;
for (const p of vwe.points({ inSelection: true })) vwe.setGround(p.x, p.z, Math.floor(p.h / step) * step);`,
};

export function createScript(ed) {
  const { $, W, H } = ed;
  const panel = document.createElement('aside');
  panel.id = 'scriptPanel'; panel.className = 'card side'; panel.hidden = true;
  panel.innerHTML = `<h3>Script <button class="ghost" id="scClose" title="Close">✕</button></h3>
    <label class="field">Script <select id="scPick"></select></label>
    <textarea id="scCode" spellcheck="false" wrap="off"></textarea>
    <div class="row"><button id="scRun" class="primary">Run <kbd>Ctrl+Enter</kbd></button><button id="scSave">Keep as…</button><button id="scDel" disabled>Delete</button></div>
    <pre id="scOut"></pre>
    <details class="hint"><summary>API (vwe)</summary><div id="scHelp">
      <b>vwe.objects({ name, kind, inSelection })</b>: objects (id, name, kind, x, y, z, ry, scale); name: text or /pattern/.<br>
      <b>vwe.add([{ name, x, z, y, ry, rx, rz, scale }])</b>: new objects (y: the ground) · <b>vwe.remove(objects or ids)</b><br>
      <b>vwe.ground(x, z)</b>, <b>vwe.original(x, z)</b>: height now / as generated · <b>vwe.setGround(x, z, y)</b> (±8 m limit)<br>
      <b>vwe.points({ inSelection, every })</b>: ground points (x, z, h, weight) · <b>vwe.selection()</b>, <b>vwe.inside(x, z)</b><br>
      <b>vwe.kinds()</b>: kinds that can be placed · <b>vwe.select(objects)</b> · <b>vwe.log(...)</b>. A run is one undo step.
    </div></details>`;
  document.body.appendChild(panel);
  const style = document.createElement('style');
  style.textContent = `
    #scriptPanel { position: fixed; right: 10px; top: 70px; width: 420px; padding: 12px 14px; max-height: calc(100vh - 136px); overflow-y: auto; z-index: 6; }
    #scriptPanel h3 { display: flex; justify-content: space-between; align-items: center; }
    #scCode { width: 100%; height: 220px; font: 12px/1.45 ui-monospace, monospace; background: var(--panel2); color: var(--text); border: 1px solid var(--line); border-radius: 6px; padding: 8px; resize: vertical; box-sizing: border-box; tab-size: 2; }
    #scOut { max-height: 160px; overflow: auto; font: 12px/1.4 ui-monospace, monospace; white-space: pre-wrap; margin: 6px 0; color: var(--muted); }
    #scOut .err { color: var(--warn); }
    #scHelp { font-size: 11.5px; line-height: 1.5; margin-top: 4px; }`;
  document.head.appendChild(style);
  const btn = document.createElement('button');
  btn.id = 'scriptToggle'; btn.className = 'ghost'; btn.title = 'Run a script on the world (JavaScript with a small API)';
  btn.innerHTML = '<svg class="i" viewBox="0 0 24 24"><path d="M8 7l-5 5 5 5M16 7l5 5-5 5"/></svg>Script';
  $('historyToggle').before(btn);

  // ---- Kept scripts (this browser) and the examples.
  const read = () => { try { const v = JSON.parse(localStorage.getItem('editorScripts') ?? '{}'); return v && typeof v === 'object' ? v : {}; } catch { return {}; } };
  let mine = read();
  function fillPick(sel) {
    $('scPick').innerHTML = `<optgroup label="Examples">${Object.keys(EXAMPLES).map(n => `<option value="ex:${n}">${n}</option>`).join('')}</optgroup>`
      + (Object.keys(mine).length ? `<optgroup label="Yours">${Object.keys(mine).map(n => `<option value="my:${n}">${n}</option>`).join('')}</optgroup>` : '');
    if (sel) $('scPick').value = sel;
    $('scDel').disabled = !$('scPick').value.startsWith('my:');
  }
  const codeOf = v => v.startsWith('ex:') ? EXAMPLES[v.slice(3)] : mine[v.slice(3)];
  $('scPick').onchange = () => { $('scCode').value = codeOf($('scPick').value) ?? ''; $('scDel').disabled = !$('scPick').value.startsWith('my:'); try { localStorage.setItem('editorScriptLast', $('scPick').value); } catch { } };
  $('scSave').onclick = () => {
    const name = (prompt('Name for this script:', $('scPick').value.startsWith('my:') ? $('scPick').value.slice(3) : '') ?? '').trim();
    if (!name) return;
    mine[name] = $('scCode').value;
    try { localStorage.setItem('editorScripts', JSON.stringify(mine)); } catch { }
    fillPick(`my:${name}`);
  };
  $('scDel').onclick = () => {
    const v = $('scPick').value;
    if (!v.startsWith('my:') || !confirm(`Delete the script ${v.slice(3)}?`)) return;
    delete mine[v.slice(3)];
    try { localStorage.setItem('editorScripts', JSON.stringify(mine)); } catch { }
    fillPick(); $('scPick').onchange();
  };
  let last = null; try { last = localStorage.getItem('editorScriptLast'); } catch { }
  fillPick(last && codeOf(last) != null ? last : undefined);
  $('scCode').value = codeOf($('scPick').value) ?? '';

  // ---- The API for one run.
  const toG = (x, z) => ({ gx: x - ed.originX, gz: z - ed.originZ });
  const inArea = (gx, gz) => gx >= 0 && gz >= 0 && gx <= W - 1 && gz <= H - 1;
  function api(out, run) {
    const poly = ed.area?.polygon?.() ?? null;
    const insidePoly = (gx, gz) => {
      if (!poly) return true;
      let c = false;
      for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
        const a = poly[i], b = poly[j];
        if ((a.gz > gz) !== (b.gz > gz) && gx < (b.gx - a.gx) * (gz - a.gz) / (b.gz - a.gz) + a.gx) c = !c;
      }
      return c;
    };
    // "In the selection" without one would be the whole area: stop instead.
    const needSelection = () => { if (!poly) throw new Error('Select an area first (Area tool): this script works inside the selection.'); };
    const matches = (v, want) => want == null || (want instanceof RegExp ? want.test(v) : v === want);
    const idsOf = list => (Array.isArray(list) ? list : [list]).map(o => typeof o === 'object' ? o.id : o);
    return {
      objects({ name, kind, inSelection = false } = {}) {
        if (inSelection) needSelection();
        return ed.objects.alive().filter(r => matches(r.name, name) && matches(r.kind, kind) && (!inSelection || insidePoly(...Object.values(toG(r.x, r.z)))))
          .map(r => ({ id: r.id, name: r.name, kind: r.kind, x: r.x, y: r.y, z: r.z, ry: r.ry, scale: r.scale, added: !!r.added }));
      },
      ground(x, z) { const { gx, gz } = toG(x, z); return inArea(gx, gz) ? ed.sampleHeight(gx, gz) : null; },
      original(x, z) { const { gx, gz } = toG(x, z); return inArea(gx, gz) ? ed.base[Math.round(gz) * W + Math.round(gx)] : null; },
      setGround(x, z, y) {
        const { gx, gz } = toG(x, z), ix = Math.round(gx), iz = Math.round(gz);
        if (!inArea(ix, iz) || ed.locked(ix, iz)) return false;
        const g = iz * W + ix;
        ed.setHeight(g, y); run.touched.add(g);
        return true;
      },
      *points({ inSelection = false, every = 1 } = {}) {
        if (inSelection) needSelection();
        const w = inSelection ? ed.area?.weights?.() : null;
        if (w) {
          for (const [g, weight] of w.cells) {
            const gx = g % W, gz = (g - gx) / W;
            if (gx % every || gz % every) continue;
            yield { x: gx + ed.originX, z: gz + ed.originZ, h: ed.height(g), weight };
          }
          return;
        }
        for (let gz = 1; gz < H - 1; gz += every) for (let gx = 1; gx < W - 1; gx += every) yield { x: gx + ed.originX, z: gz + ed.originZ, h: ed.height(gz * W + gx), weight: 1 };
      },
      selection() { return poly && poly.map(p => ({ x: p.gx + ed.originX, z: p.gz + ed.originZ })); },
      inside(x, z) { const { gx, gz } = toG(x, z); return inArea(gx, gz) && insidePoly(gx, gz); },
      kinds() { return ed.objects.creatableTypes().map(t => t.name); },
      async add(list) {
        const ok = new Set(this.kinds()), skipped = new Set();
        const rows = (Array.isArray(list) ? list : [list]).filter(o => ok.has(o.name) || (skipped.add(o.name), false)).map(o => {
          const y = o.y ?? this.ground(o.x, o.z);
          return { name: o.name, x: o.x, y: y ?? 0, z: o.z, rx: o.rx ?? 0, ry: o.ry ?? 0, rz: o.rz ?? 0, scale: o.scale ?? 0, fresh: true };
        }).filter(o => this.ground(o.x, o.z) != null);
        if (skipped.size) out(`Not placeable here: ${[...skipped].join(', ')}`);
        const ids = await ed.objects.add(rows);
        run.added.push(...ids);
        return ids;
      },
      remove(list) { const ids = idsOf(list).filter(id => ed.objects.records.get(id) && !ed.objects.records.get(id).deleted); ed.setDeleted(ids, true); run.deleted.push(...ids); return ids.length; },
      select(list) { ed.setTool('select'); ed.selectIds(idsOf(list)); },
      log(...args) { out(args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' ')); },
    };
  }

  async function runScript() {
    const lines = [];
    const out = (text, err = false) => { lines.push(err ? `<span class="err">${text.replace(/</g, '&lt;')}</span>` : text.replace(/</g, '&lt;')); $('scOut').innerHTML = lines.join('\n'); };
    const run = { touched: new Set(), added: [], deleted: [] };
    const before = ed.snapshotState();
    $('scOut').textContent = '';
    try {
      const fn = new (Object.getPrototypeOf(async function () { }).constructor)('vwe', $('scCode').value);
      await fn(api(out, run));
    } catch (e) {
      out(`${e.name}: ${e.message}`, true);
    }
    // Whatever the run changed (also when it stopped on an error) is one step in History.
    let zones = [];
    if (run.touched.size) {
      zones = ed.zonesOf(run.touched);
      ed.refresh();
      ed.upload(zones);
    }
    if (zones.length || run.added.length || run.deleted.length) {
      ed.pushHistory({ ...(zones.length ? { state: before, zones } : {}), ...(run.added.length ? { added: run.added } : {}), ...(run.deleted.length ? { deleted: run.deleted } : {}), label: `Script: ${$('scPick').selectedOptions[0]?.textContent ?? 'run'}` });
      ed.changed?.();
    }
    const said = [run.touched.size ? `${run.touched.size} ground point(s)` : '', run.added.length ? `${run.added.length} added` : '', run.deleted.length ? `${run.deleted.length} removed` : ''].filter(Boolean).join(', ');
    ed.msg(said ? `Script: ${said}. Ctrl+Z takes it back.` : 'Script: nothing changed.');
    if (!lines.length) out(said ? `Done: ${said}.` : 'Done: nothing changed.');
  }
  $('scRun').onclick = runScript;
  $('scCode').addEventListener('keydown', e => {
    if (e.key === 'Enter' && e.ctrlKey) { e.preventDefault(); runScript(); }
    if (e.key === 'Tab') { e.preventDefault(); const t = e.target, s = t.selectionStart; t.setRangeText('  ', s, t.selectionEnd, 'end'); }
    e.stopPropagation();   // typing here is not a shortcut
  });

  function toggle(open = panel.hidden) {
    panel.hidden = !open; btn.classList.toggle('on', open);
    if (open) {
      for (const id of ['viewPanel', 'historyPanel', 'help']) { const el = $(id); if (el) el.hidden = true; }
      for (const id of ['viewToggle', 'historyToggle', 'helpToggle']) $(id)?.classList.remove('on');
      for (const el of document.querySelectorAll('aside.side')) if (el !== panel) el.hidden = true;
    }
  }
  btn.onclick = () => toggle();
  $('scClose').onclick = () => toggle(false);
  ed.script = { run: runScript, toggle };
}
